# 2026-09-08 — Rate-limit gap audit (Phase 6, slice 3)

Branch `phase-6-polish-release`. Commit `f9060ac`.

`docs/08` Phase 6 lists "rate limiting". It was mostly built already
(`RateLimiting.cs`): one generous **global** limiter (300/window, partitioned per
user id or per IP), an **auth** policy (20/window per IP) on the whole
`/api/v1/auth` group, and a **write** policy (90/window per user) applied to
mutations. This slice audited every endpoint and closed the gaps.

## How the audit was done

`grep` for every `MapGet/MapPost/MapPut/MapDelete` and, for each, whether the
route (or its `MapGroup`) carries `.RequireRateLimiting(...)`. Then classify:

- **Write with no `write` policy** → gap (only the 300 global stands between a
  script and a DB-write loop).
- **Anonymous + does real work** (external call, heavy compute) → gap.
- **Read on the global limit** → judgement call, not necessarily a gap.

## Gaps found and fixed

| Endpoint | Was | Now |
|---|---|---|
| `GET /api/v1/foods/barcode/{code}` | anonymous, global only — **each miss is an outbound Open Food Facts call** | new **`lookup`** policy: per-caller, 30/window |
| `PUT /api/v1/me/profile` | `RequireAuthorization` only | + `write` policy |
| `PUT /api/v1/me/preferences` | `RequireAuthorization` only | + `write` policy |
| `POST /api/v1/me/onboarding/complete` | `RequireAuthorization` only | + `write` policy |
| `POST /api/v1/me/nutrition-estimate` | `RequireAuthorization` only (pure compute) | + `write` policy |

The barcode one is the real find: an unauthenticated endpoint that makes the
server call a third party. The new `lookup` policy is tighter than `write` (30 vs
90) precisely because it is anonymous and each request can cost an outbound HTTP
round-trip. Its partition key is `CallerKey` — user id when a token is present,
IP otherwise — so it degrades sensibly whether or not the caller authenticates.
(Requiring auth on the barcode endpoint outright is a bigger, separate change —
the existing anonymous `BarcodeEndpointTests` would all need auth — so it's noted,
not done.)

## Deliberately not changed

- The workout route groups (`programs`, `workout-days`, `workout-sessions`) apply
  the `write` policy at the **group** level, so their `GET`s are write-limited
  too. That's stricter than necessary (browsing your programs spends write
  budget) but it is not a security gap, and splitting the groups risks
  regressions.
- `GET /analytics/strength`, `GET /analytics/nutrition`,
  `GET /exercises/{id}/history` are CPU-heavy reads sitting on the 300 global
  only. A dedicated `read` policy would be defense-in-depth, but the right limit
  is a capacity decision — left as a follow-up.

## New .NET / ASP.NET notes

- `limiter.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(key, …))`
  registers a named policy; a route opts in with `.RequireRateLimiting(name)`.
- The rate-limit **middleware runs before endpoint execution**, so a request that
  the handler would reject with 400/409 still counts against the budget and still
  gets a 429 once the budget is spent. The `Lookup_policy_…` test leans on this:
  it spams `/foods/barcode/123` (fails the 8–14-digit format check → 400, no OFF
  call) and still sees 429s after the budget.
- `WebApplicationFactory` config override for a focused test:
  `builder.UseSetting("RateLimiting:LookupPermitLimit", "2")` +
  `ConfigureAppConfiguration(… AddInMemoryCollection(…))` — the rest of the suite
  keeps `RateLimiting:Enabled=false`.
