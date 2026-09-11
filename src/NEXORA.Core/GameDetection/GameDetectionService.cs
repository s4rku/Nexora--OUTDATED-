using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NEXORA.Core.Interfaces;
using NEXORA.Core.Models;
using System.IO;

namespace NEXORA.Core.GameDetection;

/// <summary>
/// Scans all installed game launchers and the Windows Uninstall registry
/// to build a complete library of detected games.
/// </summary>
public sealed class GameDetectionService : IGameDetectionService
{
    private readonly ILogger<GameDetectionService> _logger;

    public GameDetectionService(ILogger<GameDetectionService> logger)
        => _logger = logger;

    public async Task<List<GameStore>> GetInstalledStoresAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var stores = new List<GameStore>();
            if (SteamDetector.IsSteamInstalled())       stores.Add(GameStore.Steam);
            if (EpicDetector.IsInstalled())              stores.Add(GameStore.EpicGames);
            if (RiotDetector.IsInstalled())              stores.Add(GameStore.RiotGames);
            if (RockstarDetector.IsInstalled())          stores.Add(GameStore.Standalone);
            if (GogDetector.IsInstalled())               stores.Add(GameStore.GOG);
            return stores;
        }, ct);
    }

    public async Task<List<GameEntry>> DetectGamesAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var games = new List<GameEntry>();

        progress?.Report("Scanning Steam library…");
        try { games.AddRange(await SteamDetector.FindGamesAsync(ct)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Steam scan failed."); }

        progress?.Report("Scanning Epic Games library…");
        try { games.AddRange(await EpicDetector.FindGamesAsync(ct)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Epic scan failed."); }

        progress?.Report("Scanning Riot Games…");
        try { games.AddRange(await RiotDetector.FindGamesAsync(ct)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Riot scan failed."); }

        progress?.Report("Scanning Rockstar Games…");
        try { games.AddRange(await RockstarDetector.FindGamesAsync(ct)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Rockstar scan failed."); }

        progress?.Report("Scanning GOG Galaxy…");
        try { games.AddRange(await GogDetector.FindGamesAsync(ct)); }
        catch (Exception ex) { _logger.LogWarning(ex, "GOG scan failed."); }

        progress?.Report("Scanning Windows registry…");
        try { games.AddRange(await RegistryDetector.FindGamesAsync(ct)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Registry scan failed."); }

        progress?.Report("Scanning known game paths…");
        try { games.AddRange(await KnownPathsDetector.FindGamesAsync(ct)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Known paths scan failed."); }

        // Deduplicate by install directory
        var result = games
            .DistinctBy(g => string.IsNullOrEmpty(g.InstallDirectory)
                ? g.Name
                : g.InstallDirectory.TrimEnd('\\', '/').ToLowerInvariant())
            .OrderBy(g => g.Name)
            .ToList();

        progress?.Report($"Found {result.Count} games.");
        return result;
    }
}

// ── Steam ─────────────────────────────────────────────────────────────────────

internal static class SteamDetector
{
    public static bool IsSteamInstalled() => GetSteamPath() != null;

    private static string? GetSteamPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                         ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            return key?.GetValue("InstallPath")?.ToString();
        }
        catch { return null; }
    }

    public static async Task<List<GameEntry>> FindGamesAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var games = new List<GameEntry>();
            var steamPath = GetSteamPath();
            if (steamPath == null) return games;

            // Collect all Steam library paths from libraryfolders.vdf
            var libraryPaths = new List<string> { Path.Combine(steamPath, "steamapps") };
            var vdfFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfFile))
            {
                foreach (var line in File.ReadAllLines(vdfFile))
                {
                    var t = line.Trim();
                    if (t.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = t.Split('"');
                        if (parts.Length >= 4)
                            libraryPaths.Add(Path.Combine(parts[3].Replace("\\\\", "\\"), "steamapps"));
                    }
                }
            }

            foreach (var lib in libraryPaths.Distinct())
            {
                if (!Directory.Exists(lib)) continue;
                foreach (var acf in Directory.GetFiles(lib, "appmanifest_*.acf"))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var content   = File.ReadAllText(acf);
                        var name      = ExtractVdf(content, "name");
                        var installDir= ExtractVdf(content, "installdir");
                        var appId     = ExtractVdf(content, "appid");
                        var state     = int.TryParse(ExtractVdf(content, "StateFlags"), out var sf) ? sf : 0;

                        // StateFlags: 4 = fully installed
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(installDir)) continue;
                        if (state != 4 && state != 6) continue; // skip uninstalling/downloading

                        var fullDir = Path.Combine(lib, "common", installDir);
                        if (!Directory.Exists(fullDir)) continue;

                        games.Add(new GameEntry
                        {
                            Name             = name,
                            InstallDirectory = fullDir,
                            ExecutablePath   = FindMainExe(fullDir, name) ?? string.Empty,
                            Store            = GameStore.Steam,
                            StoreAppId       = appId ?? string.Empty,
                            InstallSizeBytes = GetDirSize(fullDir)
                        });
                    }
                    catch { }
                }
            }
            return games;
        }, ct);
    }

    private static string ExtractVdf(string content, string key)
    {
        var idx = content.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return string.Empty;
        var after = content[(idx + key.Length + 2)..].TrimStart('\t', ' ');
        if (!after.StartsWith('"')) return string.Empty;
        after = after[1..];
        var end = after.IndexOf('"');
        return end >= 0 ? after[..end] : string.Empty;
    }

    internal static string? FindMainExe(string dir, string gameName)
    {
        try
        {
            return Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                .Where(e => !Path.GetFileName(e).Contains("setup",     StringComparison.OrdinalIgnoreCase)
                         && !Path.GetFileName(e).Contains("unins",     StringComparison.OrdinalIgnoreCase)
                         && !Path.GetFileName(e).Contains("crash",     StringComparison.OrdinalIgnoreCase)
                         && !Path.GetFileName(e).Contains("helper",    StringComparison.OrdinalIgnoreCase)
                         && !Path.GetFileName(e).Contains("installer", StringComparison.OrdinalIgnoreCase)
                         && !Path.GetFileName(e).Contains("redist",    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => new FileInfo(e).Length)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    internal static long GetDirSize(string dir)
    {
        try { return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch { return 0; }
    }
}

// ── Epic Games ────────────────────────────────────────────────────────────────

internal static class EpicDetector
{
    private static string? GetManifestsPath()
    {
        var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        return Directory.Exists(p) ? p : null;
    }

    public static bool IsInstalled() => GetManifestsPath() != null;

    public static async Task<List<GameEntry>> FindGamesAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var games = new List<GameEntry>();
            var dir = GetManifestsPath();
            if (dir == null) return games;

            foreach (var item in Directory.GetFiles(dir, "*.item"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var json       = File.ReadAllText(item);
                    var name       = ExtractJson(json, "DisplayName");
                    var installDir = ExtractJson(json, "InstallLocation");
                    var appName    = ExtractJson(json, "AppName");
                    var exeName    = ExtractJson(json, "LaunchExecutable");
                    var isApp      = ExtractJson(json, "bIsApplication");

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(installDir)) continue;
                    if (!Directory.Exists(installDir)) continue;
                    if (isApp == "false") continue; // skip non-game items

                    games.Add(new GameEntry
                    {
                        Name             = name,
                        InstallDirectory = installDir,
                        ExecutablePath   = !string.IsNullOrEmpty(exeName)
                                           ? Path.Combine(installDir, exeName) : string.Empty,
                        Store            = GameStore.EpicGames,
                        StoreAppId       = appName ?? string.Empty,
                        InstallSizeBytes = SteamDetector.GetDirSize(installDir)
                    });
                }
                catch { }
            }
            return games;
        }, ct);
    }

    private static string ExtractJson(string json, string key)
    {
        var idx = json.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return string.Empty;
        var after = json[(idx + key.Length + 2)..].TrimStart(' ', ':');
        if (!after.StartsWith('"')) return string.Empty;
        after = after[1..];
        var end = after.IndexOf('"');
        return end >= 0 ? after[..end] : string.Empty;
    }
}

