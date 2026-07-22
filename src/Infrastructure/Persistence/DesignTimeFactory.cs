using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mastemis.Infrastructure.Persistence;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<MastemisDbContext>
{
    public MastemisDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MASTEMIS_MIGRATION_CONNECTION")
            ?? "Host=localhost;Database=mastemis;Username=mastemis";
        var options = new DbContextOptionsBuilder<MastemisDbContext>().UseNpgsql(connectionString).Options;
        return new MastemisDbContext(options);
    }
}
