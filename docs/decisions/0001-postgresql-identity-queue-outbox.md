# ADR 0001: PostgreSQL production foundation

Status: Accepted.

PostgreSQL is the system of record for identity, examination state, source metadata, submissions, SFE records, warnings, judge jobs, audit data, and outbox messages. Binary source remains behind the storage abstraction. This keeps deployment infrastructure small and lets state changes, final termination, queue creation, and notification intent share one transaction.

ASP.NET Core Identity provides human password hashing, normalization, cookies, roles, and lockout. Worker credentials use a separate authentication scheme and Identity's password hasher; raw secrets are returned once and never stored. Resource authorization remains explicit because roles alone cannot represent examination, room, or candidate ownership.

Judge claims use PostgreSQL `FOR UPDATE SKIP LOCKED` and expiring unpredictable lease identifiers. Realtime publication uses a transactional outbox and is at-least-once because PostgreSQL and SignalR cannot commit atomically. Clients deduplicate by outbox message identifier.
