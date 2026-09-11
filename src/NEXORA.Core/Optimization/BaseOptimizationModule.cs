using Microsoft.Extensions.Logging;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;

namespace NEXORA.Core.Optimization;

/// <summary>
/// Base class providing common helpers for all optimization modules.
/// Subclasses must override the abstract members.
/// </summary>
public abstract class BaseOptimizationModule : IOptimizationModule
{
    protected readonly ILogger Logger;

    protected BaseOptimizationModule(ILogger logger)
    {
        Logger = logger;
    }

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }
    public abstract RiskLevel Risk { get; }
    public abstract string ExpectedBenefit { get; }
    public virtual OptimizationLevel MinimumLevel => OptimizationLevel.Safe;

    public abstract Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct = default);
    public abstract Task<string> GetCurrentValueAsync(CancellationToken ct = default);
    public abstract Task<AppliedOptimization> ApplyAsync(CancellationToken ct = default);
    public abstract Task<bool> ValidateAsync(CancellationToken ct = default);
    public abstract Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct = default);

    // ── Registry helpers ──────────────────────────────────────────────────

    protected static string? ReadRegistryString(Microsoft.Win32.RegistryHive hive, string keyPath, string valueName)
    {
        try
        {
            using var root = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Registry64);
            using var key = root.OpenSubKey(keyPath, writable: false);
            return key?.GetValue(valueName)?.ToString();
        }
        catch { return null; }
    }

    protected static bool WriteRegistryString(Microsoft.Win32.RegistryHive hive, string keyPath, string valueName, string value)
    {
        try
        {
            using var root = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Registry64);
            using var key = root.OpenSubKey(keyPath, writable: true) ?? root.CreateSubKey(keyPath);
            key?.SetValue(valueName, value, Microsoft.Win32.RegistryValueKind.String);
            return true;
        }
        catch { return false; }
    }

    protected static bool WriteRegistryDword(Microsoft.Win32.RegistryHive hive, string keyPath, string valueName, int value)
    {
        try
        {
            using var root = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Registry64);
            using var key = root.OpenSubKey(keyPath, writable: true) ?? root.CreateSubKey(keyPath);
            key?.SetValue(valueName, value, Microsoft.Win32.RegistryValueKind.DWord);
            return true;
        }
        catch { return false; }
    }

    protected static int? ReadRegistryDword(Microsoft.Win32.RegistryHive hive, string keyPath, string valueName)
    {
        try
        {
            using var root = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Registry64);
            using var key = root.OpenSubKey(keyPath, writable: false);
            if (key?.GetValue(valueName) is int v) return v;
            return null;
        }
        catch { return null; }
    }

    protected AppliedOptimization BuildRecord(string previousValue, string newValue,
        bool successful, string? error = null, string? rollbackData = null)
    {
        return new AppliedOptimization
        {
            OptimizationId = Id,
            Name = Name,
            Category = Category,
            Risk = Risk,
            PreviousValue = previousValue,
            NewValue = newValue,
            WasSuccessful = successful,
            ErrorMessage = error,
            RollbackData = rollbackData ?? string.Empty
        };
    }
}
