# Threat model

Mastemis treats clients, candidate identifiers, client timestamps, client sequence values, archive names, compiler inputs, and worker claims as untrusted. Primary assets are identities, source revisions, authentication material, examination state, warning decisions, evidence, and judgement integrity.

Implemented controls include server UTC timestamps, explicit domain transitions, idempotency keys, duplicate-warning prevention by evaluation identity, terminal-session write rejection, bounded source size, safe generated object names, canonical path checks, atomic file moves, content hashing, HTTP rate limiting, request cancellation, secure response headers, stable error codes, and generic internal-error responses.

PostgreSQL mode uses Identity password hashing, lockout, secure SameSite cookies, explicit human roles, examination/room/candidate resource checks, hashed and revocable worker secrets, lease ownership checks, unique replay constraints, optimistic concurrency, durable audit metadata, and post-commit outbox delivery. Worker secrets are returned only on issuance or rotation. Worker authentication cannot use human management endpoints, and human cookies cannot use worker endpoints.

Evidence metadata access requires explicit grants or invigilator scope and successful reads are audited. Administrators do not implicitly bypass package review grants.

Remaining risks include operator TLS/configuration errors, the bounded period before orphan source reconciliation, missing referenced filesystem objects, at-least-once realtime duplicates, and denial-of-service beyond configured request/queue bounds. The Development runtime intentionally permits operations and must not be exposed. Browser DevTools detection remains an unreliable signal and never establishes guilt by itself.
