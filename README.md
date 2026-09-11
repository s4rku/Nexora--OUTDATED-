# NEXORA — Intelligent PC & Game Performance Optimizer

A production-quality Windows desktop application for PC and game performance optimization.

---

## Requirements

| Requirement | Version |
|---|---|
| .NET SDK | 8.0 or later |
| Windows App SDK | 1.5 |
| Visual Studio | 2022 17.8+ (recommended) |
| Windows | 10 (19041+) or 11 |
| Architecture | x64 only |

Install the .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8

---

## Building

```
# Restore packages
dotnet restore NEXORA.sln

# Build (Release)
dotnet build NEXORA.sln -c Release -p:Platform=x64

# Run the app
dotnet run --project src/NEXORA/NEXORA.csproj -c Release

# Run tests
dotnet test tests/NEXORA.Tests/NEXORA.Tests.csproj
```

---

## Project Structure

```
NEXORA/
├── src/
│   ├── NEXORA.Core/              # Business logic, services, models
│   │   ├── Models/               # HardwareInfo, OptimizationModels, GameModels, SystemModels
│   │   ├── Interfaces/           # IOptimizationModule, IHardwareService, IRestoreService, etc.
│   │   ├── Hardware/             # HardwareService (WMI + LibreHardwareMonitor)
│   │   ├── GameDetection/        # Steam, Epic, GOG, registry scanner
│   │   ├── Optimization/         # OptimizationEngine + all modules
│   │   │   └── Modules/          # VisualEffects, PowerPlan, GameMode, HAGS, Mouse, NVIDIA, TempFiles
│   │   ├── AI/                   # NexoraAIAdvisor
│   │   ├── Database/             # SQLite via Microsoft.Data.Sqlite
│   │   ├── Restore/              # RestoreService (session persistence + rollback)
│   │   ├── System/               # StartupManager, ProcessMonitor
│   │   ├── Storage/              # StorageCleaner
│   │   └── Network/              # NetworkDiagnostics
│   │
│   └── NEXORA/                   # WinUI 3 application
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / .cs
│       ├── Styles/               # Colors, Typography, Controls XAML
│       ├── ViewModels/           # MVVM ViewModels (CommunityToolkit.Mvvm)
│       └── Views/                # Pages for all navigation items
│
└── tests/
    └── NEXORA.Tests/             # xUnit tests
        ├── OptimizationEngineTests.cs
        ├── AIAdvisorTests.cs
        └── HardwareScoringTests.cs
```

---

## Architecture: DETECT → ANALYZE → RECOMMEND → OPTIMIZE → MEASURE → VERIFY → ROLLBACK

Every optimization module implements `IOptimizationModule`:

```csharp
interface IOptimizationModule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    RiskLevel Risk { get; }
    string ExpectedBenefit { get; }
    OptimizationLevel MinimumLevel { get; }

    Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct);
    Task<string> GetCurrentValueAsync(CancellationToken ct);
    Task<AppliedOptimization> ApplyAsync(CancellationToken ct);
    Task<bool> ValidateAsync(CancellationToken ct);
    Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct);
}
```

---

## Optimization Modules

| Module | Category | Risk | Min Level |
|---|---|---|---|
| Visual Effects | Windows | Safe | Safe |
| Power Plan (High Performance) | Power | Safe | Safe |
| Windows Game Mode | Gaming | Safe | Safe |
| Hardware-Accelerated GPU Scheduling | Gaming | Low | Balanced |
| Disable Mouse Acceleration | Gaming | Safe | Balanced |
| NVIDIA Max Performance | Gaming | Low | Balanced |
| Temp Files Cleanup | Storage | Safe | Safe |

---

## Safety Principles

- **Never disables** Windows Defender, Windows Update, or the firewall
- **Never deletes** critical system files
- **Every change is logged** in a local SQLite database
- **Every change can be rolled back** individually or as a session
- **Creates a Windows restore point** before applying optimizations
- **Validates** each change after applying it
- **One module failure never affects** other modules

---

## Navigation

| Page | Description |
|---|---|
| Dashboard | Hardware summary, NEXORA Score, live metrics, Optimize Now |
| PC Optimizer | Intelligent optimization engine with Safe/Balanced/Performance levels |
| Game Optimizer | Per-game analysis, bottleneck detection, settings recommendations |
| Game Library | Auto-detects Steam, Epic, GOG, and standalone games |
| Performance | Live CPU/GPU/RAM/disk monitoring |
| Benchmark | Before/after performance snapshot comparison |
| Startup Manager | View and disable startup applications safely |
| Process Manager | View running processes with resource usage |
| Storage Cleaner | Scan and safely remove temporary files |
| Network | Ping, jitter, packet loss, DNS diagnostics |
| Restore Center | Full rollback history — restore any session |
| NEXORA AI | Plain-language performance advisor based on real system data |
| Settings | Telemetry, auto-launch, monitoring preferences |

---

## Database

All data is stored locally at:
```
%LOCALAPPDATA%\NEXORA\nexora.db
```

No data is transmitted to any external server unless telemetry is explicitly enabled.

---

## Adding a New Optimization Module

1. Create a class in `src/NEXORA.Core/Optimization/Modules/`
2. Extend `BaseOptimizationModule`
3. Implement all `IOptimizationModule` members
4. Register it in `ServiceRegistration.cs`:
   ```csharp
   services.AddSingleton<IOptimizationModule, YourNewModule>();
   ```

The engine picks it up automatically.
