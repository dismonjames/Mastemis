using Mastemis.Application.Dashboard;

namespace Mastemis.Server.Endpoints.Queries;

public static class DashboardQueryEndpoints
{
    public static RouteGroupBuilder MapDashboardQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/dashboard", async (DashboardQueryService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(ct)));
        return group;
    }
}
