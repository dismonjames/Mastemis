# Linux sandbox design

The initial Linux backend is Podman OCI isolation. It is fail-closed: an absent runtime, absent pinned image, failed capability probe, or failed mandatory isolation control yields `InfrastructureError`; there is no `Process.Start` fallback for candidate programs.

Each run uses structured arguments with `--network none`, `--read-only`, `--cap-drop ALL`, `no-new-privileges`, `--userns keep-id`, a configured non-root container user, PID and memory limits, CPU and file-size ulimits, a bounded temporary filesystem, and exactly one writable job-workspace bind. The environment is cleared and rebuilt from a small validated allowlist. The repository, home directory, database, storage, Docker socket, and server secrets are not mounted. Wall timeout cancels and kills the runtime process tree; output is redirected to bounded workspace files and classified from backend evidence.

The capability probe checks runtime version, local image presence, rootless status, and an isolated smoke run. It reports cgroup, memory, process, network, read-only-filesystem, and resource-measurement support separately. Required controls must all be true before the worker processes jobs.
