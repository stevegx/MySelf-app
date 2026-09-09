# MySelf App — 09 Operations

> Purpose: how to configure, observe and back up a deployed instance. Phase 6
> ("production config, monitoring and backups"). Docker Compose is still deferred
> (see `docs/04`); today PostgreSQL runs directly on the host.

## Environments

`ASPNETCORE_ENVIRONMENT` selects the config layer: `Development` (default when
running from `dotnet run`), `Production`, or `Test` (the integration suite).
`appsettings.json` holds non-secret defaults; `appsettings.Production.json`
tightens logging and pins `RateLimiting:Enabled=true`; secrets and host-specific
values come from environment variables only.

### Required configuration

| Key (env var form) | Notes |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Npgsql connection string. Checked at startup in every environment. |
| `Jwt__Key` | Access-token signing secret. Checked at startup; **≥ 32 chars in Production**. `openssl rand -base64 48`. |
| `Cors__AllowedOrigins__0` (…`__1`, …) | Deployed frontend origin(s). In Production must not be `localhost`/`127.0.0.1`. |
| `AllowedHosts` | The API's host name(s). In Production must not be `*`. |
| `Frontend__BaseUrl` | Used to build password-reset links. In Production must not be `localhost`. |

### Fail-fast validation

In **Production** the app runs `StartupChecks.ProductionConfigProblems` before
building the host and refuses to start if any dev-only value is still in place
(short signing key, localhost CORS/frontend origin, `AllowedHosts = "*"`, rate
limiting disabled). The exception message lists every problem at once.
Development and Test are exempt on purpose.

## Health probes

| Route | Runs | Use for |
| --- | --- | --- |
| `GET /health/live` | nothing | container liveness — "the process is up" |
| `GET /health/ready` | the `ready`-tagged checks (PostgreSQL reachability) | load-balancer readiness — "it can serve traffic" |
| `GET /health` | every check | manual inspection |

All are unversioned and anonymous.

## Logging & monitoring

`AddHttpLogging` emits one structured line per request (method, path, status,
duration; the `Authorization` header is not in the field set). It is at
`Information` level, so:

- **Development / Test** — suppressed (`Microsoft.AspNetCore` is at `Warning`).
- **Production** — enabled via
  `Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware: Information` in
  `appsettings.Production.json`.

Errors flow through `AddProblemDetails` as RFC 7807 responses; unhandled
exceptions are logged by the framework with the request path.

Rate-limit rejections return `429` with a `Retry-After` header and a
ProblemDetails-shaped body (see `RateLimiting.cs`).

## Database backups

`scripts/db-backup.sh` writes a timestamped, gzipped `pg_dump` custom-format
archive to `./backups/` (git-ignored) and prunes to the newest `KEEP` (default
14). It resolves the connection from `$DATABASE_URL` (`postgres://…`) or, failing
that, from `ConnectionStrings__DefaultConnection` in the repo-root `.env`.

```bash
./scripts/db-backup.sh                 # -> ./backups/myself-YYYYMMDD-HHMMSS.dump.gz
KEEP=30 ./scripts/db-backup.sh /var/backups/myself
```

Schedule it with cron, e.g. hourly:

```cron
0 * * * * cd /srv/myself && /srv/myself/scripts/db-backup.sh /var/backups/myself >> /var/log/myself-backup.log 2>&1
```

### Restore

`scripts/db-restore.sh` drops and recreates every object in the target database
from a dump. It refuses to run without `CONFIRM=yes`.

```bash
CONFIRM=yes ./scripts/db-restore.sh ./backups/myself-20260908-120000.dump.gz
```

After a restore, run `dotnet ef database update` if the dump predates the current
migration set.

## Deploy checklist

1. Set every variable in the table above (real hosts, a fresh `Jwt__Key`).
2. `ASPNETCORE_ENVIRONMENT=Production`.
3. `dotnet ef database update` against the target database.
4. Start the API; confirm it did **not** throw a "configuration is not
   deployment-ready" error.
5. `GET /health/ready` returns `200`.
6. A backup cron entry exists and its first run produced a file.
