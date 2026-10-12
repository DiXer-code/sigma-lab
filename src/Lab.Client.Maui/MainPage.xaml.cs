using System.Text.Json;
using Lab.Client.Maui.Data;
using Lab.Client.Maui.Sync;
using Microsoft.Data.Sqlite;
namespace Lab.Client.Maui;
public partial class MainPage : ContentPage
{
    private readonly LocalDatabaseService database;
    private readonly SyncQueueStore syncQueue;
    public MainPage(LocalDatabaseService database, SyncQueueStore syncQueue)
    {
        InitializeComponent();
        this.database = database;
        this.syncQueue = syncQueue;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }
    private async void OnRefreshClicked(object? sender, EventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        try
        {
            SitesView.ItemsSource = await database.GetSitesAsync();
            var items = await syncQueue.GetAllAsync();
            var pending = items.Count(item => item.Status == SyncQueueStatus.Pending);
            var sent = items.Count(item => item.Status == SyncQueueStatus.Sent);
            var failed = items.Count(item => item.Status == SyncQueueStatus.Failed);
            SyncStatus.Text = $"Синхронізація — очікують: {pending}, відправлено: {sent}, помилки: {failed}";
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or JsonException or SqliteException)
        {
            SyncStatus.Text = $"Не вдалося відкрити локальні дані: {exception.Message}";
        }
    }
}
