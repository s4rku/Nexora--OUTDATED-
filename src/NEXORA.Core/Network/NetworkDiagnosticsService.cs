using Microsoft.Extensions.Logging;
using NEXORA.Core.Models;
using System.Net;
using System.Net.NetworkInformation;

namespace NEXORA.Core.Network;

/// <summary>
/// Performs safe network diagnostics: ping, jitter, DNS response time.
/// Does not modify any TCP/IP registry values or claim to reduce ping via "tweaks".
/// </summary>
public sealed class NetworkDiagnosticsService
{
    private readonly ILogger<NetworkDiagnosticsService> _logger;

    private static readonly string[] _pingTargets =
    {
        "8.8.8.8",        // Google DNS
        "1.1.1.1",        // Cloudflare DNS
        "208.67.222.222"  // OpenDNS
    };

    public NetworkDiagnosticsService(ILogger<NetworkDiagnosticsService> logger)
    {
        _logger = logger;
    }

    public async Task<NetworkDiagnostics> RunDiagnosticsAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new NetworkDiagnostics { TestedAt = DateTime.UtcNow };

        progress?.Report("Detecting network adapters…");
        DetectAdapter(result);

        progress?.Report("Measuring latency and jitter…");
        await MeasurePingAsync(result, ct);

        progress?.Report("Testing DNS response time…");
        await MeasureDnsAsync(result, ct);

        result.Recommendations = GenerateRecommendations(result);

        progress?.Report("Network diagnostics complete.");
        return result;
    }

    private static void DetectAdapter(NetworkDiagnostics result)
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                result.AdapterName = nic.Name;
                result.ConnectionType = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                    ? "Wi-Fi" : "Ethernet";

                var props = nic.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        result.IpAddress = addr.Address.ToString();
                        break;
                    }
                }
                break;
            }
        }
        catch { /* skip */ }
    }

    private async Task MeasurePingAsync(NetworkDiagnostics result, CancellationToken ct)
    {
        const int samples = 8;
        var times = new List<double>();

        using var ping = new Ping();
        foreach (var target in _pingTargets)
        {
            for (int i = 0; i < samples; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var reply = await ping.SendPingAsync(target, 2000);
                    if (reply.Status == IPStatus.Success)
                        times.Add(reply.RoundtripTime);
                    await Task.Delay(50, ct);
                }
                catch { /* host unreachable */ }
            }
            if (times.Count > 0) break; // Use first responsive target
        }

        if (times.Count > 0)
        {
            result.PingMs = times.Average();

            // Jitter = average deviation
            var avg = result.PingMs;
            result.JitterMs = times.Average(t => Math.Abs(t - avg));

            // Packet loss
            result.PacketLossPercent = (double)(samples - times.Count) / samples * 100;
        }
    }

    private static async Task MeasureDnsAsync(NetworkDiagnostics result, CancellationToken ct)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Dns.GetHostEntryAsync("www.google.com");
            sw.Stop();
            result.DnsResponseMs = sw.Elapsed.TotalMilliseconds;
        }
        catch
        {
            result.DnsResponseMs = -1; // DNS resolution failed
        }
    }

    private static List<string> GenerateRecommendations(NetworkDiagnostics d)
    {
        var recs = new List<string>();

        if (d.ConnectionType == "Wi-Fi")
            recs.Add("Using a wired Ethernet connection will provide lower and more consistent latency than Wi-Fi.");

        if (d.PacketLossPercent > 2)
            recs.Add($"Packet loss of {d.PacketLossPercent:F1}% detected. Check your network cable or Wi-Fi signal strength.");

        if (d.JitterMs > 20)
            recs.Add($"High jitter ({d.JitterMs:F1} ms) detected. Network congestion or a weak Wi-Fi signal may be the cause.");

        if (d.DnsResponseMs > 100)
            recs.Add("Slow DNS response detected. Switching to a faster DNS server (e.g., 1.1.1.1 or 8.8.8.8) may improve connection times.");

        if (d.PingMs < 20)
            recs.Add("Excellent ping. Your network connection looks healthy for gaming.");
        else if (d.PingMs > 80)
            recs.Add($"High base ping of {d.PingMs:F0} ms. This is determined by your ISP and physical distance to game servers — software optimizations cannot significantly reduce it.");

        if (recs.Count == 0)
            recs.Add("No network issues detected. Your connection looks healthy.");

        return recs;
    }
}
