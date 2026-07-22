using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Tests.Session;

public sealed class ClientSessionTests
{
    [Fact]
    public void SignOutClearsIdentityAndRoles()
    {
        var session = new ClientSession();
        session.Authenticate(new(Guid.NewGuid(), "manager", "Manager", ["ExamManager"]));
        session.SignOut();
        Assert.False(session.IsAuthenticated);
        Assert.Empty(session.Roles);
    }
}
