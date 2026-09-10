"""Database I/O for the ShiftFlow applicant recommender.

``scoring.py`` is pure -- no I/O, no third-party imports. Everything that talks
to SQL Server lives here:

* :meth:`RecommenderDb.connect` opens a ``pymssql`` connection from the parts
  :func:`config.load_database_config` resolves from the environment;
* the read methods gather, per Open shift and per applicant, exactly the three
  inputs the CLAUDE.md #5 formula needs -- the rating for the month *before* the
  shift's month, the approved hours *in* the shift's month, and the availability
  window that covers the shift;
* :meth:`RecommenderDb.merge_shift` writes the results back with a single
  idempotent ``MERGE`` keyed on ``(ShiftId, ExpertId)`` (docs/01 #3-10).

``Recommendations`` is the only table ever written (docs/01 #4). The ``MERGE``
also carries a ``WHEN NOT MATCHED BY SOURCE`` arm so a row whose applicant can no
longer be scored -- they withdrew their covering availability window, lost
project membership, or picked up an overlapping approved shift -- is removed in
the same statement rather than left behind as a stale score.
"""

from __future__ import annotations

import contextlib
from dataclasses import dataclass
from datetime import datetime
from decimal import Decimal
from typing import Iterator, Optional, Sequence

import pymssql

from config import DatabaseConfig

__all__ = [
    "OpenShift",
    "ScoredRow",
    "MergeStats",
    "RecommenderDb",
    "hours_between",
    "month_bounds",
    "previous_period",
]

_SECONDS_PER_HOUR = Decimal(3600)


# --- value objects -----------------------------------------------------------


@dataclass(frozen=True)
class OpenShift:
    """One row of ``Shifts`` with ``Status = 'Open'``."""

    id: int
    start_utc: datetime
    end_utc: datetime

    @property
    def hours(self) -> Decimal:
        return hours_between(self.start_utc, self.end_utc)


@dataclass(frozen=True)
class ScoredRow:
    """A scored applicant, ready to be written to ``Recommendations``."""

    shift_id: int
    expert_id: int
    score: Decimal
    reason: str


@dataclass(frozen=True)
class MergeStats:
    """What one :meth:`RecommenderDb.merge_shift` call changed."""

    inserted: int = 0
    updated: int = 0
    deleted: int = 0

    @property
    def unchanged_is_possible(self) -> bool:
        return self.inserted == 0 and self.updated == 0 and self.deleted == 0


# --- small date helpers (pure; handy to unit-test without a database) --------


def hours_between(start: datetime, end: datetime) -> Decimal:
    """Whole-second span between two ``datetime2(0)`` values, in hours, as an
    exact :class:`~decimal.Decimal`."""
    return Decimal(int((end - start).total_seconds())) / _SECONDS_PER_HOUR


def month_bounds(moment: datetime) -> tuple[datetime, datetime]:
    """``[first of moment's month, first of the next month)`` -- the half-open
    range CLAUDE.md #5 counts approved hours over."""
    start = moment.replace(day=1, hour=0, minute=0, second=0, microsecond=0)
    end = (
        start.replace(year=start.year + 1, month=1)
        if start.month == 12
        else start.replace(month=start.month + 1)
    )
    return start, end


def previous_period(moment: datetime) -> str:
    """The ``ExpertRatings.Period`` string (``YYYY-MM``) for the month *before*
    ``moment``'s month."""
    year, month = moment.year, moment.month
    if month == 1:
        year, month = year - 1, 12
    else:
        month -= 1
    return f"{year:04d}-{month:02d}"


# --- the database ----------------------------------------------------------


