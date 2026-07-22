using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Mastemis.Server.Tests;

public sealed class DurableCompositionTests
{
    [Fact]
    public async Task Durable_service_graph_builds_without_connecting_to_external_services()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Mastemis",
                "Host=127.0.0.1;Port=1;Database=mastemis;Username=test;Password=test");
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
