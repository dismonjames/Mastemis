# `.mas` package format

Format `1.0` is a ZIP-compatible container with one `manifest.json`. Paths use normalized forward slashes. Statements live under `statement/`, tests under `tests/`, sources under `generators/` or `solutions/`, assets under `assets/`, and checksums under `metadata/checksums.json`.

The manifest records problem identity, title, authors, tags, difficulty, locales, statements, time/memory/output limits, `cpp`/`csharp` languages, `exact`/`tokens` checker configuration, ordered weighted groups, static/generated tests, generators, reference solutions, assets, and SHA-256 checksums. Signature metadata is reserved and is not verified in version 1.0.

Export uses ordinal path ordering, no compression, a fixed ZIP timestamp, explicit JSON serialization, and SHA-256, making identical logical content deterministic.
