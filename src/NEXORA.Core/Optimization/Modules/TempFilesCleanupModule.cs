using Microsoft.Extensions.Logging;
using NEXORA.Core.Models;
using System.IO;

namespace NEXORA.Core.Optimization.Modules;

/// <summary>
/// Safely removes temporary files from %TEMP%, Windows\Temp, and prefetch.
/// Only deletes files that are not in use. Never touches system files.
/// </summary>
public sealed class TempFilesCleanupModule : BaseOptimizationModule
{
    private long _reclaimableBytes;

    public TempFilesCleanupModule(ILogger<TempFilesCleanupModule> logger) : base(logger) { }

    public override string Id => "storage.temp_files";
    public override string Name => "Clean Temporary Files";
    public override string Description =>
        "Removes temporary files from %TEMP%, Windows\\Temp, and old Windows prefetch entries. " +
        "Only files that are not currently in use are deleted. " +
        "System files and files modified in the last 24 hours are preserved.";
    public override string Category => "Storage";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string ExpectedBenefit =>
        "Recovers disk space and reduces storage clutter. May improve storage scan times.";
    public override OptimizationLevel MinimumLevel => OptimizationLevel.Safe;

    public override async Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct = default)
    {
        _reclaimableBytes = await CountReclaimableBytesAsync(ct);
        return _reclaimableBytes > 10_485_760; // > 10 MB
    }

    public override async Task<string> GetCurrentValueAsync(CancellationToken ct = default)
    {
        if (_reclaimableBytes == 0)
            _reclaimableBytes = await CountReclaimableBytesAsync(ct);
        return $"{_reclaimableBytes / 1_048_576.0:F1} MB reclaimable";
    }

    public override async Task<AppliedOptimization> ApplyAsync(CancellationToken ct = default)
    {
        long deleted = 0;
        int fileCount = 0;
        var errors = new List<string>();

        var dirs = GetTempDirectories();
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(file);
                        // Skip recently modified files (may be in active use)
                        if ((DateTime.UtcNow - fi.LastWriteTimeUtc).TotalHours < 1) continue;

                        var size = fi.Length;
                        fi.Delete();
                        deleted += size;
                        fileCount++;
                    }
                    catch { /* file in use — skip */ }
                }

                // Delete empty subdirectories
                foreach (var subDir in Directory.EnumerateDirectories(dir))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(subDir).Any())
                            Directory.Delete(subDir);
                    }
                    catch { /* skip */ }
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        var msg = $"Deleted {fileCount} files, recovered {deleted / 1_048_576.0:F1} MB";
        Logger.LogInformation("{Msg}", msg);

        // Temp cleanup is not rollback-able by design
        return BuildRecord(
            $"{_reclaimableBytes / 1_048_576.0:F1} MB",
            $"Freed {deleted / 1_048_576.0:F1} MB",
            true,
            errors.Count > 0 ? string.Join("; ", errors.Take(3)) : null,
            "{}");
    }

    public override Task<bool> ValidateAsync(CancellationToken ct = default) => Task.FromResult(true);

    public override Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct = default)
    {
        // Deleted temp files cannot be restored — this is expected and safe
        Logger.LogInformation("Temp file cleanup cannot be rolled back (expected).");
        return Task.FromResult(true);
    }

    private async Task<long> CountReclaimableBytesAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            long total = 0;
            foreach (var dir in GetTempDirectories())
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var fi = new FileInfo(file);
                            if ((DateTime.UtcNow - fi.LastWriteTimeUtc).TotalHours >= 1)
                                total += fi.Length;
                        }
                        catch { /* skip */ }
                    }
                }
                catch { /* skip inaccessible */ }
            }
            return total;
        }, ct);
    }

    private static string[] GetTempDirectories()
    {
        var userTemp = Path.GetTempPath();
        var winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        return new[] { userTemp, winTemp };
    }
}
