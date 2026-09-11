using Microsoft.Extensions.DependencyInjection;
using NEXORA.Core.AI;
using NEXORA.Core.Database;
using NEXORA.Core.GameDetection;
using NEXORA.Core.Hardware;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Network;
using NEXORA.Core.Optimization;
using NEXORA.Core.Optimization.Modules;
using NEXORA.Core.Restore;
using NEXORA.Core.Storage;
using NEXORA.Core.SystemTools;

namespace NEXORA.Core;

/// <summary>
/// Extension method that registers all NEXORA.Core services with the DI container.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddNexoraCore(this IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<IHardwareService, HardwareService>();
        services.AddSingleton<IGameDetectionService, GameDetectionService>();
        services.AddSingleton<IRestoreService, RestoreService>();

        // Optimization modules — each is a singleton so state (previousValue) is preserved
        services.AddSingleton<IOptimizationModule, VisualEffectsModule>();
        services.AddSingleton<IOptimizationModule, PowerPlanModule>();
        services.AddSingleton<IOptimizationModule, GameModeModule>();
        services.AddSingleton<IOptimizationModule, HardwareAcceleratedGpuSchedulingModule>();
        services.AddSingleton<IOptimizationModule, MouseInputModule>();
        services.AddSingleton<IOptimizationModule, NvidiaPreferMaxPerformanceModule>();
        services.AddSingleton<IOptimizationModule, TempFilesCleanupModule>();

        // Optimization engine
        services.AddSingleton<IOptimizationEngine, OptimizationEngine>();

        // System tools
        services.AddSingleton<StartupManagerService>();
        services.AddSingleton<ProcessMonitorService>();
        services.AddSingleton<StorageCleanerService>();
        services.AddSingleton<NetworkDiagnosticsService>();

        // AI Advisor
        services.AddSingleton<NexoraAIAdvisor>();

        return services;
    }
}
