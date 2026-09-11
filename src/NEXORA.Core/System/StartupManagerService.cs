using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NEXORA.Core.Models;
using System.Diagnostics;
using System.IO;

namespace NEXORA.Core.SystemTools;

/// <summary>
/// Reads and manages startup applications from registry and startup folders.
/// Never disables critical Windows components automatically.
/// </summary>
public sealed class StartupManagerService
{
    private readonly ILogger<StartupManagerService> _logger;

    private static readonly HashSet<string> _criticalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SecurityHealthSystray", "WindowsDefender", "MsMpEng",
        "OneDrive", "ctfmon", "dwm", "winlogon", "lsass", "svchost"
    };

    public StartupManagerService(ILogger<StartupManagerService> logger)
    {
        _logger = logger;
    }

    public async Task<List<StartupEntry>> GetStartupEntriesAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var entries = new List<StartupEntry>();

            // HKCU Run
            ReadRegistryStartup(entries, Registry.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StartupLocation.HKCU_Run);

            // HKLM Run
            ReadRegistryStartup(entries, Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StartupLocation.HKLM_Run);
            ReadRegistryStartup(entries, Registry.LocalMachine,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", StartupLocation.HKLM_Run);

            // Startup folders
            ReadStartupFolder(entries,
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                StartupLocation.StartupFolder);
            ReadStartupFolder(entries,
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                StartupLocation.StartupFolder);

            return entries
                .DistinctBy(e => e.ExecutablePath, StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e.Impact)
                .ToList();
        }, ct);
    }

    public async Task<bool> SetEnabledAsync(StartupEntry entry, bool enabled, CancellationToken ct = default)
    {
        if (entry.IsCriticalSystem)
        {
            _logger.LogWarning("Refused to modify critical system startup entry: {Name}", entry.Name);
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                if (entry.Location == StartupLocation.HKCU_Run || entry.Location == StartupLocation.HKLM_Run)
                {
                    var hive = entry.Location == StartupLocation.HKCU_Run
                        ? Registry.CurrentUser : Registry.LocalMachine;
                    var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

                    if (!enabled)
                    {
                        // Move to disabled key (preserves the value for re-enabling)
                        var disabledKeyPath = keyPath.Replace(@"\Run", @"\Run\Disabled");
                        using var src = hive.OpenSubKey(keyPath, writable: true);
                        var val = src?.GetValue(entry.Name)?.ToString();
                        if (val != null)
                        {
                            using var dst = hive.CreateSubKey(disabledKeyPath);
                            dst.SetValue(entry.Name, val);
                            src?.DeleteValue(entry.Name, throwOnMissingValue: false);
                        }
                    }
                    else
                    {
                        // Move back from disabled key
                        var disabledKeyPath = keyPath.Replace(@"\Run", @"\Run\Disabled");
                        using var src = hive.OpenSubKey(disabledKeyPath, writable: true);
                        var val = src?.GetValue(entry.Name)?.ToString();
                        if (val != null)
                        {
                            using var dst = hive.OpenSubKey(keyPath, writable: true);
                            dst?.SetValue(entry.Name, val);
                            src?.DeleteValue(entry.Name, throwOnMissingValue: false);
                        }
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set startup entry {Name} enabled={Enabled}", entry.Name, enabled);
                return false;
            }
        }, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void ReadRegistryStartup(List<StartupEntry> list,
        RegistryKey hive, string keyPath, StartupLocation location)
    {
        try
        {
            using var key = hive.OpenSubKey(keyPath, writable: false);
            if (key == null) return;

            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName)?.ToString() ?? string.Empty;
                var exePath = ExtractExePath(value);
                var entry = new StartupEntry
                {
                    Name = valueName,
                    ExecutablePath = exePath,
                    RegistryKey = keyPath,
                    Location = location,
                    IsEnabled = true,
                    Publisher = GetPublisher(exePath),
                    IsCriticalSystem = _criticalProcesses.Contains(valueName)
                };
                entry.Impact = EstimateImpact(exePath);
                list.Add(entry);
            }
        }
        catch { /* inaccessible key */ }
    }

    private static void ReadStartupFolder(List<StartupEntry> list, string folder, StartupLocation location)
    {
        if (!Directory.Exists(folder)) return;
        try
        {
            foreach (var file in Directory.GetFiles(folder, "*.lnk"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                list.Add(new StartupEntry
                {
                    Name = name,
                    ExecutablePath = file,
                    Location = location,
                    IsEnabled = true,
                    Impact = StartupImpact.Medium
                });
            }
        }
        catch { /* skip */ }
    }

    private static string ExtractExePath(string value)
    {
        var trimmed = value.Trim('"', ' ');
        var spaceIdx = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (spaceIdx >= 0) return trimmed[..(spaceIdx + 4)];
        return trimmed;
    }

    private static string GetPublisher(string exePath)
    {
        try
        {
            if (File.Exists(exePath))
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                return info.CompanyName ?? string.Empty;
            }
        }
        catch { /* skip */ }
        return string.Empty;
    }

    private static StartupImpact EstimateImpact(string exePath)
    {
        if (string.IsNullOrEmpty(exePath)) return StartupImpact.Low;
        try
        {
            var fi = new FileInfo(exePath);
            if (!fi.Exists) return StartupImpact.Low;
            // Rough heuristic: larger executables tend to take longer to start
            return fi.Length switch
            {
                > 50_000_000 => StartupImpact.High,
                > 10_000_000 => StartupImpact.Medium,
                _ => StartupImpact.Low
            };
        }
        catch { return StartupImpact.Low; }
    }
}
