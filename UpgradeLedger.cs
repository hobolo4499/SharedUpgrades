using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SharedUpgrades;

// Host-authoritative record of every upgrade item used this run, and of how many shared
// levels each player has received. Both live in StatsManager.dictionaryOfDictionaries, so the
// game saves them with the run, clears them on a new run and syncs them to clients for us.
internal static class UpgradeLedger
{
    // upgrade method name -> total levels activated by the team
    internal const string TeamKey = "sharedUpgradesTeam";
    // "steamID|upgrade method name" -> levels that player has received
    internal const string GrantedKey = "sharedUpgradesGranted";

    // Upgrade stats are read ~0.2s after spawn / level generation; granting inside that window
    // would apply the upgrade twice, so wait until a player has been around for a while.
    private const float SpawnGraceSeconds = 3f;
    private const float CheckIntervalSeconds = 1f;

    // Every PunManager.UpgradeXxx(string _steamID, int value) method, keyed by name.
    internal static readonly Dictionary<string, MethodInfo> UpgradeMethods = typeof(PunManager)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Where(m => m.Name.StartsWith("Upgrade")
            && m.ReturnType == typeof(int)
            && m.GetParameters().Select(p => p.ParameterType)
                .SequenceEqual(new[] { typeof(string), typeof(int) }))
        .ToDictionary(m => m.Name);

    // True while we apply a grant, so the PunManager postfix doesn't record it as an activation.
    internal static bool Granting { get; private set; }

    private static readonly Dictionary<PlayerAvatar, float> _firstSeen = new();
    private static float _nextCheck;
    private static bool _dirty;

    internal static void Register(StatsManager stats)
    {
        stats.dictionaryOfDictionaries.TryAdd(TeamKey, new Dictionary<string, int>());
        stats.dictionaryOfDictionaries.TryAdd(GrantedKey, new Dictionary<string, int>());
    }

    // Host only: called when an upgrade item was used by `steamID`. The game already applied it
    // to that player on every client, so only the team total and their own count go up here.
    internal static void RecordActivation(string upgrade, string steamID, int value)
    {
        if (!TryGetDictionaries(out var team, out var granted))
        {
            return;
        }

        team[upgrade] = team.GetValueOrDefault(upgrade, 0) + value;
        string key = GrantedKeyFor(steamID, upgrade);
        granted[key] = granted.GetValueOrDefault(key, 0) + value;
        _dirty = true;

        SharedUpgrades.Logger.LogInfo($"{steamID} used {upgrade}; team total is now {team[upgrade]}");
    }

    // Host only, every frame: hand out whatever each player is missing.
    internal static void HostTick()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !IsLevelReady())
        {
            _firstSeen.Clear();
            return;
        }

        if (!_dirty && Time.time < _nextCheck)
        {
            return;
        }
        _nextCheck = Time.time + CheckIntervalSeconds;
        _dirty = false;

        Reconcile();
    }

    private static void Reconcile()
    {
        if (!TryGetDictionaries(out var team, out var granted) || team.Count == 0)
        {
            return;
        }

        foreach (PlayerAvatar dead in _firstSeen.Keys.Where(a => !a).ToList())
        {
            _firstSeen.Remove(dead);
        }

        foreach (PlayerAvatar player in SemiFunc.PlayerGetAll())
        {
            if (!player)
            {
                continue;
            }
            if (!_firstSeen.TryGetValue(player, out float seen))
            {
                _firstSeen[player] = Time.time;
                continue;
            }
            if (Time.time - seen < SpawnGraceSeconds)
            {
                continue;
            }

            string steamID = SemiFunc.PlayerGetSteamID(player);
            if (string.IsNullOrEmpty(steamID))
            {
                continue;
            }

            foreach (KeyValuePair<string, int> entry in team.ToList())
            {
                string key = GrantedKeyFor(steamID, entry.Key);
                int missing = entry.Value - granted.GetValueOrDefault(key, 0);
                // An upgrade from a mod that isn't installed now stays in the ledger for later.
                if (missing <= 0 || !CanApply(entry.Key))
                {
                    continue;
                }

                granted[key] = entry.Value;
                Apply(steamID, entry.Key, missing);
                if (!RepoLibUpgrades.IsRepoLibKey(entry.Key))
                {
                    UpgradeNetwork.SendGrant(steamID, entry.Key, missing);
                }
                SharedUpgrades.Logger.LogInfo($"Granted {missing}x {entry.Key} to {steamID}");
            }
        }
    }

    private static bool CanApply(string upgrade)
    {
        return UpgradeMethods.ContainsKey(upgrade) || RepoLibUpgrades.CanApply(upgrade);
    }

    // Runs on the host directly and, for vanilla upgrades, on clients when the host's grant
    // event arrives. REPOLib upgrades are only applied on the host; REPOLib syncs them.
    internal static void Apply(string steamID, string upgrade, int amount)
    {
        Granting = true;
        try
        {
            if (RepoLibUpgrades.IsRepoLibKey(upgrade))
            {
                RepoLibUpgrades.AddLevel(steamID, upgrade, amount);
            }
            else if (UpgradeMethods.TryGetValue(upgrade, out MethodInfo method) && PunManager.instance)
            {
                method.Invoke(PunManager.instance, new object[] { steamID, amount });
            }
            else
            {
                SharedUpgrades.Logger.LogWarning($"Can't apply unknown upgrade {upgrade}");
            }
        }
        finally
        {
            Granting = false;
        }
    }

    private static bool IsLevelReady()
    {
        return LevelGenerator.Instance
            && LevelGenerator.Instance.Generated
            && PunManager.instance
            && StatsManager.instance
            && !SemiFunc.MenuLevel();
    }

    private static bool TryGetDictionaries(out Dictionary<string, int> team, out Dictionary<string, int> granted)
    {
        team = null!;
        granted = null!;
        // Look up every time: loading a save may replace the dictionaries.
        var all = StatsManager.instance ? StatsManager.instance.dictionaryOfDictionaries : null;
        return all != null && all.TryGetValue(TeamKey, out team) && all.TryGetValue(GrantedKey, out granted);
    }

    private static string GrantedKeyFor(string steamID, string upgrade) => $"{steamID}|{upgrade}";
}
