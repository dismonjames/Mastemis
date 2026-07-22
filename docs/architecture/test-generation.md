# Test generation

Generation parses and binds before execution, initializes SplitMix64 v1 from an explicit seed, evaluates declarations in source order, assigns directive strategies without increasing the requested count, formats bytes deterministically, hashes each input, and records duplicate count. Preview is bounded to ten cases, two seconds, one MiB total generation output, and 64 KiB per returned input, and is non-publishing.

The PostgreSQL store stages each input, verifies its hash, then creates the versioned test set, ordered test metadata, completed operation, and deduplicable outbox message in one transaction. An operation can publish one test set. A process interruption after database commit but before file promotion is recovered by reference-aware reconciliation.

Expected-output generation is not yet connected. Current publication is input-only and must not be treated as complete judge data for a checker requiring expected output. Reference output must be a distinct durable job consumed by a sandbox-capable worker and must fail closed when Podman or the pinned image is unavailable.