// ── Riot Games ────────────────────────────────────────────────────────────────

internal static class RiotDetector
{
    private static readonly string[] _riotRoots =
    {
        @"C:\Riot Games",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Riot Games"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Riot Games"),
    };

    // Map of Riot game folder names to friendly display names + exe
    private static readonly Dictionary<string, (string name, string exe)> _riotGames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "VALORANT",           ("VALORANT",              @"live\VALORANT.exe") },
        { "League of Legends",  ("League of Legends",     @"Game\League of Legends.exe") },
        { "TeamFight Tactics",  ("Teamfight Tactics",     @"Game\League of Legends.exe") },
        { "Legends of Runeterra",("Legends of Runeterra", @"live\LoR.exe") },
    };

    public static bool IsInstalled() => _riotRoots.Any(Directory.Exists);

    public static async Task<List<GameEntry>> FindGamesAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var games = new List<GameEntry>();

            // Also check registry for Riot installs
            var regPaths = GetRiotPathsFromRegistry();

            foreach (var root in _riotRoots.Concat(regPaths).Distinct())
            {
                if (!Directory.Exists(root)) continue;
                foreach (var gameDir in Directory.GetDirectories(root))
                {
                    ct.ThrowIfCancellationRequested();
                    var folderName = Path.GetFileName(gameDir);

                    if (_riotGames.TryGetValue(folderName, out var info))
                    {
                        var exePath = Path.Combine(gameDir, info.exe);
                        // Try alternate exe paths if primary not found
                        if (!File.Exists(exePath))
                            exePath = SteamDetector.FindMainExe(gameDir, info.name) ?? string.Empty;

                        if (Directory.Exists(gameDir))
                        {
                            games.Add(new GameEntry
                            {
                                Name             = info.name,
                                InstallDirectory = gameDir,
                                ExecutablePath   = exePath,
                                Store            = GameStore.RiotGames,
                                InstallSizeBytes = SteamDetector.GetDirSize(gameDir)
                            });
                        }
                    }
                    else
                    {
                        // Unknown Riot game — include it if it has an exe
                        var exe = SteamDetector.FindMainExe(gameDir, folderName);
                        if (exe != null)
                        {
                            games.Add(new GameEntry
                            {
                                Name             = folderName,
                                InstallDirectory = gameDir,
                                ExecutablePath   = exe,
                                Store            = GameStore.RiotGames,
                                InstallSizeBytes = SteamDetector.GetDirSize(gameDir)
                            });
                        }
                    }
                }
            }
            return games;
        }, ct);
    }

    private static IEnumerable<string> GetRiotPathsFromRegistry()
    {
        var paths = new List<string>();
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var regPath in new[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            })
            {
                try
                {
                    using var key = hive.OpenSubKey(regPath);
                    if (key == null) continue;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        using var sk = key.OpenSubKey(sub);
                        var loc = sk?.GetValue("InstallLocation")?.ToString() ?? string.Empty;
                        var name = sk?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(loc) &&
                            (name.Contains("VALORANT", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Riot", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("League", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Navigate up to the Riot Games root
                            var parent = Path.GetDirectoryName(loc.TrimEnd('\\', '/'));
                            if (parent != null) paths.Add(parent);
                            paths.Add(loc);
                        }
                    }
                }
                catch { }
            }
        }
        return paths.Distinct();
    }
}

