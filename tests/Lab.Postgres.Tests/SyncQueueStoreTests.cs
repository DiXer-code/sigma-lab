using Lab.Client.Maui.Sync;

namespace Lab.Postgres.Tests;

public sealed class SyncQueueStoreTests
{
    [Fact]
    public async Task EmptyQueue_DoesNotCreateFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "queue.json");

        Assert.Empty(await new SyncQueueStore(path).GetAllAsync());
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ConcurrentWriters_DoNotLoseOperations()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "queue.json");
        var stores = new[] { new SyncQueueStore(path), new SyncQueueStore(path) };
        var ids = Enumerable.Range(0, 24).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(ids.Select((id, index) =>
            stores[index % stores.Length].EnqueueAsync("Measurement", id.ToString(), "{}", id)));

        var items = await stores[0].GetAllAsync();
        Assert.Equal(ids.Length, items.Count);
        Assert.All(ids, id => Assert.Contains(items, item => item.OperationId == id));
    }

    [Fact]
    public async Task CorruptQueue_IsReported_AndNotOverwritten()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "queue.json");
        await File.WriteAllTextAsync(path, "invalid-json");

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() =>
            new SyncQueueStore(path).EnqueueAsync("Measurement", "id", "{}"));
        Assert.Equal("invalid-json", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task EnqueueAsync_IsIdempotent_AndPersistsPendingItem()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "queue.json");
        var operationId = Guid.NewGuid();
        var store = new SyncQueueStore(path);

        var first = await store.EnqueueAsync("Measurement", "measurement-1", "{\"value\":12.5}", operationId);
        var repeated = await store.EnqueueAsync("Measurement", "measurement-1", "{\"value\":12.5}", operationId);
        var reopened = await new SyncQueueStore(path).GetAllAsync();

        Assert.Equal(first, repeated);
        Assert.Equal(SyncQueueStatus.Pending, first.Status);
        Assert.Single(reopened);
        Assert.Equal(operationId, reopened[0].OperationId);
    }

    [Fact]
    public async Task FailedItem_CanBeRetried_AndMarkedSent()
    {
        using var directory = new TemporaryDirectory();
        var store = new SyncQueueStore(Path.Combine(directory.Path, "queue.json"));
        var item = await store.EnqueueAsync("Measurement", "measurement-2", "{}");

        var failed = await store.MarkFailedAsync(item.OperationId, "Мережа недоступна");
        var pending = await store.RetryAsync(item.OperationId);
        var sent = await store.MarkSentAsync(item.OperationId);

        Assert.Equal(SyncQueueStatus.Failed, failed.Status);
        Assert.Equal("Мережа недоступна", failed.LastError);
        Assert.Equal(SyncQueueStatus.Pending, pending.Status);
        Assert.Null(pending.LastError);
        Assert.Equal(SyncQueueStatus.Sent, sent.Status);
        Assert.Equal(2, sent.Attempts);
        Assert.NotNull(sent.LastAttemptAtUtc);
    }

    [Fact]
    public async Task UpdatingUnknownOperation_ThrowsKeyNotFoundException()
    {
        using var directory = new TemporaryDirectory();
        var store = new SyncQueueStore(Path.Combine(directory.Path, "queue.json"));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.MarkSentAsync(Guid.NewGuid()));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sigma-sync-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
