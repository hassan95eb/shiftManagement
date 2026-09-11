"""Pure scoring functions for the ShiftFlow applicant recommender.

No I/O, no third-party imports. Every number is a :class:`decimal.Decimal` so a
result never depends on binary floating-point representation, and every rounding
step is ``ROUND_HALF_UP`` (CLAUDE.md #5).

The stored ``Recommendations.Score`` is the **sum of the three one-decimal
component figures**, then widened to two decimals -- it is *not* the raw
``FinalScore`` rounded to two decimals. For the seeded ``nate`` row that is the
difference between ``71.50`` (what the seed stores, what this module produces)
and ``71.46`` (raw formula, wrong). See ``python/README.md`` ->
"Why the total is a sum of rounded parts".

The output format matches ``backend/src/ShiftFlow.Infrastructure/Persistence/
SeedData.cs`` and ``database/03-seed.sql`` byte for byte -- the Python recommender
overwrites those seeded rows on its first run, so the two must agree exactly.
"""

from __future__ import annotations

from dataclasses import dataclass
from decimal import Decimal, ROUND_HALF_UP
from typing import Iterable, Optional

__all__ = [
    "ScoringConfig",
    "DEFAULT_CONFIG",
    "ScoreBreakdown",
    "RankableApplicant",
    "rating_ratio",
    "workload_ratio",
    "availability_ratio",
    "weighted_component",
    "score",
    "sort_key",
    "rank",
]

_ONE_DP = Decimal("0.1")
_TWO_DP = Decimal("0.01")
_HUNDRED = Decimal("100")
_FIVE = Decimal("5")
_ONE = Decimal("1")


def _round1(value: Decimal) -> Decimal:
    """One decimal place, half away from zero (CLAUDE.md #5: ``ROUND_HALF_UP``)."""
    return value.quantize(_ONE_DP, rounding=ROUND_HALF_UP)


def _fmt1(value: Decimal) -> str:
    """A weighted component as it appears in ``Reason``: exactly one decimal."""
    return f"{_round1(value):.1f}"


def _fmt_hours(value: Decimal) -> str:
    """Hours as they appear in ``Reason``.

    The brief calls for ``:g`` formatting -- whole hours stay ``8``, a half hour
    reads ``12.5``. ``Decimal`` with ``:g`` can fall into scientific notation
    (``Decimal("200")`` -> ``2e+2``), so the ``:g`` intent is implemented
    directly instead. No seeded row exercises the fractional branch; that form is
    a convention this module sets, not one it matches (see ``python/README.md``).
    """
    if value == value.to_integral_value():
        return str(int(value))
    return f"{value.normalize():f}"


@dataclass(frozen=True)
class ScoringConfig:
    """Weights, monthly cap and rating fallback.

    The defaults are the values fixed in CLAUDE.md #5 -- the same ones the
    backend is meant to bind from configuration. ``config.py`` overrides any of
    them from the environment.
    """

    rating_weight: Decimal = Decimal("0.30")
    workload_weight: Decimal = Decimal("0.30")
    availability_weight: Decimal = Decimal("0.40")
    monthly_cap: Decimal = Decimal("160")
    rating_default: Decimal = Decimal("3.0")


DEFAULT_CONFIG = ScoringConfig()


@dataclass(frozen=True)
class ScoreBreakdown:
    """The three weighted components (one decimal each), their 2-decimal sum, and
    the traceable ``Reason`` string."""

    rating_component: Decimal
    workload_component: Decimal
    availability_component: Decimal
    final_score: Decimal
    reason: str


@dataclass(frozen=True)
class RankableApplicant:
    """The fields the CLAUDE.md #5 tie-break needs. ``applied_at_utc`` may be any
    consistently ordered type (``datetime``, ISO-8601 ``str``, ...); every item
    in one ranking must use the same type."""

    call_agent_id: int
    final_score: Decimal
    approved_hours: Decimal
    applied_at_utc: object