// ── Rockstar Games ────────────────────────────────────────────────────────────

internal static class RockstarDetector
{
    private static readonly string[] _rockstarRoots =
    {
        @"C:\Program Files\Rockstar Games",
        @"C:\Program Files (x86)\Rockstar Games",
        @"D:\Rockstar Games",
        @"D:\Games\Rockstar Games",
    };

    private static readonly Dictionary<string, string> _gameExes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Grand Theft Auto V",         "GTA5.exe" },
        { "Grand Theft Auto V Legacy",  "GTA5.exe" },
        { "GTA V",                      "GTA5.exe" },
        { "Red Dead Redemption 2",      "RDR2.exe" },
        { "Grand Theft Auto IV",        "GTAIV.exe" },
    };

    public static bool IsInstalled() =>
        _rockstarRoots.Any(Directory.Exists) || GetFromRegistry().Any();

    public static async Task<List<GameEntry>> FindGamesAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var games = new List<GameEntry>();

            // From registry (most reliable)
            foreach (var (name, dir) in GetFromRegistry())
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(dir)) continue;
                var exeName = _gameExes.FirstOrDefault(k =>
                    name.Contains(k.Key, StringComparison.OrdinalIgnoreCase)).Value;
                var exe = !string.IsNullOrEmpty(exeName)
                    ? FindExe(dir, exeName)
                    : SteamDetector.FindMainExe(dir, name);

                games.Add(new GameEntry
                {
                    Name             = name,
                    InstallDirectory = dir,
                    ExecutablePath   = exe ?? string.Empty,
                    Store            = GameStore.Standalone,
                    InstallSizeBytes = SteamDetector.GetDirSize(dir)
                });
            }

            // From known folders
            foreach (var root in _rockstarRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var gameDir in Directory.GetDirectories(root))
                {
                    ct.ThrowIfCancellationRequested();
                    var folderName = Path.GetFileName(gameDir);
                    if (folderName.Equals("Launcher", StringComparison.OrdinalIgnoreCase)) continue;
                    if (folderName.Equals("Social Club", StringComparison.OrdinalIgnoreCase)) continue;

                    // Skip if already found via registry
                    if (games.Any(g => g.InstallDirectory.Equals(gameDir, StringComparison.OrdinalIgnoreCase))) continue;

                    var exeName = _gameExes.FirstOrDefault(k =>
                        folderName.Contains(k.Key, StringComparison.OrdinalIgnoreCase)).Value;
                    var exe = !string.IsNullOrEmpty(exeName)
                        ? FindExe(gameDir, exeName)
                        : SteamDetector.FindMainExe(gameDir, folderName);
                    if (exe == null) continue;

                    games.Add(new GameEntry
                    {
                        Name             = CleanName(folderName),
                        InstallDirectory = gameDir,
                        ExecutablePath   = exe,
                        Store            = GameStore.Standalone,
                        InstallSizeBytes = SteamDetector.GetDirSize(gameDir)
                    });
                }
            }
            return games;
        }, ct);
    }

    private static IEnumerable<(string name, string dir)> GetFromRegistry()
    {
        var result = new List<(string, string)>();
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var regPath in new[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            })
            {
                try
                {
                    using var key = hive.OpenSubKey(regPath);
                    if (key == null) continue;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        using var sk = key.OpenSubKey(sub);
                        var name = sk?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                        var loc  = sk?.GetValue("InstallLocation")?.ToString() ?? string.Empty;
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(loc)) continue;
                        if (name.Contains("GTA", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Grand Theft", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Red Dead", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Rockstar", StringComparison.OrdinalIgnoreCase) && !name.Contains("Launcher"))
                            result.Add((name, loc));
                    }
                }
                catch { }
            }
        }
        return result;
    }

    private static string? FindExe(string dir, string exeName)
    {
        try
        {
            return Directory.GetFiles(dir, exeName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch { return null; }
    }

    private static string CleanName(string folderName)
    {
        // "Grand Theft Auto V Legacy" → "Grand Theft Auto V"
        return folderName.Replace(" Legacy", "").Trim();
    }
}

// ── GOG Galaxy ────────────────────────────────────────────────────────────────

internal static class GogDetector
{
    public static bool IsInstalled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games")
                         ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Games");
            return key?.SubKeyCount > 0;
        }
        catch { return false; }
    }

    public static async Task<List<GameEntry>> FindGamesAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var games = new List<GameEntry>();
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games")
                             ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Games");
                if (key == null) return games;

                foreach (var sub in key.GetSubKeyNames())
                {
                    ct.ThrowIfCancellationRequested();
                    using var sk = key.OpenSubKey(sub);
                    var name = sk?.GetValue("GAMENAME")?.ToString();
                    var dir  = sk?.GetValue("PATH")?.ToString();
                    var exe  = sk?.GetValue("EXE")?.ToString();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(dir)) continue;
                    if (!Directory.Exists(dir)) continue;

                    games.Add(new GameEntry
                    {
                        Name             = name,
                        InstallDirectory = dir,
                        ExecutablePath   = exe ?? SteamDetector.FindMainExe(dir, name) ?? string.Empty,
                        Store            = GameStore.GOG,
                        StoreAppId       = sub
                    });
                }
            }
            catch { }
            return games;
        }, ct);
    }
}

