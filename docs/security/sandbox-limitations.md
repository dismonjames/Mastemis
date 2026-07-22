# Sandbox limitations

The repository provides a fail-closed Podman backend, but its guarantees depend on the host kernel, cgroups, Podman/OCI runtime, and pinned toolchain image. Rootless Podman reduces daemon privilege; it does not eliminate kernel trust. Rootful operation has a larger consequence if the runtime is compromised and should use a dedicated host.

Peak-memory and signal data are reported only when the backend can measure them. File-size enforcement uses container ulimits and writable-mount confinement; host filesystem quotas are not configured by Mastemis. CPU classification combines timeout and backend state, and ambiguous kills are reported as RuntimeError or InfrastructureError instead of falsely claiming a memory verdict. The default backend denies all network, but operators must also isolate the worker host because a runtime escape would bypass container networking.

The worker never falls back to unsandboxed execution. Missing mandatory capabilities make it unready and jobs remain durable for another capable worker.

No sandbox should claim protection from every kernel or runtime exploit. Operators remain responsible for host patching, dedicated worker isolation, restricted credentials, resource monitoring, and incident response.
