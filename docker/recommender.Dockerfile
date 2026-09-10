# ---------------------------------------------------------------------------
# ShiftFlow applicant recommender -- one-shot job.
#
# NOT a long-running service. It scores every Open shift's applicants once and
# exits. Compose keeps it behind the `recommender` profile so `docker compose
# up` never starts it; run it on demand (CLAUDE.md #11):
#
#     docker compose run --rm recommender
#
# Build context is the repo root (see docker-compose.yml) so this can COPY
# python/.
# ---------------------------------------------------------------------------
FROM python:3.12-slim

WORKDIR /app

# pymssql publishes manylinux wheels with FreeTDS bundled, so no gcc/freetds-dev
# layer is needed on -slim. Copy just the requirements first for layer caching.
COPY python/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

COPY python/ ./

# No .env inside the image -- Compose passes MSSQL_* / SCORING__* through the
# environment. config.py falls back to the CLAUDE.md #5 defaults for anything
# unset except the SA password, which has no default.
ENTRYPOINT ["python", "recommendation.py"]
