# Test generation

Generation parses and binds before execution, initializes the deterministic PRNG from an explicit seed, evaluates declarations in source order, assigns directive strategies without increasing the requested count, formats bytes deterministically, hashes each input, and records duplicate count. Preview is bounded and non-publishing. The production store boundary is responsible for staged object writes, relational metadata, atomic publication, outbox notification, and cleanup.

Expected-output generation is not yet connected. It must be a durable job consumed by a sandbox-capable worker and must fail closed when Podman or the pinned image is unavailable.
