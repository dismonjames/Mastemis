# Architecture

## Repository and responsibilities

- `src/Domain`: framework-free identifiers, entities, state machines, and invariants.
- `src/Application`: use-case orchestration and infrastructure abstractions; no ASP.NET Core or EF Core dependency.
- `src/Infrastructure`: development runtime, production aggregate persistence, ASP.NET Core Identity store, durable queue, worker credentials, atomic source storage, migrations, and outbox dispatcher.
- `src/Server`: composition root, cookie and worker authentication, resource-scoped authorization, HTTP API, health checks, secure headers, rate limiting, OpenAPI, and SignalR fan-out.
- `tests`: behavior tests at domain, application, and HTTP boundaries.
- `deploy/compose`: opt-in local PostgreSQL.

Dependencies point `Domain <- Application <- Infrastructure <- Server`. The server also references Application to expose process-boundary contracts. Domain contains no persistence annotations. UI code must never reference a DbContext when introduced.

## Main flow

An operator creates and opens an examination, creates a room, and registers candidates. A candidate starts a session only while the exam is open, saves hash-addressed source metadata after atomic file storage, and submits an immutable revision. Submission creation enqueues a judge job and writes an outbox event inside the application transaction boundary. Raw SFE events are server-timestamped and separately evaluated. Only a confirmed evaluation can create a warning. The third distinct warning freezes the authoritative revision, creates exactly one final submission and judge job, and emits a termination outbox event.

When PostgreSQL is configured, a scoped `PostgresRuntime` rehydrates framework-free aggregates and synchronizes tracked rows inside one EF transaction. The third stored confirmed evaluation creates its warning, terminates the session, freezes the current source revision, creates one final submission and job, terminates examination access, appends audit/outbox rows, and commits once. Database unique constraints and an optimistic session token protect retries and competing instances.

Queue claims use `FOR UPDATE SKIP LOCKED`; leases bind a worker, job, unpredictable lease identifier, and expiry. Completion checks all four and is idempotent for an already completed job owned by the same worker. Expired leases are eligible for recovery while attempts remain.

Outbox messages contain a versioned type, JSON contract, occurrence time, retry state, and stable identifier. The hosted dispatcher publishes only committed rows, marks success after SignalR returns, and retries failures with bounded exponential backoff. SignalR delivery is at-least-once, so `RealtimeEnvelope.MessageId` is the client deduplication key. Group entry is authorized for examination, room, candidate, chief, and worker scopes.

Source contents remain outside PostgreSQL. Files use generated names, canonical paths, atomic rename, SHA-256, and relational length metadata. Filesystem and PostgreSQL cannot share a transaction: a database rollback after file finalization can leave an unreferenced object. Operators should periodically reconcile unreferenced objects; a missing referenced object is an operational fault and readiness verifies only storage accessibility.

## Local operation

Run all commands from the repository root. `dotnet format Mastemis.sln`, `dotnet restore Mastemis.sln`, `dotnet build Mastemis.sln`, and `dotnet test Mastemis.sln` validate the current solution. Use the compose instructions in the README for PostgreSQL.
