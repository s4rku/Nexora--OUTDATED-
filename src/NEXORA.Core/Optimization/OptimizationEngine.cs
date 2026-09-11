using Microsoft.Extensions.Logging;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;

namespace NEXORA.Core.Optimization;

/// <summary>
/// Central optimization coordinator.
/// Follows the DETECT → ANALYZE → RECOMMEND → OPTIMIZE → MEASURE → VERIFY → ROLLBACK lifecycle.
/// All modules are independent; a failure in one never affects others.
/// </summary>
public sealed class OptimizationEngine : IOptimizationEngine
{
    private readonly ILogger<OptimizationEngine> _logger;
    private readonly IRestoreService _restore;
    private readonly List<IOptimizationModule> _modules;

    public IReadOnlyList<IOptimizationModule> AllModules => _modules;

    public OptimizationEngine(
        ILogger<OptimizationEngine> logger,
        IRestoreService restore,
        IEnumerable<IOptimizationModule> modules)
    {
        _logger = logger;
        _restore = restore;
        _modules = modules.ToList();
        _logger.LogInformation("OptimizationEngine initialized with {Count} modules.", _modules.Count);
    }

    // ── Analyze ──────────────────────────────────────────────────────────────

    public async Task<List<OptimizationDescriptor>> AnalyzeAsync(
        OptimizationLevel level,
        HardwareInfo hardware,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new List<OptimizationDescriptor>();
        int i = 0;

        foreach (var module in _modules)
        {
            ct.ThrowIfCancellationRequested();
            i++;
            progress?.Report($"Checking: {module.Name}… ({i}/{_modules.Count})");

            try
            {
                // Skip modules above the selected level
                if (module.MinimumLevel > level) continue;

                bool applicable = await module.CanApplyAsync(hardware, ct);
                if (!applicable) continue;

                var currentValue = await module.GetCurrentValueAsync(ct);

                result.Add(new OptimizationDescriptor
                {
                    Id = module.Id,
                    Name = module.Name,
                    Description = module.Description,
                    Category = module.Category,
                    Risk = module.Risk,
                    ExpectedBenefit = module.ExpectedBenefit,
                    CurrentValue = currentValue,
                    IsRecommended = true,
                    IsSelected = true,
                    MinimumLevel = module.MinimumLevel
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing module {Name}.", module.Name);
            }
        }

        progress?.Report($"Analysis complete. {result.Count} optimizations available.");
        return result;
    }

    // ── Apply ────────────────────────────────────────────────────────────────

    public async Task<OptimizationSession> ApplyAsync(
        List<OptimizationDescriptor> selected,
        OptimizationLevel level,
        IProgress<(string message, int percent)>? progress = null,
        CancellationToken ct = default)
    {
        var session = new OptimizationSession
        {
            AppliedAt = DateTime.UtcNow,
            Level = level,
            ProfileName = level.ToString()
        };

        // Create a system restore point before making changes
        progress?.Report(("Creating restore point…", 2));
        try
        {
            await _restore.CreateSystemRestorePointAsync("NEXORA optimization — before changes", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create system restore point (may require elevated rights).");
        }

        int total = selected.Count;
        int done = 0;

        foreach (var descriptor in selected)
        {
            ct.ThrowIfCancellationRequested();

            var module = _modules.FirstOrDefault(m => m.Id == descriptor.Id);
            if (module == null)
            {
                _logger.LogWarning("Module {Id} not found during apply.", descriptor.Id);
                continue;
            }

            int percent = (int)((double)(done + 1) / total * 90) + 5;
            progress?.Report(($"Applying: {module.Name}…", percent));

            AppliedOptimization? applied = null;
            try
            {
                applied = await module.ApplyAsync(ct);
                applied.SessionId = session.Id;

                // Verify the change was actually applied
                bool valid = await module.ValidateAsync(ct);
                if (!valid && applied.WasSuccessful)
                {
                    applied.WasSuccessful = false;
                    applied.ErrorMessage = "Validation failed after apply.";
                    _logger.LogWarning("Validation failed for {Name}.", module.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception applying module {Name}.", module.Name);
                applied ??= new AppliedOptimization
                {
                    OptimizationId = module.Id,
                    Name = module.Name,
                    Category = module.Category,
                    Risk = module.Risk,
                    WasSuccessful = false,
                    ErrorMessage = ex.Message
                };
            }

            session.AppliedOptimizations.Add(applied);
            done++;
        }

        progress?.Report(("Saving session…", 97));
        try
        {
            await _restore.SaveSessionAsync(session, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save optimization session.");
        }

        progress?.Report(("Done.", 100));
        return session;
    }
}
