using GolfGrind.Core.Abstractions;
using GolfGrind.Core.Services;
using GolfGrind.App.Platforms.Windows;
using GolfGrind.App.Services;

namespace GolfGrind.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        builder.Services.AddSingleton<AppSettingsService>();
        builder.Services.AddSingleton<MockLaunchMonitor>();
        builder.Services.AddSingleton<GolferProfileService>();
        builder.Services.AddSingleton<GolfBagService>();
        builder.Services.AddSingleton<SessionStorageService>();
        builder.Services.AddSingleton<ActivitySessionCoordinator>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddSingleton<PracticeGameEngine>();
        builder.Services.AddSingleton<LaunchMonitorCoordinator>();
        builder.Services.AddSingleton<LaunchMonitorWorkspace>();
        builder.Services.AddSingleton<IDisplayWakeService, WindowsDisplayWakeService>();
        builder.Services.AddSingleton<SquareLaunchMonitor>();
        builder.Services.AddSingleton<ILaunchMonitor>(sp => sp.GetRequiredService<SquareLaunchMonitor>());

        return builder.Build();
    }
}
