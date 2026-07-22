using Mastemis.Client.Core.Navigation;

namespace Mastemis.Client.Navigation;

public interface IClientPage { ClientRoute Route { get; } }

public sealed class ClientPageRegistry(IEnumerable<IClientPage> pages)
{
    private readonly IReadOnlyDictionary<ClientRoute, IClientPage> pages = pages.ToDictionary(page => page.Route);
    public Page Resolve(ClientRoute route) => pages.TryGetValue(route, out var page) ? (Page)page : (Page)pages[ClientRoute.NotFound];
}
