using Mastemis.Server.Endpoints.ProblemStudio.Assets;
using Mastemis.Server.Endpoints.ProblemStudio.Drafts;
using Mastemis.Server.Endpoints.ProblemStudio.Generation;
using Mastemis.Server.Endpoints.ProblemStudio.Mas;
using Mastemis.Server.Endpoints.ProblemStudio.ReferenceSolutions;
using Mastemis.Server.Endpoints.ProblemStudio.Statements;
using Mastemis.Server.Endpoints.ProblemStudio.Tests;

namespace Mastemis.Server.Endpoints.ProblemStudio;

public static class ProblemStudioEndpoints
{
    public static void MapProblemStudioEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/problem-studio").RequireAuthorization();
        group.MapProblemDraftEndpoints();
        group.MapProblemScopeEndpoints();
        group.MapProblemStatementEndpoints();
        group.MapProblemAssetEndpoints();
        group.MapReferenceSolutionEndpoints();
        group.MapProblemMasEndpoints();
        group.MapProblemGenerationEndpoints();
        group.MapProblemTestEndpoints();
    }
}
