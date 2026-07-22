using Mastemis.Sandbox.Contracts;

namespace Mastemis.Sandbox.Abstractions;

public interface ISandboxRunner
{
    ValueTask<SandboxResult> RunAsync(SandboxRequest request, CancellationToken cancellationToken);
}

public interface ISandboxCapabilityProbe
{
    ValueTask<SandboxCapabilities> ProbeAsync(CancellationToken cancellationToken);
}
