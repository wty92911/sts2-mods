using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;

namespace DamageTracker;

public record PlayerCombatStats(
    ulong NetId,
    string DisplayName,
    string CharacterClass,
    int Damage,
    int Blocked,
    int Kills);

public class PlayerRunStats
{
    public string DisplayName { get; set; } = "";
    public string CharacterClass { get; set; } = "";
    public int Damage { get; set; }
    public int Blocked { get; set; }
    public int Kills { get; set; }
    public int Combats { get; set; }
}

public class SavedEntry
{
    public ulong NetId { get; set; }
    public string DisplayName { get; set; } = "";
    public string CharacterClass { get; set; } = "";
    public int Damage { get; set; }
    public int Blocked { get; set; }
    public int Kills { get; set; }
    public int Combats { get; set; }
}

[ModInitializer(nameof(Initialize))]
public static class DamageTrackerMod
{
    public const string ModId = "DamageTracker";
    const string SaveFileName = "run_stats.json";

    static readonly Dictionary<ulong, PlayerRunStats> RunStats = new();
    static DamageOverlay? _overlay;
    static string? _saveDir;

    static bool _wasInCombat;
    static bool _wasRunInProgress;
    static int _lastHistoryCount;
    static List<PlayerCombatStats>? _lastCombatSnapshot;

    public static void Initialize()
    {
        Log.Info($"[{ModId}] Loaded, deferring setup...");
        _saveDir = ResolveModDirectory();

        var tree = (SceneTree)Engine.GetMainLoop();
        Action? cb = null;
        cb = () =>
        {
            tree.ProcessFrame -= cb!;
            Setup();
        };
        tree.ProcessFrame += cb;
    }

    static void Setup()
    {
        _overlay = new DamageOverlay();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(_overlay);

        LoadStats();

        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame += Tick;

        Log.Info($"[{ModId}] Ready — polling every frame via ProcessFrame.");
    }

    static void Tick()
    {
        try
        {
            var rm = RunManager.Instance;
            var cm = CombatManager.Instance;
            bool runActive = rm.IsInProgress;
            bool inCombat = cm.IsInProgress;

            if (!runActive && _wasRunInProgress)
            {
                Log.Info($"[{ModId}] Run ended (back to menu).");
                _overlay?.HideAll();
            }

            if (runActive && !_wasRunInProgress)
            {
                Log.Info($"[{ModId}] New run started.");
                RunStats.Clear();
                DeleteSaveFile();
                _overlay?.HideAll();
                _lastCombatSnapshot = null;
                _lastHistoryCount = 0;
            }

            if (inCombat && !_wasInCombat)
            {
                Log.Info($"[{ModId}] Combat started.");
                _lastCombatSnapshot = null;
                _lastHistoryCount = 0;
                _overlay?.HideAll();
            }

            if (inCombat)
            {
                int entryCount = cm.History.Entries.OfType<DamageReceivedEntry>().Count();
                if (entryCount > 0 && entryCount != _lastHistoryCount)
                {
                    _lastHistoryCount = entryCount;
                    var stats = CollectCurrentStats();
                    if (stats.Count > 0)
                    {
                        _lastCombatSnapshot = stats;
                        _overlay?.ShowLive(stats, IsMultiplayer());
                    }
                }
            }

            if (!inCombat && _wasInCombat)
            {
                Log.Info($"[{ModId}] Combat ended.");
                ProcessCombatEnd();
            }

            if (!runActive)
                _overlay?.HideAll();

            _wasRunInProgress = runActive;
            _wasInCombat = inCombat;
        }
        catch (Exception ex)
        {
            Log.Error($"[{ModId}] Tick error: {ex.Message}");
        }
    }

    static bool IsMultiplayer()
    {
        try
        {
            var rm = RunManager.Instance;
            return rm.IsInProgress && !rm.IsSinglePlayerOrFakeMultiplayer;
        }
        catch { return false; }
    }

