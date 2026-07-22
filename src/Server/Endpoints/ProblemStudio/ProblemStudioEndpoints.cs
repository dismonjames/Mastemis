using Mastemis.Server.Endpoints.ProblemStudio.Drafts;
using Mastemis.Server.Endpoints.ProblemStudio.Generation;
using Mastemis.Server.Endpoints.ProblemStudio.Mas;

namespace Mastemis.Server.Endpoints.ProblemStudio;

public static class ProblemStudioEndpoints
{
    public static void MapProblemStudioEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/problem-studio").RequireAuthorization();
        group.MapProblemDraftEndpoints();
        group.MapProblemScopeEndpoints();
        group.MapProblemMasEndpoints();
        group.MapProblemGenerationEndpoints();
    }
}
