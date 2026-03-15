using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
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
    int IndirectDamage,
    int Kills);

public class PlayerRunStats
{
    public string DisplayName { get; set; } = "";
    public string CharacterClass { get; set; } = "";
    public int Damage { get; set; }
    public int Blocked { get; set; }
    public int IndirectDamage { get; set; }
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
    public int IndirectDamage { get; set; }
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
                int entryCount = cm.History.Entries.OfType<DamageReceivedEntry>()
                    .Count(e => ResolveOwnerPlayer(e.Dealer) != null);
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
                DumpHistoryEntries(cm.History);
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

    static void DumpHistoryEntries(CombatHistory history)
    {
        try
        {
            var entries = history.Entries.ToList();
            Log.Info($"[{ModId}] === HISTORY DUMP: {entries.Count} entries ===");

            foreach (var entry in entries)
            {
                switch (entry)
                {
                    case DamageReceivedEntry dre:
                        var dealer = dre.Dealer;
                        string dealerInfo = dealer == null ? "null"
                            : $"{dealer.Name}(IsPlayer={dealer.IsPlayer}, IsPet={dealer.IsPet}, Side={dealer.Side})";
                        var recv = dre.Result.Receiver;
                        string recvInfo = recv == null ? "null"
                            : $"{recv.Name}(IsPlayer={recv.IsPlayer}, Side={recv.Side})";
                        Log.Info($"[{ModId}]   DMG: dealer={dealerInfo} → receiver={recvInfo} total={dre.Result.TotalDamage} blocked={dre.Result.BlockedDamage} killed={dre.Result.WasTargetKilled} card={dre.CardSource?.Title ?? "none"}");
                        break;

                    case PowerReceivedEntry pre:
                        string applierInfo = pre.Applier == null ? "null"
                            : $"{pre.Applier.Name}(IsPlayer={pre.Applier.IsPlayer}, IsPet={pre.Applier.IsPet})";
                        Log.Info($"[{ModId}]   PWR: {pre.Power?.GetType().Name ?? "?"} amount={pre.Amount} applier={applierInfo} actor={pre.Actor?.Name ?? "null"}");
                        break;

                    default:
                        Log.Info($"[{ModId}]   {entry.GetType().Name}: actor={entry.Actor?.Name ?? "null"}");
                        break;
                }
            }
            Log.Info($"[{ModId}] === END DUMP ===");
        }
        catch (Exception ex)
        {
            Log.Error($"[{ModId}] DumpHistory error: {ex.Message}");
        }
    }

    static Player? ResolveOwnerPlayer(Creature? dealer)
    {
        if (dealer == null) return null;
        if (dealer.IsPlayer) return dealer.Player;
        if (dealer.IsPet) return dealer.PetOwner;
        return null;
    }

    static Dictionary<ulong, int> CollectDoomDamage()
    {
        var result = new Dictionary<ulong, int>();
        foreach (var pre in CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>())
        {
            if (pre.Power?.GetType().Name != "DoomPower") continue;
            if (pre.Amount <= 0) continue;

            var owner = ResolveOwnerPlayer(pre.Applier);
            if (owner == null) continue;

            result.TryGetValue(owner.NetId, out int existing);
            result[owner.NetId] = existing + (int)pre.Amount;
        }
        return result;
    }

    static (int totalPoisonDmg, HashSet<ulong> poisonPlayerIds) CollectPoisonInfo()
    {
        var history = CombatManager.Instance.History.Entries;

        int totalPoison = history
            .OfType<DamageReceivedEntry>()
            .Where(e => e.Dealer == null && e.Receiver is { IsMonster: true })
            .Sum(e => e.Result.TotalDamage);

        var playerIds = new HashSet<ulong>();
        foreach (var pre in history.OfType<PowerReceivedEntry>())
        {
            if (pre.Power?.GetType().Name != "PoisonPower") continue;
            if (pre.Amount <= 0) continue;
            var owner = ResolveOwnerPlayer(pre.Applier);
            if (owner != null) playerIds.Add(owner.NetId);
        }

        return (totalPoison, playerIds);
    }

    static List<PlayerCombatStats> CollectCurrentStats()
    {
        var history = CombatManager.Instance.History.Entries;
        var doomByPlayer = CollectDoomDamage();
        var (totalPoisonDmg, poisonPlayerIds) = CollectPoisonInfo();

        int poisonShare = poisonPlayerIds.Count > 0
            ? totalPoisonDmg / poisonPlayerIds.Count
            : 0;

        var damageStats = history
            .OfType<DamageReceivedEntry>()
            .Select(e => (Entry: e, Owner: ResolveOwnerPlayer(e.Dealer)))
            .Where(x => x.Owner != null)
            .GroupBy(x => x.Owner!.NetId)
            .ToDictionary(g => g.Key, g => g);

        var allPlayerIds = damageStats.Keys
            .Union(doomByPlayer.Keys)
            .Union(poisonPlayerIds)
            .ToHashSet();

        return allPlayerIds
            .Select(netId =>
            {
                Player? owner = null;
                string displayName = "???";
                string charClass = "???";
                int damage = 0, blocked = 0, kills = 0;

                if (damageStats.TryGetValue(netId, out var group))
                {
                    owner = group.First().Owner;
                    damage = group.Sum(x => x.Entry.Result.TotalDamage);
                    blocked = group.Sum(x => x.Entry.Result.BlockedDamage);
                    kills = group.Count(x => x.Entry.Result.WasTargetKilled);
                }

                if (owner == null)
                {
                    foreach (var pre in history.OfType<PowerReceivedEntry>())
                    {
                        var o = ResolveOwnerPlayer(pre.Applier);
                        if (o?.NetId == netId) { owner = o; break; }
                    }
                }

                if (owner != null)
                {
                    displayName = owner.Creature?.Name ?? "???";
                    try { charClass = owner.Character.Title.GetFormattedText(); }
                    catch { charClass = owner.Character.Id.Entry; }
                }

                doomByPlayer.TryGetValue(netId, out int doom);
                int poison = poisonPlayerIds.Contains(netId) ? poisonShare : 0;
                int indirect = doom + poison;

                return new PlayerCombatStats(
                    netId, displayName, charClass,
                    damage + indirect, blocked, indirect, kills);
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
                rs.IndirectDamage += s.IndirectDamage;
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
                IndirectDamage = kvp.Value.IndirectDamage,
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
                    IndirectDamage = e.IndirectDamage,
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
