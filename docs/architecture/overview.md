# Architecture

## Repository and responsibilities

- `src/Domain`: framework-free identifiers, entities, state machines, and invariants.
- `src/Application`: use-case orchestration and infrastructure abstractions; no ASP.NET Core or EF Core dependency.
- `src/Infrastructure/Persistence`: aggregate persistence, focused identity and scope administration, auditing, durable queue, migrations, and concurrent outbox delivery.
- `src/Infrastructure/Storage`: hash-addressed source writes and bounded orphan reconciliation.
- `src/Server`: composition root plus feature-grouped authentication, administration, evidence, examination, and worker endpoints; resource authorization and realtime routing are separate subsystems.
- `src/Contracts`: versioned process-boundary judge and worker contracts without server implementation details.
- `src/Judge`: standalone authenticated worker, safe workspace lifecycle, C++ and C# adapters, output checkers, and deterministic judgement orchestration.
- `src/Sandbox`: independently testable fail-closed Podman isolation contracts, capability probe, and Linux backend.
- `tests`: behavior tests at domain, application, and HTTP boundaries.
- `deploy/compose`: opt-in local PostgreSQL.

Dependencies point `Domain <- Application <- Infrastructure <- Server`. The server also references Application to expose process-boundary contracts. Domain contains no persistence annotations. UI code must never reference a DbContext when introduced.

## Main flow

An operator creates and opens an examination, creates a room, and registers candidates. A candidate starts a session only while the exam is open, saves hash-addressed source metadata after atomic file storage, and submits an immutable revision. Submission creation enqueues a judge job and writes an outbox event inside the application transaction boundary. Raw SFE events are server-timestamped and separately evaluated. Only a confirmed evaluation can create a warning. The third distinct warning freezes the authoritative revision, creates exactly one final submission and judge job, and emits a termination outbox event.

When PostgreSQL is configured, a scoped `PostgresRuntime` rehydrates framework-free aggregates and synchronizes tracked rows inside one EF transaction. The third stored confirmed evaluation creates its warning, terminates the session, freezes the current source revision, creates one final submission and job, terminates examination access, appends audit/outbox rows, and commits once. Database unique constraints and an optimistic session token protect retries and competing instances.

Queue claims use `FOR UPDATE SKIP LOCKED`; leases bind a worker, job, unpredictable lease identifier, and expiry. Completion checks all four and is idempotent for an already completed job owned by the same worker. Expired leases are eligible for recovery while attempts remain.

Leased workers download only generated source and test objects for their job. The worker compiles outside the API process, but candidate execution always passes through the Podman sandbox. Results persist bounded metadata and create the existing judgement outbox notifications in the same database save. See `judge-worker.md` and `judgement-pipeline.md`.

Outbox messages contain a versioned type, JSON contract, occurrence time, retry state, and stable identifier. Each dispatcher holds a PostgreSQL `FOR UPDATE SKIP LOCKED` claim through publication and marks success only after SignalR returns. A crash between publication and commit can redeliver, so delivery is at-least-once and `RealtimeEnvelope.MessageId` is the client deduplication key. Failures use bounded exponential backoff and the tenth failure marks the row poison. Group entry is authorized independently for examination, room, enabled candidate, assigned chief, and bound worker scopes.

Source contents remain outside PostgreSQL. Files use generated names, canonical paths, atomic rename, SHA-256, and relational length metadata. Filesystem and PostgreSQL cannot share a transaction: a database rollback after file finalization can leave an unreferenced object. The reconciliation service examines only generated `source/{guid}.bin` names, waits for the configured orphan age, queries all candidate references before deletion, and is idempotent. A database failure suspends that pass without deletion. A missing referenced object remains an operational fault.

Evidence metadata packages, items, and explicit reviewer grants are relational. Room and chief access derives from assignments; Administrator and EvidenceReviewer identities require a package grant. Successful sensitive reads append immutable application-level audit records. Binary evidence storage and export remain outside this server-foundation stage.

## Local operation

Run all commands from the repository root. `dotnet format Mastemis.sln`, `dotnet restore Mastemis.sln`, `dotnet build Mastemis.sln`, and `dotnet test Mastemis.sln` validate the current solution. Use the compose instructions in the README for PostgreSQL.
