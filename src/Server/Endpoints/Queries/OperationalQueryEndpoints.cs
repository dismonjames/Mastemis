namespace Mastemis.Server.Endpoints.Queries;

public static class OperationalQueryEndpoints
{
    public static void MapOperationalQueryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/queries").RequireAuthorization();
        group.MapDashboardQueryEndpoints();
    }
}
