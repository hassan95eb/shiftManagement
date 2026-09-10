"""Configuration for the recommender, resolved from the environment.

No database work happens here -- this module only reads values. It is imported
by ``recommendation.py`` (the entry point, a later phase) and by the tests.

The scoring weights, ``MonthlyCap`` and the rating default use the SAME env var
names the .NET API is meant to bind: ASP.NET maps ``Scoring:RatingWeight`` to
``SCORING__RATINGWEIGHT``, so one ``.env`` feeds both sides and the two scoring
implementations cannot silently diverge (docs/02 #5).

NOTE: as of this phase the backend has no ``ScoringOptions`` binding, so the
shared ``SCORING__*`` names are aspirational on the C# side. The defaults below
are the CLAUDE.md #5 values, which is what the backend currently hard-codes into
the seed.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from decimal import Decimal
from typing import Mapping, Optional

from scoring import ScoringConfig

_SCORING_DEFAULTS: dict[str, str] = {
    "SCORING__RATINGWEIGHT": "0.30",
    "SCORING__WORKLOADWEIGHT": "0.30",
    "SCORING__AVAILABILITYWEIGHT": "0.40",
    "SCORING__MONTHLYCAP": "160",
    "SCORING__RATINGDEFAULT": "3.0",
}

# Database connection parts. pymssql takes host/port/user/password/database
# directly, so there is no ADO.NET-style connection string to parse. These reuse
# the names docker-compose already defines (MSSQL_SA_PASSWORD, MSSQL_DB,
# MSSQL_PORT); MSSQL_HOST and MSSQL_SA_USER are read here with the same defaults
# the backend's .env.example uses (Server=localhost,1433; User Id=sa).
_DB_DEFAULTS: dict[str, str] = {
    "MSSQL_HOST": "localhost",
    "MSSQL_PORT": "1433",
    "MSSQL_DB": "ShiftFlow",
    "MSSQL_SA_USER": "sa",
}


def _resolve(src: Mapping[str, str], name: str, defaults: Mapping[str, str]) -> Optional[str]:
    raw = src.get(name)
    if raw is None or raw.strip() == "":
        return defaults.get(name)
    return raw.strip()


def load_scoring_config(env: Optional[Mapping[str, str]] = None) -> ScoringConfig:
    """Build a :class:`ScoringConfig` from ``env`` (defaults to ``os.environ``).
    A missing or blank variable falls back to the CLAUDE.md #5 default."""
    src = os.environ if env is None else env

    def dec(name: str) -> Decimal:
        return Decimal(_resolve(src, name, _SCORING_DEFAULTS))

    return ScoringConfig(
        rating_weight=dec("SCORING__RATINGWEIGHT"),
        workload_weight=dec("SCORING__WORKLOADWEIGHT"),
        availability_weight=dec("SCORING__AVAILABILITYWEIGHT"),
        monthly_cap=dec("SCORING__MONTHLYCAP"),
        rating_default=dec("SCORING__RATINGDEFAULT"),
    )


@dataclass(frozen=True)
class DatabaseConfig:
    """Connection parts for ``pymssql`` (used from ``db.py`` in a later phase)."""

    host: str
    port: int
    database: str
    user: str
    password: Optional[str]


def load_database_config(env: Optional[Mapping[str, str]] = None) -> DatabaseConfig:
    """Read the ``MSSQL_*`` connection parts from ``env`` (defaults to
    ``os.environ``). ``MSSQL_SA_PASSWORD`` has no default -- it is ``None`` when
    unset, and connecting is a later phase's concern."""
    src = os.environ if env is None else env
    return DatabaseConfig(
        host=_resolve(src, "MSSQL_HOST", _DB_DEFAULTS),
        port=int(_resolve(src, "MSSQL_PORT", _DB_DEFAULTS)),
        database=_resolve(src, "MSSQL_DB", _DB_DEFAULTS),
        user=_resolve(src, "MSSQL_SA_USER", _DB_DEFAULTS),
        password=(src.get("MSSQL_SA_PASSWORD") or None),
    )