    static List<PlayerCombatStats> CollectCurrentStats()
    {
        return CombatManager.Instance.History.Entries
            .OfType<DamageReceivedEntry>()
            .Where(e => e.Dealer is { IsPlayer: true })
            .GroupBy(e => e.Dealer!.Player!.NetId)
            .Select(g =>
            {
                var player = g.First().Dealer!.Player!;
                var displayName = g.First().Dealer!.Name;
                string charClass;
                try { charClass = player.Character.Title.GetFormattedText(); }
                catch { charClass = player.Character.Id.Entry; }

                return new PlayerCombatStats(
                    g.Key,
                    displayName,
                    charClass,
                    g.Sum(e => e.Result.TotalDamage),
                    g.Sum(e => e.Result.BlockedDamage),
                    g.Count(e => e.Result.WasTargetKilled));
            })
            .OrderByDescending(s => s.Damage)
            .ToList();
    }

    static void ProcessCombatEnd()
    {
        try
        {
            var stats = _lastCombatSnapshot ?? CollectCurrentStats();
            _lastCombatSnapshot = null;
            Log.Info($"[{ModId}] CombatEnd — {stats.Count} player(s) in snapshot.");
            if (stats.Count == 0) return;

            foreach (var s in stats)
            {
                if (!RunStats.TryGetValue(s.NetId, out var rs))
                {
                    rs = new PlayerRunStats();
                    RunStats[s.NetId] = rs;
                }
                rs.DisplayName = s.DisplayName;
                rs.CharacterClass = s.CharacterClass;
                rs.Damage += s.Damage;
                rs.Blocked += s.Blocked;
                rs.Kills += s.Kills;
                rs.Combats++;
            }

            SaveStats();

            var runList = RunStats.Values
                .OrderByDescending(r => r.Damage)
                .ToList();

            _overlay!.ShowSummary(stats, runList, IsMultiplayer());
            Log.Info($"[{ModId}] Combat: {string.Join(", ", stats.Select(s => $"{s.DisplayName}({s.CharacterClass})={s.Damage}"))}");
        }
        catch (Exception ex)
        {
            Log.Error($"[{ModId}] CombatEnd error: {ex.Message}");
        }
    }

    // ── Persistence ─────────────────────────────────────────

    static string SaveFilePath => Path.Combine(_saveDir ?? ".", SaveFileName);

    static void SaveStats()
    {
        try
        {
            var entries = RunStats.Select(kvp => new SavedEntry
            {
                NetId = kvp.Key,
                DisplayName = kvp.Value.DisplayName,
                CharacterClass = kvp.Value.CharacterClass,
                Damage = kvp.Value.Damage,
                Blocked = kvp.Value.Blocked,
                Kills = kvp.Value.Kills,
                Combats = kvp.Value.Combats,
            }).ToList();

            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SaveFilePath, json);
        }
        catch (Exception ex)
        {
            Log.Error($"[{ModId}] Failed to save stats: {ex.Message}");
        }
    }

    static void LoadStats()
    {
        try
        {
            var path = SaveFilePath;
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<SavedEntry>>(json);
            if (entries == null || entries.Count == 0) return;

            RunStats.Clear();
            foreach (var e in entries)
            {
                RunStats[e.NetId] = new PlayerRunStats
                {
                    DisplayName = e.DisplayName,
                    CharacterClass = e.CharacterClass,
                    Damage = e.Damage,
                    Blocked = e.Blocked,
                    Kills = e.Kills,
                    Combats = e.Combats,
                };
            }

            Log.Info($"[{ModId}] Restored {entries.Count} player stats from previous session.");
        }
        catch (Exception ex)
        {
            Log.Error($"[{ModId}] Failed to load stats: {ex.Message}");
        }
    }

    static void DeleteSaveFile()
    {
        try
        {
            var path = SaveFilePath;
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { /* best effort */ }
    }

    static string ResolveModDirectory()
    {
        var loc = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(loc))
        {
            var dir = Path.GetDirectoryName(loc);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }
        return Path.Combine(AppContext.BaseDirectory, "mods", ModId);
    }
}
