using Lab.Client.Maui.Data;
using Lab.Domain.Sites;
using Lab.Infrastructure.DependencyInjection;
using Lab.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lab.Postgres.Tests;

public sealed class LocalDatabaseServiceTests
{
    [Fact]
    public async Task LocalDatabase_UsesSharedMigrations_AndPersistsSites()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sigma-local-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Sqlite"] = $"Data Source={Path.Combine(directory, "lab.sqlite3")};Pooling=False",
        }).Build();

        try
        {
            await using var services = new ServiceCollection()
                .AddLabInfrastructure(configuration)
                .AddSingleton<LocalDatabaseService>()
                .BuildServiceProvider();
            var database = services.GetRequiredService<LocalDatabaseService>();
            Assert.Empty(await database.GetSitesAsync());

            await using (var scope = services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<LabDbContext>();
                context.Sites.Add(new Site { Name = "Майданчик", Latitude = 50.45, Longitude = 30.52, CreatedAt = DateTime.UtcNow });
                await context.SaveChangesAsync();
            }

            var site = Assert.Single(await database.GetSitesAsync());
            Assert.Equal("Майданчик", site.Name);
            Assert.True(site.Id > 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
