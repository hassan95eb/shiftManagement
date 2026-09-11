# ---------------------------------------------------------------------------
# ShiftFlow API -- multi-stage build.
#
#   Stage `build`   -- restores and publishes ShiftFlow.Api in Release on the
#                      .NET 10 SDK image. Project files are copied and restored
#                      before the rest of the source so the restore layer is
#                      cached until a .csproj or a Directory.Build.props changes.
#   Stage `runtime` -- copies only the publish output onto the aspnet:10.0
#                      runtime image. No SDK, no source, no build artefacts --
#                      the "trimmed" image is the framework-dependent app on the
#                      runtime-only base, roughly a quarter the size of the SDK
#                      layer. (No IL trimming / PublishTrimmed: EF Core and the
#                      JWT stack rely on reflection.)
#
# Build context is the repo root (see docker-compose.yml) so this can COPY the
# whole `backend/` tree. Run:
#
#     docker compose up --build
#
# Local `dotnet run` is untouched -- this file is only read by `docker build`.
# ---------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the project graph first -- cached until a project file moves.
COPY backend/Directory.Build.props backend/ShiftFlow.sln ./backend/
COPY backend/src/Directory.Build.props ./backend/src/
COPY backend/src/ShiftFlow.Domain/ShiftFlow.Domain.csproj ./backend/src/ShiftFlow.Domain/
COPY backend/src/ShiftFlow.Application/ShiftFlow.Application.csproj ./backend/src/ShiftFlow.Application/
COPY backend/src/ShiftFlow.Infrastructure/ShiftFlow.Infrastructure.csproj ./backend/src/ShiftFlow.Infrastructure/
COPY backend/src/ShiftFlow.Api/ShiftFlow.Api.csproj ./backend/src/ShiftFlow.Api/
RUN dotnet restore backend/src/ShiftFlow.Api/ShiftFlow.Api.csproj

# Then the full source, and publish without a second restore.
COPY backend/ ./backend/
RUN dotnet publish backend/src/ShiftFlow.Api/ShiftFlow.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Kestrel serves plain HTTP inside the container; TLS terminates upstream
# (the compose port map now, the `web`/nginx service in a later phase). This
# overrides the base image's ASPNETCORE_HTTP_PORTS default and keeps the URL
# explicit. `dotnet run` locally still uses launchSettings.json (5023 / 7194).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

# aspnet:10.0 (Debian) runs as root unless told otherwise; $APP_UID is the
# unprivileged "app" account the image already provisions for this.
USER $APP_UID
ENTRYPOINT ["dotnet", "ShiftFlow.Api.dll"]
