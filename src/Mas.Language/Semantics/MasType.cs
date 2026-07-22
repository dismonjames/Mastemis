namespace Mastemis.Mas.Language.Semantics;

public enum MasType { Error, Integer, Float, Boolean, String, Array, Permutation, Tree, Graph, Generated, Void }
public sealed record VariableSymbol(string Name, MasType Type);
public sealed record FunctionSymbol(string Name, int MinimumArguments, int MaximumArguments, MasType ResultType);
