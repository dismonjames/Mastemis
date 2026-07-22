# Test generation

Generation parses and binds before execution, initializes SplitMix64 v1 from an explicit seed, evaluates declarations in source order, assigns directive strategies without increasing the requested count, formats bytes deterministically, hashes each input, and records duplicate count. Preview is bounded to ten cases, two seconds, one MiB total generation output, and 64 KiB per returned input, and is non-publishing.

The PostgreSQL store stages each input, verifies its hash, creates an unpublished versioned test set, and transitions the operation to `WaitingForReferenceOutputs`. The durable states are Pending, Validating, GeneratingInputs, WaitingForReferenceOutputs, Publishing, Completed, Failed, CancelRequested, and Cancelled. Transitions and progress are concurrency-protected and emit deduplicable outbox messages.

Expected outputs use a distinct authoring job without candidate, score, leaderboard, or submission-history semantics. The Judge worker compiles the current C++ or C# reference revision once and executes ordered inputs only through the configured Podman backend. Hashes are verified on download and ingestion. Job ownership, lease, operation, membership, index, size, and hash are server-validated. Compilation, runtime, timeout, memory, output, lease, and infrastructure failures remain distinct.

Publication requires a completed reference job and one expected output for every input. It replaces the current judge test rows only when the problem is not attached to an open examination. The database transaction creates exactly one visible set per operation; filesystem promotion occurs afterward and a hosted reconciler promotes referenced staged objects or deletes only old, unreferenced objects after a successful database reference check.

Podman enforcement and PostgreSQL race behavior are capability-dependent and must be verified in the target deployment. The repository keeps those tests skipped with explicit reasons when the services are unavailable.
