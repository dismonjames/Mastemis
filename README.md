# Mastemis

Mastemis is an open-source, self-hosted programming examination and judging platform. This repository delivers a server-side examination vertical slice with PostgreSQL persistence, ASP.NET Core Identity, scoped authorization, durable judge leases, a transactional outbox, and SignalR delivery. Deployment data remains on infrastructure selected by the operator; the application contains no maintainer telemetry.

## Build and test

Requirements: .NET SDK selected by `global.json`. PostgreSQL is optional for liveness and required for a durable production deployment.

```bash
dotnet restore Mastemis.sln
dotnet build Mastemis.sln
dotnet test Mastemis.sln
dotnet run --project src/Server
```

The server exposes `/health/live`, `/health/ready`, `/api/system/version`, authenticated examination/session APIs, worker APIs, and OpenAPI at `/openapi/v1.json`. Without a PostgreSQL connection string, readiness reports a degraded dependency and Development uses the explicitly volatile runtime. Production management operations fail closed without durable configuration.

Start local PostgreSQL with:

```bash
MASTEMIS_POSTGRES_PASSWORD='choose-a-local-password' docker compose -f deploy/compose/compose.yaml up -d
ConnectionStrings__Mastemis='Host=localhost;Database=mastemis;Username=mastemis;Password=choose-a-local-password' dotnet run --project src/Server
```

Never use the compose fallback password outside a disposable local environment.

Migrations are generated and applied with:

```bash
dotnet tool restore
dotnet ef database update --project src/Infrastructure
```

The server applies pending migrations at startup unless `Database__ApplyMigrations=false`. For an optional first administrator, set `Bootstrap__Administrator__Username` and `Bootstrap__Administrator__Password` through a secret provider or environment only. If either value is absent, no account is created. Existing accounts are never overwritten and credentials are never logged. Remove the bootstrap values after first startup.

Human users authenticate with `POST /api/auth/login` and a secure, HTTP-only, SameSite cookie. Obtain an antiforgery token from `/api/auth/antiforgery` for protected cookie mutations. Roles are Administrator, ExamManager, ChiefInvigilator, RoomInvigilator, Candidate, JudgeWorker, and EvidenceReviewer. Examination and room access also require stored scope assignments; Administrator does not implicitly receive evidence-review access.

Administrators issue worker credentials through `/api/admin/workers`. The returned secret is shown once; only its Identity password-hash representation is stored. Workers authenticate with `Authorization: Worker {worker-id}.{secret}` and use `/api/worker`. Rotation revokes prior credentials, revocation disables the worker, and optional expiry is enforced.

Judge claims use PostgreSQL row locks with `FOR UPDATE SKIP LOCKED`, unpredictable lease identifiers, expiry recovery, bounded attempts, and worker/lease validation. Outbox publication is at-least-once: state and versioned notification payloads commit together, a bounded dispatcher publishes after commit, and realtime envelopes contain a stable message identifier for client deduplication.

## Privacy

Operators control all identity, source, event, screenshot, evidence, authentication, and log data. Mastemis does not send these data to project maintainers and has no mandatory telemetry. Examination deployments must show a clear, unselected acknowledgement describing monitoring, event-triggered screenshot capture, evidence use, realtime alerts, three-warning termination, the operating organization, and local data control before SFE begins.

See [architecture](docs/architecture/overview.md), [threat model](docs/security/threat-model.md), and [privacy model](docs/security/privacy-model.md).
