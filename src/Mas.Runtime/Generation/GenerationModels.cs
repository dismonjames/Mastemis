using Mastemis.Mas.Language.Diagnostics;

namespace Mastemis.Mas.Runtime.Generation;

public sealed record GeneratedTest(int Index, string Group, string Input, string Sha256, string Strategy);
public sealed record MasGenerationReport(ulong Seed, string RuntimeVersion, string RandomAlgorithm,
    IReadOnlyList<GeneratedTest> Tests, IReadOnlyList<MasDiagnostic> Diagnostics, int DuplicateCount);
public sealed class MasRuntimeException(string code, string message) : Exception(message) { public string Code { get; } = code; }
