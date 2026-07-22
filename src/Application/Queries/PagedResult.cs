namespace Mastemis.Application.Queries;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Offset, int Limit, int Total);
