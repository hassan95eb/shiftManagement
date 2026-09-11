"""Tests for the pure scoring functions.

The load-bearing test is :func:`test_reproduces_seeded_recommendation_rows`: it
feeds the same inputs the scenario seed used and asserts the score and the
``Reason`` string come out byte-identical to what
``backend/src/ShiftFlow.Infrastructure/Persistence/SeedData.cs`` (and
``database/03-seed.sql``) store. That is the proof the Python recommender and the
C# seed agree -- the recommender overwrites those rows on its first run.
"""

from decimal import Decimal

import pytest

from config import load_database_config, load_scoring_config
from scoring import (
    DEFAULT_CONFIG,
    RankableApplicant,
    ScoringConfig,
    rank,
    score,
    sort_key,
)


# --- the three seeded rows, verbatim from SeedData.cs / 03-seed.sql -------------
#
# Pool shift: retailPool, 2026-11-10 08:00-16:00 -> 8h. "Previous month" is
# 2026-10; "approved hours" are for 2026-11 (the shift's month).
#
#   ada : SeedData.cs L120-124,163,182,189-196  rating 2026-10 = 4.6;
#         approved for adaApprovedShift 2026-11-12 09:00-17:00 = 8h;
#         covering window 2026-11-10 06:00-22:00 = 16h.
#   nate: SeedData.cs L138-140,197-204          rating 2026-10 = 3.1;
#         no approved shift = 0h; window 2026-11-10 06:00-20:00 = 14h.
#   kite: SeedData.cs L142-144,205-212          no rating row = default 3.0;
#         no approved shift = 0h; window 2026-11-10 06:00-20:00 = 14h.

SEED_ROWS = {
    "ada": {
        "inputs": dict(
            previous_month_rating=Decimal("4.6"),
            approved_hours=Decimal("8"),
            shift_hours=Decimal("8"),
            covering_window_hours=Decimal("16"),
        ),
        "score": Decimal("76.10"),
        "reason": "Rating 4.6/5 -> 27.6 | Workload 8h -> 28.5 | Availability 8/16h -> 20.0 | Total 76.1",
    },
    "nate": {
        "inputs": dict(
            previous_month_rating=Decimal("3.1"),
            approved_hours=Decimal("0"),
            shift_hours=Decimal("8"),
            covering_window_hours=Decimal("14"),
        ),
        "score": Decimal("71.50"),
        "reason": "Rating 3.1/5 -> 18.6 | Workload 0h -> 30.0 | Availability 8/14h -> 22.9 | Total 71.5",
    },
    "kite": {
        "inputs": dict(
            previous_month_rating=None,
            approved_hours=Decimal("0"),
            shift_hours=Decimal("8"),
            covering_window_hours=Decimal("14"),
        ),
        "score": Decimal("70.90"),
        "reason": "Rating default 3.0/5 -> 18.0 | Workload 0h -> 30.0 | Availability 8/14h -> 22.9 | Total 70.9",
    },
}


@pytest.mark.parametrize("name", list(SEED_ROWS))
def test_reproduces_seeded_recommendation_rows(name):
    row = SEED_ROWS[name]
    result = score(**row["inputs"])  # default config == CLAUDE.md #5 weights

    assert result.reason == row["reason"]
    assert result.final_score == row["score"]
    # DECIMAL(5,2): the stored value always carries exactly two fractional digits.
    assert result.final_score.as_tuple().exponent == -2


def test_worked_example_ada_is_exactly_76_10():
    """The README worked example: 76.10, not "approximately"."""
    result = score(**SEED_ROWS["ada"]["inputs"])
    assert result.final_score == Decimal("76.10")
    assert result.rating_component == Decimal("27.6")
    assert result.workload_component == Decimal("28.5")
    assert result.availability_component == Decimal("20.0")


