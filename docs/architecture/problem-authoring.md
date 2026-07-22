# Problem authoring backend

The application layer exposes authorized draft creation, MAS validation, bounded preview, generation status, cancellation, and atomic publication. Preview never publishes tests and returns the seed, runtime version, diagnostics, bounded inputs, and truncation state.

PostgreSQL mode implements `IProblemStudioStore`. The `AddProblemAuthoring` migration creates draft, localized statement metadata, generation operation, generated test set, generated test, and package import/export audit tables. Drafts and operations have optimistic concurrency tokens. A partial unique index permits one pending/running generation per draft, one test set is allowed per operation, and test indexes are unique within a set.

The authenticated `/api/problem-studio` surface supports draft creation/retrieval, MAS update, validation, bounded preview, generation start/status, and cancellation. Administrator and ExamManager roles are currently trusted global authoring roles. Per-problem author assignments, examination-scoped mutation policy, statement/asset APIs, package APIs, and hidden-test content APIs are not implemented.

Large authoring content uses generated object identifiers. Files are written atomically beneath a canonical storage root, bounded and SHA-256 hashed, and remain staged until relational publication commits. Reconciliation asks PostgreSQL which staged identifiers are referenced; referenced staged files are promoted and only stale unreferenced files are removed. A database lookup failure aborts cleanup.

Reference-output worker jobs and complete import/export persistence remain incomplete. The API server and MAS runtime never execute native code, and there is no unsandboxed fallback.
