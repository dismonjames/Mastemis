using System.Net;

namespace Mastemis.Client.Core.Networking.Http;

public sealed record ApiProblem(HttpStatusCode Status, string Title, string? Detail, string? Code,
    string? CorrelationId, IReadOnlyDictionary<string, string[]> Errors);

public sealed class ApiException(ApiProblem problem) : Exception(problem.Title)
{
    public ApiProblem Problem { get; } = problem;
}
