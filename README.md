# MySelf App

Workout planning, logging, nutrition and progress in one place. Portfolio product + .NET learning project.

- **Frontend:** React 19 + TypeScript + Vite + Tailwind v4, React Router, TanStack Query
- **Backend:** .NET 10 ASP.NET Core Web API, EF Core 10, PostgreSQL, modular monolith
- **API:** REST under `/api/v1`

See `docs/` for the full product + engineering spec (`docs/01`…`docs/08`) and `CLAUDE.md` for the working contract.

## Prerequisites

- .NET SDK **10.x**
- Node **22+**
- PostgreSQL **17** running locally on `5432` (Docker Compose is deferred — see `docs/04`)

## First-time setup

**1. Database** — create the role + database the app expects:

```bash
psql -U postgres -h localhost \
  -c "CREATE ROLE myself WITH LOGIN PASSWORD 'myself_local_change_me';" \
  -c "CREATE DATABASE myself OWNER myself;"
```

**2. Environment** — copy the template and adjust if your password differs:

```bash
cp .env.example .env
```

`.env` holds `ConnectionStrings__DefaultConnection` (read by the API and the tools). It is git-ignored.

**3. Backend** — apply migrations, then seed the exercise catalogue:

```bash
cd backend
dotnet tool restore
dotnet ef database update --project MySelf.Infrastructure --startup-project MySelf.Api
dotnet run --project MySelf.Tools.WgerImport -- import   # loads seed-data/wger-catalogue.json (~862 exercises)
```

**4. Frontend:**

```bash
cd frontend
npm install
```

## Run

```bash
# API — http://localhost:5242  (health: /health,  OpenAPI: /openapi/v1.json)
dotnet run --project backend/MySelf.Api

# Frontend — http://localhost:5173
cd frontend && npm run dev
```

Quick check: `curl http://localhost:5242/api/v1/foods/barcode/3017620422003` (Nutella, via Open Food Facts).

## Test

```bash
dotnet test backend/MySelf.sln     # requires the local PostgreSQL to be running
cd frontend && npm run test
cd frontend && npm run lint
```

## Data sources

| Data | Source | How |
|---|---|---|
| Exercise catalogue | [wger](https://wger.de) (CC-BY-SA) | Seeded once from `backend/seed-data/wger-catalogue.json`; regenerate with `dotnet run --project backend/MySelf.Tools.WgerImport -- fetch` |
| Packaged foods (barcode) | [Open Food Facts](https://world.openfoodfacts.org) (ODbL) | Runtime lookup + 24h cache; no key needed |
| Generic food text-search | USDA FoodData Central | Deferred to a later phase; key goes in `.env` as `NUTRIAPI_KEY` |

## Layout

```
backend/
  MySelf.Api/             ASP.NET Core Web API (endpoints, DI, config)
  MySelf.Application/      use-case layer (thin for now)
  MySelf.Domain/          entities + domain logic (Exercises/, Nutrition/)
  MySelf.Infrastructure/  EF Core, migrations, external integrations
  MySelf.Tools.WgerImport/ one-off catalogue import (build-time only)
  MySelf.UnitTests/  MySelf.IntegrationTests/
  seed-data/             committed import snapshots
frontend/
  src/app/         router, providers, app shell
  src/features/    one folder per screen area
  src/components/ui/  design-system primitives
  src/styles/      Tailwind theme + Balanced Indigo tokens
docs/              product + engineering spec
```
