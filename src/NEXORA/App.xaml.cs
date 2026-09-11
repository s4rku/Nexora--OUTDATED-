using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using NEXORA.Core;
using NEXORA.Core.Database;
using NEXORA.ViewModels;
using System.IO;

namespace NEXORA;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow? MainWindow { get; private set; }

    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NEXORA", "startup.log");

    public App()
    {
        this.UnhandledException += (s, e) =>
        {
            Log($"UnhandledException: {e.Exception}");
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            Log($"DomainException: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log($"TaskException: {e.Exception}");
            e.SetObserved();
        };

        InitializeComponent();

        try
        {
            Services = ConfigureServices();
            Log("DI container built OK.");
        }
        catch (Exception ex)
        {
            Log($"FATAL in ConfigureServices: {ex}");
            throw;
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddNexoraCore();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PcOptimizerViewModel>();
        services.AddTransient<GameLibraryViewModel>();
        services.AddTransient<GameOptimizerViewModel>();
        services.AddTransient<BenchmarkViewModel>();
        services.AddTransient<StartupManagerViewModel>();
        services.AddTransient<ProcessManagerViewModel>();
        services.AddTransient<StorageCleanerViewModel>();
        services.AddTransient<NetworkViewModel>();
        services.AddTransient<RestoreCenterViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AIAdvisorViewModel>();
        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log("OnLaunched called.");
        try
        {
            // Initialize DB synchronously to avoid async/await off-thread issues
            var db = Services.GetRequiredService<DatabaseService>();
            db.InitializeAsync().GetAwaiter().GetResult();
            Log("Database initialized.");

            MainWindow = new MainWindow();
            MainWindow.Activate();
            Log("MainWindow activated.");

            // Kick off non-critical async work after the window is registered
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // let the window settle
            });
        }
        catch (Exception ex)
        {
            Log($"FATAL in OnLaunched: {ex}");
        }
    }

    internal static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath,
                $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch { }
    }
}
