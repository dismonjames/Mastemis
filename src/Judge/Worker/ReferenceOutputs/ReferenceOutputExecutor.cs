using System.Security.Cryptography;
using Mastemis.Contracts.Judge;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Judge.Configuration;
using Mastemis.Judge.Execution;
using Mastemis.Judge.Languages;
using Mastemis.Judge.Workspaces;
using Mastemis.Sandbox.Abstractions;
using Mastemis.Sandbox.Contracts;

namespace Mastemis.Judge.Worker.ReferenceOutputs;

public sealed class ReferenceOutputExecutor(IEnumerable<ILanguageAdapter> languages, IJudgeWorkspaceManager workspaces,
    ISandboxRunner sandbox, IReferenceOutputServerClient server, JudgeOrchestratorOptions options)
{
    public async Task ExecuteAsync(ReferenceOutputJobLease lease, CancellationToken cancellationToken)
    {
        var payload = await server.GetPayloadAsync(lease.JobId, lease.LeaseToken, cancellationToken); payload.Validate();
        var language = languages.SingleOrDefault(x => x.LanguageId.Equals(payload.Language, StringComparison.OrdinalIgnoreCase))
            ?? throw new JudgeContractException(JudgeFailureCode.UnsupportedLanguage, "Reference solution language is unavailable.");
        await server.StartAsync(lease.JobId, lease.LeaseToken, cancellationToken);
        await using var workspace = await workspaces.CreateAsync(cancellationToken);
        var sourceFiles = new List<SourceFile>(payload.Sources.Count);
        foreach (var source in payload.Sources)
        {
            var bytes = await server.GetSourceAsync(lease.JobId, lease.LeaseToken, source.FileName, source.Length, cancellationToken);
            Verify(bytes, source.Sha256); sourceFiles.Add(new(source.FileName, bytes));
        }
        var materialized = await workspace.MaterializeSourcesAsync(sourceFiles, language.SourceExtensions, cancellationToken);
        var compilation = await language.CompileAsync(new(materialized.Select(x => x.InternalPath).ToArray(), workspace.SourceDirectory,
            workspace.BuildDirectory, payload.Limits), cancellationToken);
        if (!compilation.Succeeded || compilation.Artifact is null)
            throw new JudgeContractException(compilation.FailureCode ?? JudgeFailureCode.CompilationFailed, "Reference solution compilation failed.");
        var plan = await language.CreateExecutionPlanAsync(compilation.Artifact, new("linux-x64", new Dictionary<string, string>()), cancellationToken);
        var completed = 0; var backend = string.Empty;
        foreach (var test in payload.Tests.OrderBy(x => x.TestIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inputBytes = await server.GetInputAsync(lease.JobId, lease.LeaseToken, test.TestIndex, test.InputLength, cancellationToken);
            Verify(inputBytes, test.InputSha256);
            var input = Path.Combine(workspace.InputDirectory, $"reference-{test.TestIndex:D5}.in");
            var output = Path.Combine(workspace.OutputDirectory, $"reference-{test.TestIndex:D5}.out");
            var error = Path.Combine(workspace.OutputDirectory, $"reference-{test.TestIndex:D5}.err");
            await File.WriteAllBytesAsync(input, inputBytes, cancellationToken);
            var result = await sandbox.RunAsync(new(options.Image, plan.Executable, plan.Arguments, workspace.Root, input, output, error,
                plan.Environment, new(payload.Limits.CpuTime, payload.Limits.WallTime, payload.Limits.MemoryBytes,
                    payload.Limits.OutputBytes, payload.Limits.FileBytes, payload.Limits.ProcessCount)), cancellationToken);
            backend = result.Backend; var mapped = SandboxVerdictMapper.Map(result);
            if (mapped.Verdict is not null) throw new JudgeContractException(mapped.Failure ?? JudgeFailureCode.RuntimeFailure, "Reference solution execution failed.");
            var bytes = await File.ReadAllBytesAsync(output, cancellationToken);
            if (bytes.LongLength > payload.Limits.OutputBytes) throw new JudgeContractException(JudgeFailureCode.OutputLimit, "Reference output exceeded its limit.");
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await server.UploadAsync(lease.JobId, new(payload.OperationId, lease.LeaseToken, payload.ContractVersion, test.TestIndex,
                hash, bytes.LongLength, (long)result.WallTime.TotalMilliseconds, result.PeakMemoryBytes, backend, options.JudgeVersion), bytes, cancellationToken);
            completed++;
        }
        await server.CompleteAsync(lease.JobId, new(lease.JobId, payload.OperationId, lease.WorkerId, lease.LeaseToken,
            completed, options.JudgeVersion, backend), cancellationToken);
    }
    private static void Verify(byte[] content, string expected)
    { var actual = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(); if (!actual.Equals(expected, StringComparison.Ordinal)) throw new JudgeContractException(JudgeFailureCode.InvalidContract, "Downloaded object hash is invalid."); }
}
