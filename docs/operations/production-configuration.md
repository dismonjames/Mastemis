# Production configuration

Required settings are supplied through environment variables or a secret provider:

- `ConnectionStrings__Mastemis`: PostgreSQL connection string.
- `Storage__Path`: operator-controlled source-object root, not writable by untrusted users.
- `Database__ApplyMigrations`: defaults to `true`; set `false` when migrations are deployed separately.
- `Identity__SessionMinutes`: cookie duration from 5 through 720 minutes.
- `Bootstrap__Administrator__Username` and `Bootstrap__Administrator__Password`: optional first-run values; neither has a default.

Serve only through HTTPS. Do not place secrets in `appsettings.json`, command histories, logs, or container images. Readiness checks PostgreSQL, applied migrations, source storage, durable queue queries, and dispatcher state. Liveness has no external dependency.
