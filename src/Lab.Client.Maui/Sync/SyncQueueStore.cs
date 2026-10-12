using System.Text.Json;
namespace Lab.Client.Maui.Sync;
/// <summary>Стан локальної операції, що очікує на синхронізацію.</summary>
public enum SyncQueueStatus
{
    Pending,
    Sent,
    Failed,
}
/// <summary>Операція клієнта з ідемпотентним ідентифікатором та історією спроб.</summary>
public sealed record SyncQueueItem
{
    public Guid OperationId { get; init; }
    public string ResourceType { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public SyncQueueStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? LastAttemptAtUtc { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }
}
/// <summary>Атомарно зберігає чергу офлайн-операцій клієнта.</summary>
public sealed class SyncQueueStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    /// <summary>Додає операцію один раз за її UUID.</summary>
    public async Task<SyncQueueItem> EnqueueAsync(
        string resourceType,
        string resourceId,
        string payload,
        Guid? operationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var id = operationId ?? Guid.NewGuid();
            var existing = items.FirstOrDefault(item => item.OperationId == id);
            if (existing is not null)
            {
                return existing;
            }
            var item = new SyncQueueItem
            {
                OperationId = id,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Payload = payload,
                Status = SyncQueueStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow,
            };
            items.Add(item);
            await WriteAsync(items, cancellationToken).ConfigureAwait(false);
            return item;
        }
        finally
        {
            Gate.Release();
        }
    }
    /// <summary>Повертає знімок черги в порядку створення.</summary>
    public async Task<IReadOnlyList<SyncQueueItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadAsync(cancellationToken))
                .OrderBy(item => item.CreatedAtUtc)
                .ToArray();
        }
        finally
        {
            Gate.Release();
        }
    }
    /// <summary>Позначає операцію відправленою після підтвердження сервера.</summary>
    public Task<SyncQueueItem> MarkSentAsync(Guid operationId, CancellationToken cancellationToken = default) =>
        UpdateAsync(operationId, item => item with
        {
            Status = SyncQueueStatus.Sent,
            Attempts = item.Attempts + 1,
            LastAttemptAtUtc = DateTime.UtcNow,
            LastError = null,
        }, cancellationToken);
    /// <summary>Позначає помилку відправлення, зберігаючи причину для повторної спроби.</summary>
    public Task<SyncQueueItem> MarkFailedAsync(
        Guid operationId,
        string error,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return UpdateAsync(operationId, item => item with
        {
            Status = SyncQueueStatus.Failed,
            Attempts = item.Attempts + 1,
            LastAttemptAtUtc = DateTime.UtcNow,
            LastError = error,
        }, cancellationToken);
    }
    /// <summary>Повертає невдалу операцію в чергу очікування.</summary>
    public Task<SyncQueueItem> RetryAsync(Guid operationId, CancellationToken cancellationToken = default) =>
        UpdateAsync(operationId, item => item.Status == SyncQueueStatus.Failed
            ? item with { Status = SyncQueueStatus.Pending, LastError = null }
            : item, cancellationToken);
    private async Task<SyncQueueItem> UpdateAsync(
        Guid operationId,
        Func<SyncQueueItem, SyncQueueItem> update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var index = items.FindIndex(item => item.OperationId == operationId);
            if (index < 0)
            {
                throw new KeyNotFoundException($"Операцію {operationId} не знайдено в черзі.");
            }
            var changed = update(items[index]);
            items[index] = changed;
            await WriteAsync(items, cancellationToken).ConfigureAwait(false);
            return changed;
        }
        finally
        {
            Gate.Release();
        }
    }
    private async Task<List<SyncQueueItem>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<SyncQueueItem>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? [];
    }
    private async Task WriteAsync(List<SyncQueueItem> items, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, items, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