def test_total_is_sum_of_rounded_components_not_raw_final_score():
    """nate is the case where the two strategies diverge.

    Raw formula: (0.30*3.1/5 + 0.30*1 + 0.40*8/14) * 100 = 71.457142...
      -> rounded to 2 dp that is 71.46.
    Sum of the 1-dp components: 18.6 + 30.0 + 22.9 = 71.5 -> stored 71.50.
    The seed stores 71.50; this module must too.
    """
    result = score(**SEED_ROWS["nate"]["inputs"])

    raw_final = (
        DEFAULT_CONFIG.rating_weight * (Decimal("3.1") / 5)
        + DEFAULT_CONFIG.workload_weight * 1
        + DEFAULT_CONFIG.availability_weight * (Decimal("8") / Decimal("14"))
    ) * 100
    assert raw_final.quantize(Decimal("0.01")) == Decimal("71.46")

    assert result.final_score == Decimal("71.50")
    assert result.final_score != raw_final.quantize(Decimal("0.01"))


def test_missing_rating_uses_default_and_labels_it():
    with_row = score(
        previous_month_rating=Decimal("3.0"),
        approved_hours=Decimal("0"),
        shift_hours=Decimal("8"),
        covering_window_hours=Decimal("8"),
    )
    without_row = score(
        previous_month_rating=None,
        approved_hours=Decimal("0"),
        shift_hours=Decimal("8"),
        covering_window_hours=Decimal("8"),
    )

    # Same number either way -- the default is 3.0.
    assert without_row.rating_component == with_row.rating_component == Decimal("18.0")
    # ...but the reason makes the fallback visible.
    assert without_row.reason.startswith("Rating default 3.0/5 -> 18.0 |")
    assert with_row.reason.startswith("Rating 3.0/5 -> 18.0 |")


def test_custom_rating_default_is_honoured():
    cfg = ScoringConfig(rating_default=Decimal("2.5"))
    result = score(
        previous_month_rating=None,
        approved_hours=Decimal("0"),
        shift_hours=Decimal("8"),
        covering_window_hours=Decimal("8"),
        config=cfg,
    )
    # 0.30 * (2.5/5) * 100 = 15.0
    assert result.rating_component == Decimal("15.0")
    assert result.reason.startswith("Rating default 2.5/5 -> 15.0 |")


@pytest.mark.parametrize(
    "approved_hours, token",
    [
        (Decimal("160"), "Workload 160h -> 0.0"),   # exactly at the cap
        (Decimal("200"), "Workload 200h -> 0.0"),   # beyond the cap, clamped
        (Decimal("176.5"), "Workload 176.5h -> 0.0"),  # fractional, beyond cap
    ],
)
def test_call_agent_at_or_beyond_monthly_cap_scores_zero_workload(approved_hours, token):
    result = score(
        previous_month_rating=Decimal("3.0"),
        approved_hours=approved_hours,
        shift_hours=Decimal("8"),
        covering_window_hours=Decimal("8"),
    )
    assert result.workload_component == Decimal("0.0")
    assert token in result.reason


def test_exact_availability_match_versus_a_wide_window():
    exact = score(
        previous_month_rating=Decimal("3.0"),
        approved_hours=Decimal("0"),
        shift_hours=Decimal("8"),
        covering_window_hours=Decimal("8"),
    )
    wide = score(
        previous_month_rating=Decimal("3.0"),
        approved_hours=Decimal("0"),
        shift_hours=Decimal("8"),
        covering_window_hours=Decimal("16"),
    )

    assert exact.availability_component == Decimal("40.0")
    assert "Availability 8/8h -> 40.0" in exact.reason
    assert wide.availability_component == Decimal("20.0")
    assert "Availability 8/16h -> 20.0" in wide.reason
    assert exact.final_score > wide.final_score


def test_rounding_is_half_up_not_bankers():
    """0.40 * (1/32) * 100 = 1.25 exactly. Half up -> 1.3; bankers' -> 1.2."""
    result = score(
        previous_month_rating=Decimal("3.0"),
        approved_hours=Decimal("0"),
        shift_hours=Decimal("1"),
        covering_window_hours=Decimal("32"),
    )
    assert result.availability_component == Decimal("1.3")
    assert "Availability 1/32h -> 1.3" in result.reason


# --- tie-break -----------------------------------------------------------------


def _applicant(call_agent_id, score_, hours, applied_at):
    return RankableApplicant(
        call_agent_id=call_agent_id,
        final_score=Decimal(score_),
        approved_hours=Decimal(hours),
        applied_at_utc=applied_at,
    )