// ── Windows Uninstall Registry (catches FiveM, Roblox, and anything else) ────

internal static class RegistryDetector
{
    // Patterns that indicate a game (case-insensitive substring match on display name)
    private static readonly string[] _gameKeywords = {
        "fivem", "roblox", "minecraft", "battlenet", "battle.net",
        "ubisoft connect", "uplay", "origin", "ea desktop",
        "pubg", "warzone", "overwatch", "call of duty",
        "dota", "team fortress", "counter-strike", "half-life",
        "cyberpunk", "witcher", "elden ring", "dark souls",
        "fallout", "skyrim", "oblivion",
        "rocket league", "among us", "fall guys",
        "destiny", "halo", "gears", "forza",
        "anno", "civilization", "total war",
        "age of empires", "starcraft", "diablo",
        "world of warcraft", "wow", "hearthstone",
        "path of exile", "lost ark",
        "escape from tarkov",
    };

    public static async Task<List<GameEntry>> FindGamesAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var games = new List<GameEntry>();
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var regPath in new[] {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                })
                {
                    try
                    {
                        using var key = hive.OpenSubKey(regPath);
                        if (key == null) continue;

                        foreach (var sub in key.GetSubKeyNames())
                        {
                            ct.ThrowIfCancellationRequested();
                            using var sk = key.OpenSubKey(sub);
                            var name = sk?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                            var loc  = sk?.GetValue("InstallLocation")?.ToString() ?? string.Empty;
                            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(loc)) continue;
                            if (!Directory.Exists(loc)) continue;

                            var nameLower = name.ToLowerInvariant();
                            if (!_gameKeywords.Any(k => nameLower.Contains(k))) continue;

                            var exe = SteamDetector.FindMainExe(loc, name);

                            games.Add(new GameEntry
                            {
                                Name             = name,
                                InstallDirectory = loc,
                                ExecutablePath   = exe ?? string.Empty,
                                Store            = GameStore.Standalone,
                            });
                        }
                    }
                    catch { }
                }
            }
            return games;
        }, ct);
    }
}

