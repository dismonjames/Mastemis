# Problem Studio API

The durable API is rooted at `/api/problem-studio`. Human cookie authentication and an `X-CSRF-TOKEN` obtained from `/api/auth/antiforgery` are required for mutating calls. Request identifiers are always checked again by Application authorization; possession of a route identifier is not authority.

## Draft and MAS resources

- `GET/POST /drafts` lists authorized drafts or creates one.
- `GET/PUT/DELETE /drafts/{problemId}` reads, version-updates, or deletes an editable draft.
- `/drafts/{problemId}/statements/{locale}` and `/assets` manage bounded localized content and allowlisted assets.
- `GET/PUT /drafts/{problemId}/mas` reads or revision-updates MAS source.
- `POST .../mas/validate` stores the latest bounded diagnostic result; `POST .../mas/preview` remains non-publishing.

Draft metadata and MAS writes use expected revisions. An open examination assignment blocks mutation. MAS source, statements, assets, reference source, hidden tests, and package bytes are not logged.

## Generation resources

- `POST /drafts/{problemId}/generation` starts an idempotent durable operation.
- `GET .../generation/{operationId}` returns the stable operation state.
- `GET .../progress` returns counters and the reference-job state without object identifiers.
- `GET .../diagnostics?offset=0&limit=50` returns at most 100 bounded diagnostic records.
- `DELETE .../generation/{operationId}` requests idempotent cancellation.

Progress events are emitted through the transactional outbox. SignalR delivery is at least once and carries a stable message identifier; clients must deduplicate. Event payloads exclude source, hidden input, expected output, and storage paths.

## Package resources

- `POST /packages/validate` inspects a bounded package without importing it.
- `POST /packages/import` performs CreateNew and requires `Idempotency-Key`.
- `PUT /drafts/{problemId}/packages/import?expectedVersion=N` performs ReplaceDraft.
- `GET /packages/imports/{importId}` and `GET /drafts/{problemId}/packages/imports` expose successful import metadata.
- `POST /drafts/{problemId}/packages/export` creates a retained deterministic export.
- `GET /drafts/{problemId}/packages/exports` lists authorized metadata.
- `GET` or `DELETE .../exports/{exportId}` downloads or expires a retained export.

Export and hidden-test access requires the explicit hidden-test permission. Administrator identity alone is not a bypass. Successful imports currently expose `Completed` status; failed validation is returned as Problem Details and does not create a visible import row.

## Capability-dependent verification

PostgreSQL queue, migration, and contention tests use Testcontainers and skip when Docker is absent. Reference execution and adversarial isolation tests skip when the pinned Podman backend or image is unavailable. There is no unsandboxed fallback. Operators must execute the authoring smoke harness in an isolated development deployment before relying on a new worker image or PostgreSQL version.
