using Microsoft.Extensions.Logging;
using NEXORA.Core.Models;
using System.Diagnostics;

namespace NEXORA.Core.SystemTools;

/// <summary>
/// Monitors running processes and reports resource usage.
/// Never automatically terminates processes.
/// </summary>
public sealed class ProcessMonitorService
{
    private readonly ILogger<ProcessMonitorService> _logger;

    private static readonly HashSet<string> _criticalSystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Registry", "smss", "csrss", "wininit", "winlogon", "lsass",
        "services", "svchost", "dwm", "explorer", "fontdrvhost", "LogonUI",
        "SecurityHealthService", "MsMpEng", "NisSrv"
    };

    public ProcessMonitorService(ILogger<ProcessMonitorService> logger)
    {
        _logger = logger;
    }

    public async Task<List<ProcessEntry>> GetProcessesAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var result = new List<ProcessEntry>();
            var procs = Process.GetProcesses();

            foreach (var proc in procs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var entry = new ProcessEntry
                    {
                        Pid = proc.Id,
                        Name = proc.ProcessName,
                        IsCriticalSystem = _criticalSystemProcesses.Contains(proc.ProcessName)
                    };

                    try
                    {
                        entry.MemoryBytes = proc.WorkingSet64;
                        entry.ExecutablePath = proc.MainModule?.FileName ?? string.Empty;
                        entry.Priority = proc.PriorityClass;

                        if (!string.IsNullOrEmpty(entry.ExecutablePath))
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(entry.ExecutablePath);
                            entry.Description = fvi.FileDescription ?? string.Empty;
                            entry.Publisher = fvi.CompanyName ?? string.Empty;
                        }
                    }
                    catch { /* elevated process — limited info */ }

                    result.Add(entry);
                }
                catch { /* process may have exited */ }
                finally
                {
                    proc.Dispose();
                }
            }

            return result
                .OrderByDescending(p => p.MemoryBytes)
                .ToList();
        }, ct);
    }

    public async Task<bool> SetPriorityAsync(int pid, ProcessPriorityClass priority, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                if (_criticalSystemProcesses.Contains(proc.ProcessName))
                {
                    _logger.LogWarning("Refused to change priority of critical process {Name}.", proc.ProcessName);
                    return false;
                }
                proc.PriorityClass = priority;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set priority for PID {Pid}.", pid);
                return false;
            }
        }, ct);
    }
}
