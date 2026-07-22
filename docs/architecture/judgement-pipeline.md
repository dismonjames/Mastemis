# Judgement pipeline

The durable job contract identifies one immutable source revision, a language, ordered test descriptors, checker IDs, and validated resource limits. Source and test bytes remain outside ordinary PostgreSQL rows and are exposed only to the worker that owns the unexpired lease. Generated object identifiers and canonical-path validation prevent route values or candidate names from becoming host paths.

The worker creates a generated workspace, maps source names to safe internal names, compiles through the selected adapter, and executes tests sequentially by index through `ISandboxRunner`. Compilation and runtime commands use `ProcessStartInfo.ArgumentList`; candidate flags, projects, package references, include paths, and response files are not accepted. Every outcome disposes the workspace. A stale-workspace pass removes only generated job directories older than its configured threshold.

Default verdict precedence is compilation failure, sandbox infrastructure failure, resource violation, nonzero runtime exit, checker mismatch, then Accepted. The first non-Accepted test stops execution. Exact checking compares bytes without newline normalization. Token checking requires valid UTF-8 and compares Unicode whitespace-separated tokens; extra and missing tokens fail. Both checkers enforce the configured size bound.

The server persists verdict, failed test, time, measured memory when available, exit metadata, byte counts, bounded diagnostic codes, sandbox backend, worker, judge version, and authoritative completion time. Full compiler output, stdout, stderr, source, and expected output are not stored in judgement rows or logs. Judgement and outbox rows commit together; SignalR publication remains at-least-once with stable message IDs.
