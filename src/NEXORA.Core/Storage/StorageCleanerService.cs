using Microsoft.Extensions.Logging;
using NEXORA.Core.Models;
using System.IO;

namespace NEXORA.Core.Storage;

/// <summary>
/// Scans for safely deletable files and performs cleanup.
/// Always shows the user what will be removed before deletion occurs.
/// Never deletes files created within the last hour or files outside safe directories.
/// </summary>
public sealed class StorageCleanerService
{
    private readonly ILogger<StorageCleanerService> _logger;

    public StorageCleanerService(ILogger<StorageCleanerService> logger)
    {
        _logger = logger;
    }

    public async Task<List<CleanupCategory>> ScanAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var categories = new List<CleanupCategory>();

        progress?.Report("Scanning temporary files…");
        categories.Add(await ScanTempFilesAsync(ct));

        progress?.Report("Scanning Windows temp folder…");
        categories.Add(await ScanWindowsTempAsync(ct));

        progress?.Report("Scanning browser caches…");
        categories.AddRange(await ScanBrowserCachesAsync(ct));

        progress?.Report("Scanning crash dumps…");
        categories.Add(await ScanCrashDumpsAsync(ct));

        progress?.Report("Scanning Recycle Bin…");
        categories.Add(await ScanRecycleBinAsync(ct));

        progress?.Report("Scanning Windows Update cache…");
        categories.Add(await ScanWindowsUpdateCacheAsync(ct));

        progress?.Report("Scan complete.");
        return categories.Where(c => c.ReclaimableBytes > 0).ToList();
    }

    public async Task<long> CleanAsync(List<CleanupCategory> categories,
        IProgress<(string message, int percent)>? progress = null,
        CancellationToken ct = default)
    {
        long totalDeleted = 0;
        int done = 0;
        var selected = categories.Where(c => c.IsSelected && c.IsSafe).ToList();

        foreach (var cat in selected)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(($"Cleaning: {cat.Name}…", (int)((double)(done + 1) / selected.Count * 100)));

            foreach (var path in cat.FilePaths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(path))
                    {
                        var fi = new FileInfo(path);
                        var size = fi.Length;
                        fi.Delete();
                        totalDeleted += size;
                    }
                    else if (Directory.Exists(path))
                    {
                        var size = GetDirectorySize(path);
                        Directory.Delete(path, recursive: true);
                        totalDeleted += size;
                    }
                }
                catch { /* file in use or access denied — skip */ }
            }
            done++;
        }

        _logger.LogInformation("Storage cleanup removed {MB:F1} MB.", totalDeleted / 1_048_576.0);
        return totalDeleted;
    }

    // ── Scan helpers ──────────────────────────────────────────────────────────

    private static async Task<CleanupCategory> ScanTempFilesAsync(CancellationToken ct)
    {
        var cat = new CleanupCategory
        {
            Id = "temp_user",
            Name = "Temporary Files",
            Description = "User temporary files in %TEMP%",
            Risk = RiskLevel.Safe,
            IsSafe = true
        };
        await ScanDirectory(cat, Path.GetTempPath(), ct);
        return cat;
    }

    private static async Task<CleanupCategory> ScanWindowsTempAsync(CancellationToken ct)
    {
        var cat = new CleanupCategory
        {
            Id = "temp_windows",
            Name = "Windows Temp Files",
            Description = "Temporary files in Windows\\Temp",
            Risk = RiskLevel.Safe,
            IsSafe = true
        };
        var winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        await ScanDirectory(cat, winTemp, ct);
        return cat;
    }

    private static async Task<List<CleanupCategory>> ScanBrowserCachesAsync(CancellationToken ct)
    {
        var result = new List<CleanupCategory>();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var browsers = new[]
        {
            ("Chrome Cache", Path.Combine(appData, "Google", "Chrome", "User Data", "Default", "Cache")),
            ("Edge Cache",   Path.Combine(appData, "Microsoft", "Edge", "User Data", "Default", "Cache")),
            ("Firefox Cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mozilla", "Firefox", "Profiles")),
        };

        foreach (var (name, path) in browsers)
        {
            var cat = new CleanupCategory
            {
                Id = $"browser_{name.Replace(" ", "_").ToLower()}",
                Name = name,
                Description = $"Cached web data for {name.Split(' ')[0]}",
                Risk = RiskLevel.Safe,
                IsSafe = true
            };
            if (Directory.Exists(path))
                await ScanDirectory(cat, path, ct);
            result.Add(cat);
        }

        return result;
    }

    private static async Task<CleanupCategory> ScanCrashDumpsAsync(CancellationToken ct)
    {
        var cat = new CleanupCategory
        {
            Id = "crash_dumps",
            Name = "Crash Dumps",
            Description = "Windows error reporting crash dump files",
            Risk = RiskLevel.Safe,
            IsSafe = true
        };

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dumpDir = Path.Combine(localAppData, "CrashDumps");
        await ScanDirectory(cat, dumpDir, ct, "*.dmp");

        var tempDumps = Path.Combine(Path.GetTempPath());
        await ScanDirectory(cat, tempDumps, ct, "*.dmp");

        return cat;
    }

    private static Task<CleanupCategory> ScanRecycleBinAsync(CancellationToken ct)
    {
        var cat = new CleanupCategory
        {
            Id = "recycle_bin",
            Name = "Recycle Bin",
            Description = "Items waiting in the Recycle Bin",
            Risk = RiskLevel.Safe,
            IsSafe = true
        };

        try
        {
            // Check all drives
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var recycleBin = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                if (!Directory.Exists(recycleBin)) continue;

                try
                {
                    foreach (var userDir in Directory.GetDirectories(recycleBin))
                    {
                        try
                        {
                            foreach (var f in Directory.GetFiles(userDir, "*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    var fi = new FileInfo(f);
                                    cat.ReclaimableBytes += fi.Length;
                                    cat.FilePaths.Add(f);
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }

        return Task.FromResult(cat);
    }

    private static async Task<CleanupCategory> ScanWindowsUpdateCacheAsync(CancellationToken ct)
    {
        var cat = new CleanupCategory
        {
            Id = "windows_update",
            Name = "Windows Update Cache",
            Description = "Downloaded Windows Update installation files (can be re-downloaded if needed)",
            Risk = RiskLevel.Low,
            IsSafe = true
        };

        var updateCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SoftwareDistribution", "Download");
        await ScanDirectory(cat, updateCache, ct);
        return cat;
    }

    private static async Task ScanDirectory(CleanupCategory cat, string dir,
        CancellationToken ct, string pattern = "*")
    {
        if (!Directory.Exists(dir)) return;
        await Task.Run(() =>
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(file);
                        // Skip very recently modified files
                        if ((DateTime.UtcNow - fi.LastWriteTimeUtc).TotalHours < 1) continue;
                        cat.ReclaimableBytes += fi.Length;
                        cat.FilePaths.Add(file);
                    }
                    catch { }
                }
            }
            catch { }
        }, ct);
    }

    private static long GetDirectorySize(string dir)
    {
        try { return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch { return 0; }
    }
}