def test_tie_break_orders_by_score_then_hours_then_time_then_id():
    a = _applicant(1, "80.00", "40", "2026-11-01T09:00:00")   # top score
    b = _applicant(2, "75.00", "10", "2026-11-01T09:00:00")   # same score as c/d/e, fewest hours
    c = _applicant(3, "75.00", "20", "2026-11-01T08:00:00")   # more hours than b; earliest time in its hours-group
    d = _applicant(4, "75.00", "20", "2026-11-01T10:00:00")   # ties c on score+hours; later time
    e = _applicant(5, "75.00", "20", "2026-11-01T10:00:00")   # ties d entirely; higher id -> last
    f = _applicant(6, "60.00", "0", "2026-10-01T00:00:00")    # lowest score

    ordered = rank([e, d, c, b, a, f])

    assert [x.call_agent_id for x in ordered] == [1, 2, 3, 4, 5, 6]


def test_sort_key_is_usable_directly_with_sorted():
    items = [
        _applicant(9, "70.0", "5", "2026-11-02T00:00:00"),
        _applicant(8, "70.0", "5", "2026-11-01T00:00:00"),
        _applicant(7, "90.0", "99", "2026-11-09T00:00:00"),
    ]
    assert [x.call_agent_id for x in sorted(items, key=sort_key)] == [7, 8, 9]


def test_tie_break_fewer_hours_wins_before_time():
    later_but_lighter = _applicant(1, "50.0", "4", "2026-11-05T00:00:00")
    earlier_but_heavier = _applicant(2, "50.0", "9", "2026-11-01T00:00:00")
    assert [x.call_agent_id for x in rank([earlier_but_heavier, later_but_lighter])] == [1, 2]


# --- config ------------------------------------------------------------------


def test_scoring_config_defaults_match_claude_md_section_5():
    cfg = load_scoring_config({})
    assert cfg.rating_weight == Decimal("0.30")
    assert cfg.workload_weight == Decimal("0.30")
    assert cfg.availability_weight == Decimal("0.40")
    assert cfg.monthly_cap == Decimal("160")
    assert cfg.rating_default == Decimal("3.0")
    assert cfg == DEFAULT_CONFIG


def test_scoring_config_reads_env_and_leaves_the_rest_default():
    cfg = load_scoring_config(
        {"SCORING__MONTHLYCAP": "200", "SCORING__RATINGDEFAULT": "2.0", "SCORING__RATINGWEIGHT": " 0.25 "}
    )
    assert cfg.monthly_cap == Decimal("200")
    assert cfg.rating_default == Decimal("2.0")
    assert cfg.rating_weight == Decimal("0.25")  # whitespace tolerated
    assert cfg.workload_weight == Decimal("0.30")  # untouched
    assert cfg.availability_weight == Decimal("0.40")


def test_blank_env_var_falls_back_to_default():
    cfg = load_scoring_config({"SCORING__MONTHLYCAP": "   "})
    assert cfg.monthly_cap == Decimal("160")


def test_env_config_feeds_score():
    cfg = load_scoring_config({"SCORING__MONTHLYCAP": "80"})
    # 40h of 80 -> workload ratio 0.5 -> 0.30 * 0.5 * 100 = 15.0
    result = score(
        previous_month_rating=Decimal("3.0"),
        approved_hours=Decimal("40"),
        shift_hours=Decimal("8"),
        covering_window_hours=Decimal("8"),
        config=cfg,
    )
    assert result.workload_component == Decimal("15.0")


def test_database_config_defaults_and_overrides():
    default = load_database_config({})
    assert (default.host, default.port, default.database, default.user, default.password) == (
        "localhost",
        1433,
        "ShiftFlow",
        "sa",
        None,
    )

    overridden = load_database_config(
        {"MSSQL_HOST": "db", "MSSQL_PORT": "1444", "MSSQL_DB": "sf", "MSSQL_SA_USER": "svc", "MSSQL_SA_PASSWORD": "p@ss"}
    )
    assert (overridden.host, overridden.port, overridden.database, overridden.user, overridden.password) == (
        "db",
        1444,
        "sf",
        "svc",
        "p@ss",
    )