# --- component ratios (unweighted, unrounded, 0..1) -----------------------------


def rating_ratio(previous_month_rating: Optional[Decimal], config: ScoringConfig = DEFAULT_CONFIG) -> Decimal:
    """``(previous-month rating or the configured default) / 5``.

    "Previous month" is the month before ``Shift.StartUtc``'s month; resolving it
    is the caller's job. ``None`` here means the call_agent has no rating row.
    """
    rating = config.rating_default if previous_month_rating is None else previous_month_rating
    return rating / _FIVE


def workload_ratio(approved_hours: Decimal, config: ScoringConfig = DEFAULT_CONFIG) -> Decimal:
    """``1 - min(ApprovedHours / MonthlyCap, 1)``. Hours are for the month of
    ``Shift.StartUtc``; at or beyond the cap the ratio is ``0``."""
    return _ONE - min(approved_hours / config.monthly_cap, _ONE)


def availability_ratio(shift_hours: Decimal, covering_window_hours: Decimal) -> Decimal:
    """``shift duration / covering availability window duration``. The window
    contains the shift by construction, so the ratio is in ``(0, 1]``."""
    return shift_hours / covering_window_hours


def weighted_component(weight: Decimal, ratio: Decimal) -> Decimal:
    """``weight * ratio * 100``, rounded to one decimal (half up). This rounding
    happens *per component*, before the components are summed."""
    return _round1(weight * ratio * _HUNDRED)


# --- full score + reason ------------------------------------------------------


def score(
    *,
    previous_month_rating: Optional[Decimal],
    approved_hours: Decimal,
    shift_hours: Decimal,
    covering_window_hours: Decimal,
    config: ScoringConfig = DEFAULT_CONFIG,
) -> ScoreBreakdown:
    """Score one applicant of one shift.

    ``final_score`` is ``rating_component + workload_component +
    availability_component`` (each already at one decimal), quantized to two
    decimals for the ``DECIMAL(5,2)`` column -- never the raw formula rounded to
    two decimals.
    """
    rating_c = weighted_component(config.rating_weight, rating_ratio(previous_month_rating, config))
    workload_c = weighted_component(config.workload_weight, workload_ratio(approved_hours, config))
    availability_c = weighted_component(
        config.availability_weight, availability_ratio(shift_hours, covering_window_hours)
    )

    total_1dp = rating_c + workload_c + availability_c
    final_score = total_1dp.quantize(_TWO_DP, rounding=ROUND_HALF_UP)

    if previous_month_rating is None:
        rating_token = f"Rating default {config.rating_default:.1f}/5 -> {_fmt1(rating_c)}"
    else:
        rating_token = f"Rating {previous_month_rating:.1f}/5 -> {_fmt1(rating_c)}"

    reason = " | ".join(
        (
            rating_token,
            f"Workload {_fmt_hours(approved_hours)}h -> {_fmt1(workload_c)}",
            f"Availability {_fmt_hours(shift_hours)}/{_fmt_hours(covering_window_hours)}h -> {_fmt1(availability_c)}",
            f"Total {_round1(total_1dp):.1f}",
        )
    )

    return ScoreBreakdown(rating_c, workload_c, availability_c, final_score, reason)


# --- ranking ---------------------------------------------------------------------


def sort_key(applicant: RankableApplicant) -> tuple:
    """CLAUDE.md #5 tie-break, as a ``sorted(key=...)`` function: score
    descending, then fewer approved hours, then earlier ``AppliedAtUtc``, then
    lower call_agent id."""
    return (
        -applicant.final_score,
        applicant.approved_hours,
        applicant.applied_at_utc,
        applicant.call_agent_id,
    )


def rank(applicants: Iterable[RankableApplicant]) -> list[RankableApplicant]:
    """The applicants best first, applying :func:`sort_key`."""
    return sorted(applicants, key=sort_key)
