using Lab.Client.Maui.Data;
using Lab.Client.Maui.Sync;
using Lab.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lab.Client.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "lab.sqlite3");
        builder.Configuration["Database:Provider"] = "Sqlite";
        builder.Configuration["ConnectionStrings:Sqlite"] = $"Data Source={databasePath}";

        builder.Services.AddLabInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<LocalDatabaseService>();
        builder.Services.AddSingleton(_ => new SyncQueueStore(
            Path.Combine(FileSystem.AppDataDirectory, "sync-queue.json")));
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
