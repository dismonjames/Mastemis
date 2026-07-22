using Mastemis.Client.Core.Navigation;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Tests.Navigation;

public sealed class NavigationCatalogTests
{
    [Fact]
    public void CandidateNavigationDoesNotExposeManagementRoutes()
    {
        var session = new ClientSession();
        session.Authenticate(new(Guid.NewGuid(), "candidate", null, ["Candidate"]));
        var routes = new NavigationCatalog().For(session).Select(value => value.Route).ToArray();
        Assert.Contains(ClientRoute.CandidateExam, routes);
        Assert.DoesNotContain(ClientRoute.Workers, routes);
        Assert.DoesNotContain(ClientRoute.ProblemStudio, routes);
    }

    [Fact]
    public void AdministratorNavigationIncludesOperationsButNotCandidateWorkspace()
    {
        var session = new ClientSession();
        session.Authenticate(new(Guid.NewGuid(), "admin", null, ["Administrator"]));
        var routes = new NavigationCatalog().For(session).Select(value => value.Route).ToArray();
        Assert.Contains(ClientRoute.Health, routes);
        Assert.DoesNotContain(ClientRoute.CandidateExam, routes);
    }
}
