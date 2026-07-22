# Judge worker

`Mastemis.Judge` is a separate .NET worker process. The ASP.NET Core API never loads a compiler or executes candidate code. A worker authenticates with its existing one-time-issued, hashed server credential, reports capacity and toolchains, claims a PostgreSQL lease, downloads only its leased job payload, renews the lease while working, and reports a bounded result. Worker ID, job ID, submission ID, and unpredictable lease ID are verified again at every operation.

The worker uses bounded concurrency and stops claiming when capacity is full. Shutdown stops new claims and drains until `Worker__ShutdownTimeout`; unfinished work is never reported successful and PostgreSQL lease expiry makes it recoverable. A duplicate successful completion is idempotent. Secrets come from configuration such as `Worker__Secret`; they are neither hardcoded nor logged.

Local readiness can be inspected without contacting the server:

```bash
dotnet run --project src/Judge -- --probe
```

The JSON probe contains no secret or workspace path. It succeeds only when the configured workspace is writable, both toolchains exist, the pinned image is already local, and Podman proves the mandatory isolation profile. Normal readiness additionally requires successful server authentication and heartbeat.

Build the pinned local image before an examination:

```bash
podman build -t localhost/mastemis-judge:0.1.0 -f deploy/docker/judge/Dockerfile .
```

Configure `Worker__Id`, `Worker__Secret`, `Worker__ServerUrl`, `Worker__WorkspaceRoot`, `Worker__Capacity`, `Sandbox__RuntimePath`, `Sandbox__Image`, `Toolchains__Cpp`, and `Toolchains__Dotnet`. Place workers on dedicated hosts or strongly isolated VMs with no database credentials, no server storage mount, and no access to sensitive internal services.
