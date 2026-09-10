"""Entry point for the ShiftFlow applicant recommender.

Run it with the database reachable and the ``MSSQL_*`` / ``SCORING__*`` variables
set (``.env`` at the repo root is read automatically for a local run; Docker
passes them through Compose)::

    python recommendation.py

For every ``Open`` shift it ranks that shift's *applicants* -- not every expert
in the system. For each applicant who would still pass the apply rules
(CLAUDE.md #5: project membership, the whole shift inside one availability
window, no overlap with an already-approved shift) it gathers the three formula
inputs, calls :func:`scoring.score`, and the results are written with one
idempotent ``MERGE`` per shift.

An applicant with **no covering availability window** cannot be scored --
``AvailabilityScore`` is ``shift / window`` and the divisor is undefined. Such a
row is *skipped* (never scored), and any recommendation a previous run wrote for
that pair is *removed*: the ``MERGE`` only keeps rows for applicants scored this
run, so a now-unscorable applicant falls out through its
``WHEN NOT MATCHED BY SOURCE`` arm. The same is true for an applicant who has
lost project membership or picked up an overlapping approved shift.

Exit code is ``0`` on success, ``1`` on a connection or SQL failure.
"""

from __future__ import annotations

import os
import sys
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path
from typing import Optional

from config import load_database_config, load_scoring_config
from db import MergeStats, RecommenderDb, ScoredRow, hours_between, month_bounds
from scoring import score

_REPO_ROOT = Path(__file__).resolve().parent.parent


def _load_dotenv() -> Optional[Path]:
    """Populate ``os.environ`` from the first ``.env`` found (CWD, then repo
    root). Real environment variables are left untouched -- Compose and an
    explicit ``export`` still win. Returns the file used, if any."""
    for candidate in (Path(".env"), _REPO_ROOT / ".env"):
        if not candidate.is_file():
            continue
        for raw in candidate.read_text(encoding="utf-8").splitlines():
            line = raw.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, _, value = line.partition("=")
            os.environ.setdefault(key.strip(), value.strip().strip('"').strip("'"))
        return candidate
    return None


def _overlaps_any_approved(
    spans: list[tuple[datetime, datetime]], shift_start: datetime, shift_end: datetime
) -> bool:
    """Apply rule 5, half-open: ``existing.Start < new.End AND existing.End >
    new.Start``. Back-to-back shifts (10-14, 14-18) do not overlap."""
    return any(start < shift_end and end > shift_start for start, end in spans)


def _approved_hours_in_month(
    spans: list[tuple[datetime, datetime]], month_start: datetime, month_end: datetime
) -> Decimal:
    """Total duration of the approved shifts whose ``StartUtc`` falls in the
    given month (CLAUDE.md #5: the month of ``Shift.StartUtc``, not 'now')."""
    total = Decimal(0)
    for start, end in spans:
        if month_start <= start < month_end:
            total += hours_between(start, end)
    return total


def run(db: RecommenderDb, now: datetime) -> int:
    """Score every Open shift's applicants and MERGE the results. Returns the
    process exit code."""
    scoring_config = load_scoring_config()
    computed_at = now.replace(microsecond=0)

    shifts = db.open_shifts()
    print(f"{len(shifts)} open shift(s) to rank.")

    grand = MergeStats()
    for shift in shifts:
        month_start, month_end = month_bounds(shift.start_utc)
        scored: list[ScoredRow] = []
        skipped: list[tuple[int, str]] = []

        for expert_id in db.pending_applicant_expert_ids(shift.id):
            if not db.is_project_member(expert_id, shift.id):
                skipped.append((expert_id, "not a member of the shift's project"))
                continue

            window_hours = db.covering_window_hours(expert_id, shift)
            if window_hours is None:
                skipped.append((expert_id, "no availability window covers the shift"))
                continue

            spans = db.approved_shift_spans(expert_id)
            if _overlaps_any_approved(spans, shift.start_utc, shift.end_utc):
                skipped.append((expert_id, "overlaps an already-approved shift"))
                continue

            breakdown = score(
                previous_month_rating=db.previous_month_rating(expert_id, shift),
                approved_hours=_approved_hours_in_month(spans, month_start, month_end),
                shift_hours=shift.hours,
                covering_window_hours=window_hours,
                config=scoring_config,
            )
            scored.append(
                ScoredRow(shift.id, expert_id, breakdown.final_score, breakdown.reason)
            )

        stats = db.merge_shift(shift.id, scored, computed_at)
        grand = MergeStats(
            grand.inserted + stats.inserted,
            grand.updated + stats.updated,
            grand.deleted + stats.deleted,
        )

        ranked = sorted(scored, key=lambda r: r.score, reverse=True)
        detail = ", ".join(f"expert {r.expert_id} {r.score}" for r in ranked) or "none"
        print(
            f"  shift {shift.id} ({shift.start_utc:%Y-%m-%d %H:%M}-{shift.end_utc:%H:%M}): "
            f"scored {len(scored)} [{detail}]; "
            f"+{stats.inserted} ~{stats.updated} -{stats.deleted}"
        )
        for expert_id, why in skipped:
            print(f"      skipped expert {expert_id}: {why}")

    print(
        f"Done. Recommendations inserted {grand.inserted}, updated {grand.updated}, "
        f"removed {grand.deleted}."
    )
    return 0


def main() -> int:
    env_file = _load_dotenv()
    if env_file is not None:
        print(f"Loaded environment from {env_file}.")

    db_config = load_database_config()
    print(
        f"Connecting to {db_config.host}:{db_config.port}/{db_config.database} "
        f"as {db_config.user}."
    )
    try:
        with RecommenderDb.connect(db_config) as db:
            return run(db, datetime.now(timezone.utc))
    except Exception as exc:  # noqa: BLE001 -- top-level guard: report and exit 1
        print(f"recommender failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
