using Mastemis.Server.Endpoints.ProblemStudio.Assets;
using Mastemis.Server.Endpoints.ProblemStudio.Activity;
using Mastemis.Server.Endpoints.ProblemStudio.Drafts;
using Mastemis.Server.Endpoints.ProblemStudio.Generation;
using Mastemis.Server.Endpoints.ProblemStudio.Mas;
using Mastemis.Server.Endpoints.ProblemStudio.Packages;
using Mastemis.Server.Endpoints.ProblemStudio.ReferenceSolutions;
using Mastemis.Server.Endpoints.ProblemStudio.Statements;
using Mastemis.Server.Endpoints.ProblemStudio.Tests;
using Microsoft.AspNetCore.Antiforgery;

namespace Mastemis.Server.Endpoints.ProblemStudio;

public static class ProblemStudioEndpoints
{
    public static void MapProblemStudioEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/problem-studio").RequireAuthorization()
            .WithMetadata(new RequireAntiforgeryTokenAttribute(true));
        group.MapProblemDraftEndpoints();
        group.MapProblemOverviewEndpoints();
        group.MapProblemScopeEndpoints();
        group.MapProblemStatementEndpoints();
        group.MapProblemAssetEndpoints();
        group.MapReferenceSolutionEndpoints();
        group.MapProblemMasEndpoints();
        group.MapProblemGenerationEndpoints();
        group.MapProblemTestEndpoints();
        group.MapProblemPackageEndpoints();
        group.MapProblemActivityEndpoints();
    }
}
