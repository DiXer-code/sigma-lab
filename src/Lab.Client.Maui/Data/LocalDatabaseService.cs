using Lab.Domain.Sites;
using Lab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Lab.Client.Maui.Data;
/// <summary>Доступ клієнта до тих самих сутностей і Fluent-конфігурацій, що й сервер.</summary>
public sealed class LocalDatabaseService(IServiceScopeFactory scopeFactory)
{
    /// <summary>Застосовує наявні міграції SQLite і повертає майданчики.</summary>
    public async Task<IReadOnlyList<Site>> GetSitesAsync(CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LabDbContext>();
        await database.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        return await database.Sites
            .AsNoTracking()
            .OrderBy(site => site.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
