# Mastemis client architecture

## Authorship and license

Mastemis is authored by **Lê Hùng Quang Minh** and distributed under the
Mozilla Public License 2.0 (`MPL-2.0`). The root `LICENSE`, `NOTICE`, central
build metadata, and repository REUSE annotation define the covered source.
Third-party dependencies retain their upstream licenses. The authenticated
client exposes the same author, version, copyright, and license information
through its About page.

The Mastemis client is a shared Uno Platform application. `Mastemis.Client.Core` contains platform-neutral MVVM state, typed HTTP clients, session state, navigation policy, validation, and the SignalR client. `Mastemis.Client` contains WinUI-compatible XAML, platform startup, resource dictionaries, and local preference storage. Views never call `HttpClient` directly.

The default build targets Skia Desktop. The same project contains the WebAssembly head; set `MastemisBuildWebAssembly=true` after installing the .NET `wasm-tools` workload. No client telemetry is enabled.

## Connection and authentication

Production connections require HTTPS. Loopback HTTP is accepted for development. Host Mode and Connect Mode share the same authenticated shell. The client remembers only non-secret preferences. It never stores passwords, worker secrets, or a custom plaintext copy of the authentication cookie.

Mutation requests acquire an antiforgery token from `/api/auth/antiforgery` and send it as `X-CSRF-TOKEN`. Human authentication uses the server cookie. A 401 clears client identity state. Problem Details are bounded before parsing and correlation identifiers are retained.

## Navigation and authorization

Navigation descriptors are filtered by server roles, but server authorization remains authoritative. Candidate, invigilator, authoring, evidence, worker-monitoring, and health routes are separated. A server denial is displayed as a typed failure; hiding a menu item is never treated as authorization.

## Realtime

The SignalR client connects to `/hubs/exam`, requests only server-authorized groups, rejoins after reconnect, accepts version 1 envelopes, and deduplicates message IDs with bounded memory. Event payloads are not logged. UI dispatch is abstracted for platform-specific dispatching.

## Current UI scope

Connection, login, role navigation, dashboard identity, examination create/inspect/transition, candidate draft/submission, Problem Studio draft/MAS/generation operations, health, and settings are connected to production endpoints. Rooms, candidate administration, submission history, invigilation, evidence, problem library, and worker monitoring currently expose role-aware navigation and honest integration-boundary states because the current backend lacks complete list/query contracts for those views.

The initial editor uses a bounded multiline monospace surface. It does not claim syntax highlighting or browser SFE collection.
