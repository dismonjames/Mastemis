using Mastemis.Mas.Language.Diagnostics;
using Mastemis.Mas.Language.Syntax.Nodes;

namespace Mastemis.Mas.Language.Semantics;

public sealed record SemanticModel(CompilationUnitSyntax Root, IReadOnlyDictionary<string, VariableSymbol> Variables,
    IReadOnlyList<MasDiagnostic> Diagnostics);