class RecommenderDb:
    """A thin wrapper over a ``pymssql`` connection. One instance per run."""

    def __init__(self, connection: "pymssql.Connection") -> None:
        self._conn = connection

    @classmethod
    @contextlib.contextmanager
    def connect(cls, config: DatabaseConfig) -> Iterator["RecommenderDb"]:
        """Open a connection from ``config`` and close it on exit.

        ``MSSQL_SA_PASSWORD`` has no default (``config.py``); without it there is
        nothing to connect with, so fail loudly rather than send an empty
        password.
        """
        if not config.password:
            raise RuntimeError(
                "MSSQL_SA_PASSWORD is not set -- the recommender has no password "
                "to connect with. Set it in the environment or .env."
            )
        conn = pymssql.connect(
            server=config.host,
            port=config.port,
            user=config.user,
            password=config.password,
            database=config.database,
        )
        try:
            yield cls(conn)
        finally:
            conn.close()

    # --- reads ---------------------------------------------------------------

    def open_shifts(self) -> list[OpenShift]:
        rows = self._query(
            "SELECT Id, StartUtc, EndUtc FROM Shifts WHERE Status = 'Open' ORDER BY Id"
        )
        return [OpenShift(r["Id"], r["StartUtc"], r["EndUtc"]) for r in rows]

    def pending_applicant_expert_ids(self, shift_id: int) -> list[int]:
        """Experts with a ``Pending`` application to this shift, in
        ``AppliedAtUtc`` order.

        Only ``Pending`` -- a ``Rejected`` applicant has been turned down, and an
        ``Approved`` one would have closed the shift. These are the applicants
        still in contention, i.e. the ones whose application would still pass the
        apply rules.
        """
        rows = self._query(
            """
            SELECT ExpertId
            FROM ShiftApplications
            WHERE ShiftId = %s AND Status = 'Pending'
            ORDER BY AppliedAtUtc, Id
            """,
            (shift_id,),
        )
        return [r["ExpertId"] for r in rows]

    def is_project_member(self, expert_id: int, shift_id: int) -> bool:
        """Apply rule 2: the expert is assigned to the shift's project."""
        rows = self._query(
            """
            SELECT 1 AS Ok
            FROM Shifts s
            JOIN ExpertProjects ep ON ep.ProjectId = s.ProjectId
            WHERE s.Id = %s AND ep.ExpertId = %s
            """,
            (shift_id, expert_id),
        )
        return bool(rows)

    def covering_window_hours(self, expert_id: int, shift: OpenShift) -> Optional[Decimal]:
        """Apply rule 3: the duration of the availability window that contains
        the whole shift, or ``None`` if the expert has none.

        Phase 7 merges overlapping and adjacent windows on write, so at most one
        window can contain a given shift. The ``TOP 1 ... ORDER BY`` is a
        deterministic guard for the case the data somehow holds more: the
        narrowest covering window wins.
        """
        rows = self._query(
            """
            SELECT TOP 1 StartUtc, EndUtc
            FROM Availabilities
            WHERE ExpertId = %s AND StartUtc <= %s AND EndUtc >= %s
            ORDER BY DATEDIFF(SECOND, StartUtc, EndUtc), Id
            """,
            (expert_id, shift.start_utc, shift.end_utc),
        )
        if not rows:
            return None
        return hours_between(rows[0]["StartUtc"], rows[0]["EndUtc"])

    def previous_month_rating(self, expert_id: int, shift: OpenShift) -> Optional[Decimal]:
        """``ExpertRatings.Score`` for the month before the shift's month, or
        ``None`` when the expert has no row (``scoring.py`` then applies the
        configured default)."""
        rows = self._query(
            "SELECT Score FROM ExpertRatings WHERE ExpertId = %s AND Period = %s",
            (expert_id, previous_period(shift.start_utc)),
        )
        return rows[0]["Score"] if rows else None

    def approved_shift_spans(self, expert_id: int) -> list[tuple[datetime, datetime]]:
        """Every ``(StartUtc, EndUtc)`` the expert is approved for.

        Summing durations has no provider-agnostic SQL form, and the same set
        answers apply rule 5, so both are computed in Python from this one read
        (the mirror of ``RecommendationService`` on the C# side). An expert has
        few approved shifts, so the set stays small.
        """
        rows = self._query(
            """
            SELECT s.StartUtc, s.EndUtc
            FROM ShiftApplications a
            JOIN Shifts s ON s.Id = a.ShiftId
            WHERE a.ExpertId = %s AND a.Status = 'Approved'
            """,
            (expert_id,),
        )
        return [(r["StartUtc"], r["EndUtc"]) for r in rows]

    # --- write -------------------------------------------------------------

    def merge_shift(
        self,
        shift_id: int,
        rows: Sequence[ScoredRow],
        computed_at: datetime,
    ) -> MergeStats:
        """Reconcile ``Recommendations`` for one shift with ``rows``.

        * a ``(ShiftId, ExpertId)`` in ``rows`` but not in the table -> INSERT;
        * one in both, with a different ``Score`` or ``Reason`` -> UPDATE (and
          ``ComputedAtUtc`` is bumped);
        * one in both and unchanged -> left exactly as it is, so a re-run with no
          state change touches nothing;
        * one in the table for this shift but not in ``rows`` -> DELETE (the
          applicant can no longer be scored, or is no longer an applicant).

        Committed per shift.
        """
        cur = self._conn.cursor()

        if not rows:
            # No table-value constructor can have zero rows; a plain delete of
            # this shift's rows is the same reconciliation.
            cur.execute("DELETE FROM Recommendations WHERE ShiftId = %s", (shift_id,))
            deleted = cur.rowcount if cur.rowcount and cur.rowcount > 0 else 0
            self._conn.commit()
            return MergeStats(deleted=deleted)

        values = ",\n                ".join(["(%s, %s, %s, %s, %s)"] * len(rows))
        params: list[object] = []
        for r in rows:
            params += [r.shift_id, r.expert_id, r.score, r.reason, computed_at]
        params.append(shift_id)

        sql = f"""
            MERGE INTO Recommendations WITH (HOLDLOCK) AS tgt
            USING (VALUES
                {values}
            ) AS src (ShiftId, ExpertId, Score, Reason, ComputedAtUtc)
                ON tgt.ShiftId = src.ShiftId AND tgt.ExpertId = src.ExpertId
            WHEN MATCHED AND (tgt.Score <> src.Score OR tgt.Reason <> src.Reason) THEN
                UPDATE SET Score = src.Score,
                           Reason = src.Reason,
                           ComputedAtUtc = src.ComputedAtUtc
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (ShiftId, ExpertId, Score, Reason, ComputedAtUtc)
                VALUES (src.ShiftId, src.ExpertId, src.Score, src.Reason, src.ComputedAtUtc)
            WHEN NOT MATCHED BY SOURCE AND tgt.ShiftId = %s THEN
                DELETE
            OUTPUT $action AS Action;
        """
        cur.execute(sql, tuple(params))
        actions = [row[0] for row in cur.fetchall()]
        self._conn.commit()
        return MergeStats(
            inserted=actions.count("INSERT"),
            updated=actions.count("UPDATE"),
            deleted=actions.count("DELETE"),
        )

    # --- internals -------------------------------------------------------

    def _query(self, sql: str, params: tuple = ()) -> list[dict]:
        cur = self._conn.cursor(as_dict=True)
        try:
            cur.execute(sql, params)
            return cur.fetchall()
        finally:
            cur.close()
