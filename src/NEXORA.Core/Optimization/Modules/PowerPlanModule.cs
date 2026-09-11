using Microsoft.Extensions.Logging;
using NEXORA.Core.Models;

namespace NEXORA.Core.Optimization.Modules;

/// <summary>
/// Switches the active Windows power plan to High Performance.
/// On laptops: only suggests this when plugged in.
/// Never modifies the power plan values themselves — only switches between existing plans.
/// </summary>
public sealed class PowerPlanModule : BaseOptimizationModule
{
    // GUIDs for built-in Windows power plans
    private const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private string? _previousGuid;

    public PowerPlanModule(ILogger<PowerPlanModule> logger) : base(logger) { }

    public override string Id => "windows.power_plan";
    public override string Name => "Switch to High Performance Power Plan";
    public override string Description =>
        "Activates the built-in Windows High Performance power plan. " +
        "This prevents the CPU from reducing its clock speed during gaming, " +
        "reducing micro-stutters caused by power-state transitions.";
    public override string Category => "Power";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string ExpectedBenefit =>
        "Eliminates CPU clock ramp-up latency during gaming. Improves frame consistency.";
    public override OptimizationLevel MinimumLevel => OptimizationLevel.Safe;

    public override async Task<bool> CanApplyAsync(HardwareInfo hardware, CancellationToken ct = default)
    {
        // On battery: don't recommend High Performance
        if (hardware.IsOnBattery) return false;

        var current = await GetActiveGuidAsync(ct);
        return current != null && !current.Equals(HighPerfGuid, StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<string> GetCurrentValueAsync(CancellationToken ct = default)
    {
        var output = await RunPowercfgAsync("/getactivescheme", ct);
        if (output.Contains("High performance", StringComparison.OrdinalIgnoreCase)) return "High Performance";
        if (output.Contains("Balanced", StringComparison.OrdinalIgnoreCase)) return "Balanced";
        if (output.Contains("Power saver", StringComparison.OrdinalIgnoreCase)) return "Power Saver";
        return output.Trim();
    }

    public override async Task<AppliedOptimization> ApplyAsync(CancellationToken ct = default)
    {
        _previousGuid = await GetActiveGuidAsync(ct) ?? BalancedGuid;
        var previousName = await GetCurrentValueAsync(ct);

        var result = await RunPowercfgAsync($"/setactive {HighPerfGuid}", ct);
        var success = string.IsNullOrWhiteSpace(result) || !result.Contains("error", StringComparison.OrdinalIgnoreCase);

        if (success)
            Logger.LogInformation("Power plan set to High Performance. Previous GUID: {Prev}", _previousGuid);
        else
            Logger.LogWarning("Failed to set power plan. Output: {Out}", result);

        return BuildRecord(
            previousName,
            "High Performance",
            success,
            success ? null : $"powercfg output: {result}",
            $"{{\"previousGuid\":\"{_previousGuid}\"}}");
    }

    public override async Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        var guid = await GetActiveGuidAsync(ct);
        return guid != null && guid.Equals(HighPerfGuid, StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<bool> RollbackAsync(AppliedOptimization record, CancellationToken ct = default)
    {
        // Extract previous GUID from rollback data
        string guid = BalancedGuid;
        try
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(record.RollbackData);
            if (data != null && data.TryGetValue("previousGuid", out var pg))
                guid = pg;
        }
        catch { /* use default */ }

        var result = await RunPowercfgAsync($"/setactive {guid}", ct);
        return string.IsNullOrWhiteSpace(result) || !result.Contains("error", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> GetActiveGuidAsync(CancellationToken ct)
    {
        var output = await RunPowercfgAsync("/getactivescheme", ct);
        // Output: "Power Scheme GUID: xxxxxxxx-... (Name)"
        var start = output.IndexOf("GUID:");
        if (start < 0) return null;
        var rest = output[(start + 5)..].Trim();
        var end = rest.IndexOf(' ');
        return end >= 0 ? rest[..end].Trim() : rest.Trim();
    }

    private static Task<string> RunPowercfgAsync(string args, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("powercfg", args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return string.Empty;
                proc.WaitForExit(5000);
                return proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
            }
            catch { return string.Empty; }
        }, ct);
    }
}