// ── Known Paths Detector (catches FiveM in AppData, Roblox, etc.) ─────────────

internal static class KnownPathsDetector
{
    private static readonly (string path, string name, GameStore store)[] _knownPaths = {
        ($@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\FiveM",
         "FiveM", GameStore.Standalone),
        ($@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\Roblox",
         "Roblox", GameStore.Standalone),
        ($@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\Programs\FiveM",
         "FiveM", GameStore.Standalone),
        (@"C:\FiveM", "FiveM", GameStore.Standalone),
        (@"C:\Riot Games", string.Empty, GameStore.RiotGames),
        (@"D:\Riot Games", string.Empty, GameStore.RiotGames),
    };

    public static async Task<List<GameEntry>> FindGamesAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var games = new List<GameEntry>();

            foreach (var (path, name, store) in _knownPaths)
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(path)) continue;

                if (!string.IsNullOrEmpty(name))
                {
                    // Single game at this path
                    var exe = SteamDetector.FindMainExe(path, name);
                    if (exe != null || name == "Roblox") // Roblox launcher is special
                    {
                        // For Roblox, find the versioned exe
                        if (name == "Roblox" && string.IsNullOrEmpty(exe))
                        {
                            var versions = Path.Combine(path, "Versions");
                            if (Directory.Exists(versions))
                            {
                                exe = Directory.GetFiles(versions, "RobloxPlayerBeta.exe", SearchOption.AllDirectories)
                                              .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                              .FirstOrDefault();
                            }
                        }
                        games.Add(new GameEntry
                        {
                            Name             = name,
                            InstallDirectory = path,
                            ExecutablePath   = exe ?? string.Empty,
                            Store            = store
                        });
                    }
                }
                else
                {
                    // Scan subdirectories (e.g. Riot Games root has VALORANT, LoL etc.)
                    foreach (var subDir in Directory.GetDirectories(path))
                    {
                        var folderName = Path.GetFileName(subDir);
                        if (folderName.Equals("Riot Client", StringComparison.OrdinalIgnoreCase)) continue;
                        var exe = SteamDetector.FindMainExe(subDir, folderName);
                        if (exe != null)
                        {
                            games.Add(new GameEntry
                            {
                                Name             = folderName,
                                InstallDirectory = subDir,
                                ExecutablePath   = exe,
                                Store            = store
                            });
                        }
                    }
                }
            }
            return games;
        }, ct);
    }
}
