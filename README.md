# Mastemis

Mastemis is an open-source, self-hosted programming examination and judging platform. This repository delivers a server-side examination vertical slice plus a separate fail-closed Linux judge worker with C++ and C# compilation, exact/token checking, PostgreSQL leases, transactional result reporting, and SignalR delivery. Deployment data remains on infrastructure selected by the operator; the application contains no maintainer telemetry.

## Build and test

Requirements: .NET SDK selected by `global.json`. PostgreSQL is optional for liveness and required for a durable production deployment.

For a complete local development environment with PostgreSQL, migrations, a generated administrator,
verified HTTPS login, the API server, and Uno Desktop, run:

```bash
./scripts/dev-full.sh
```

The script prints the server URL, generated test credentials, health/OpenAPI URLs, version response,
and log location. Credentials and TLS material are generated under the gitignored `.mastemis-dev/`
directory with restrictive permissions. Use `./scripts/dev-full.sh --reset` to move an existing local
database aside and start clean, or `--no-ui` to run only PostgreSQL and the server.
Run the script as your normal user, never with `sudo`. Configure rootless Docker/Podman or grant
your user access to Docker; using `sudo` would create root-owned .NET build outputs. The script
detects and quarantines build outputs owned by another user before building. If the Docker socket
still requires elevation, the script may prompt once and applies `sudo` only to Docker commands.

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

Human users authenticate with `POST /api/auth/login` and a secure, HTTP-only, SameSite cookie. Obtain an antiforgery token from `/api/auth/antiforgery` for protected cookie mutations. Roles are Administrator, ExamManager, ChiefInvigilator, RoomInvigilator, Candidate, JudgeWorker, and EvidenceReviewer. Administrators manage human identities through `/api/admin/users`; examination and room assignments use `/api/scopes`. Candidate identities cannot be converted into worker credentials. Examination and room access require stored scope assignments.

Evidence metadata is available through `/api/evidence`. EvidenceReviewer and Administrator roles do not bypass package grants: reviewers need an explicit package grant, while assigned room and chief invigilators may read metadata in their scope. Successful metadata, item, timeline, and audit reads append access-audit rows. Binary screenshots and evidence export are not implemented.

Administrators issue worker credentials through `/api/admin/workers`. The returned secret is shown once; only its Identity password-hash representation is stored. Workers authenticate with `Authorization: Worker {worker-id}.{secret}` and use `/api/worker`. Rotation revokes prior credentials, revocation disables the worker, and optional expiry is enforced.

Judge claims use PostgreSQL row locks with `FOR UPDATE SKIP LOCKED`, unpredictable lease identifiers, expiry recovery, bounded attempts, and worker/lease validation. Outbox publication is at-least-once: state and versioned notification payloads commit together, dispatchers claim rows with `FOR UPDATE SKIP LOCKED`, poison repeated failures after ten attempts, and realtime envelopes contain a stable message identifier for client deduplication.

Source objects are atomically renamed before their metadata transaction commits. A hosted reconciler scans a bounded batch of old generated objects, verifies references in PostgreSQL first, and removes only stale unreferenced objects. Configure `Storage__OrphanAgeMinutes`, `Storage__ReconciliationIntervalMinutes`, and `Storage__ReconciliationBatchSize`; recent objects and all referenced objects are retained.

The API process never compiles or runs candidate code. Build the versioned Podman image and operate `src/Judge` on a dedicated Linux worker as described in [judge worker architecture](docs/architecture/judge-worker.md). The worker refuses jobs unless the configured image is already local and mandatory network, filesystem, privilege, memory, process, and cgroup controls pass. There is no unsandboxed fallback.

Durable Problem Studio support includes scoped authors, localized statements, bounded assets, revisioned MAS sources, reference-solution revisions, deterministic MAS input staging, a leased reference-output worker queue, verified expected-output ingestion, complete atomic publication, hidden-test authorization, transactional CreateNew and ReplaceDraft `.mas` import, deterministic stored export with audit/retention metadata, realtime progress, and hosted object reconciliation. Live PostgreSQL, Podman, and SignalR generation-flow verification remains environment-dependent. See [problem authoring](docs/architecture/problem-authoring.md), [test generation](docs/architecture/test-generation.md), and the [Problem Studio API](docs/architecture/problem-studio-api.md).

## Privacy

Operators control all identity, source, event, screenshot, evidence, authentication, and log data. Mastemis does not send these data to project maintainers and has no mandatory telemetry. Examination deployments must show a clear, unselected acknowledgement describing monitoring, event-triggered screenshot capture, evidence use, realtime alerts, three-warning termination, the operating organization, and local data control before SFE begins.

See [architecture](docs/architecture/overview.md), [threat model](docs/security/threat-model.md), and [privacy model](docs/security/privacy-model.md).
