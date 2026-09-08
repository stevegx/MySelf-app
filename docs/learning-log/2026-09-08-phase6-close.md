# 2026-09-08 — Phase 6 close: localization + operations

Branch `phase-6-polish-release`. Commits `d27c399`, `05b71d9`, `f69637e`.

Phase 6 is "Accessibility, localization, export/delete, rate limiting" +
"production config, monitoring and backups". The first three shipped earlier
(learning logs `…-export-and-delete-my-data.md`, `…-accessibility-pass.md`,
`…-rate-limit-audit.md`). These two slices finish it.

## Slice 4 — localization seam (`d27c399`)

Launch language is English (decision #2), so the deliverable is *make it
translatable*, not translate.

- `src/i18n/en.ts` — the catalogue, `as const` so its shape is a literal type.
- `src/i18n/index.ts` — `t((m) => m.nav.dashboard, vars)` + `useT()`. **Selector**
  based, not stringly-typed keys: `t` takes `(m: Messages) => string`, so a typo
  is a compile error and "find usages" works. `{token}` interpolation via
  `split().join()`. `const catalogue: Messages = en` is the single line a second
  language changes.
- `AppShell` migrated end to end as the worked example: nav labels (the array now
  holds `label: (m) => m.nav.workouts` selectors), both `aria-label="Primary"`
  landmarks, the theme toggle **including its interpolated aria-label**
  (`t(m => m.theme.toggle, { current, next })`), the skip link, the route
  announcer.

Why no `react-i18next`: CLAUDE.md — "add abstractions only when they solve a
concrete explained problem". There is no second language yet. This seam makes
adding one mechanical (drop `el.ts`, pick it from `navigator.language` /
`UserProfile.Locale`), and `react-i18next` is the documented upgrade path when
real translations, pluralization rules and lazy-loaded bundles actually matter.
Dates/numbers already go through `Intl` (`toLocaleDateString`, the export number
formats), so they localize for free. Extracting the remaining screen strings is
mechanical follow-up, not a blocker.

## Slice 5 — production config / monitoring / backups (`05b71d9`, `f69637e`)

Docker Compose is still deferred, so "production" here means: the API is safe to
point at a real database and host, and there's a way to observe it and get the
data back.

### Fail-fast config validation

`StartupChecks.ProductionConfigProblems(IConfiguration)` — a **pure** function
(hence unit-testable without a host) that returns a list of reasons a Production
start should abort:

- `Jwt:Key` shorter than 32 chars
- a `Cors:AllowedOrigins` / `Frontend:BaseUrl` entry still pointing at
  `localhost` / `127.0.0.1`
- `AllowedHosts` left at `"*"`
- `RateLimiting:Enabled` set to `false`

`Program.cs` runs it **before `builder.Build()`** when
`builder.Environment.IsProduction()` and throws with every problem listed at
once. Dev and Test are exempt on purpose — they *do* use localhost origins and
short keys.

`appsettings.Production.json` (new) drops logging to `Warning`, turns the
HttpLogging middleware category up to `Information`, and pins
`RateLimiting:Enabled=true`. No secrets — those stay in env vars.

### Monitoring

- **`AddHttpLogging` / `UseHttpLogging`** — framework built-in (no Serilog). One
  combined line per request: method, path, status, duration. The `Authorization`
  header isn't in the default field set. It logs at `Information`, so it's silent
  under the Dev/Test `Microsoft.AspNetCore: Warning` filter and on in Production
  via the appsettings above.
- **Health split**:
  - `GET /health/live` — `Predicate = _ => false` runs no checks; it answers
    "the process is up" for a container liveness probe.
  - `GET /health/ready` — `Predicate = c => c.Tags.Contains("ready")` runs only
    the PostgreSQL check (tagged `["ready"]` on `AddDbContextCheck`); it answers
    "can serve traffic" for a load balancer.
  - `GET /health` — all checks, kept as the manual-inspection alias.

New ASP.NET note: `MapHealthChecks(path, new HealthCheckOptions { Predicate = … })`
filters which registered checks that endpoint runs; tags on the checks are how
you group them.

### Backups

`scripts/db-backup.sh` — `pg_dump --format=custom` piped through `gzip` to
`./backups/<db>-<timestamp>.dump.gz` (git-ignored), then prunes to the newest
`KEEP` (default 14). `scripts/db-restore.sh` — `pg_restore --clean --if-exists`,
refuses to run without `CONFIRM=yes`. Both resolve the connection from
`$DATABASE_URL` (`postgres://…`) or by parsing
`ConnectionStrings__DefaultConnection` out of the repo-root `.env`.

`docs/09-operations.md` — the runbook: required env vars and what the Production
validator rejects, the three health probes, the log format, a backup cron line +
the restore command, and a 6-point deploy checklist.

Gotcha (`f69637e`): the two `.sh` files committed as `100644` with a CRLF warning.
On Linux a CRLF shebang line is `bad interpreter: /usr/bin/env bash^M`, and a
non-executable bit means `./script.sh` fails. Fixed with a `.gitattributes`
(`*.sh text eol=lf`) and `git update-index --chmod=+x`.

## Phase 6 is done

| Roadmap item | Where |
|---|---|
| Accessibility | `…-accessibility-pass.md` (slices 2a–2c) |
| Localization | this file, slice 4 |
| Export / delete | `…-export-and-delete-my-data.md` (slices 1a–1c) |
| Rate limiting | `…-rate-limit-audit.md` (slice 3) |
| Production config / monitoring / backups | this file, slice 5 |

Deferred and documented: full string extraction; a colour-contrast audit and a
`read` rate-limit policy for heavy analytics GETs (both wait on other decisions);
Docker Compose; real deploy target.

Full suite at close: **116 frontend, 60 backend unit, 154 backend integration.**
