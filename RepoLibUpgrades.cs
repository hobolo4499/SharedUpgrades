using System;
using System.Reflection;
using HarmonyLib;

namespace SharedUpgrades;

// Upgrades that other mods add through REPOLib (optional). These don't go through PunManager:
// the client of the player who used the item calls PlayerUpgrade.SetLevel, and REPOLib sends the
// new level to everyone else, where it arrives in PlayerUpgrade.ApplyUpgrade. Patched by
// reflection so this mod still loads without REPOLib.
internal static class RepoLibUpgrades
{
    // Ledger entries for REPOLib upgrades are "REPOLib:<UpgradeId>".
    internal const string KeyPrefix = "REPOLib:";

    private static MethodInfo? _getUpgrade; // static Upgrades.GetUpgrade(string upgradeId)
    private static MethodInfo? _getLevel;   // PlayerUpgrade.GetLevel(string steamId)
    private static MethodInfo? _addLevel;   // PlayerUpgrade.AddLevel(string steamId, int amount)
    private static FieldInfo? _upgradeId;   // PlayerUpgrade.UpgradeId

    // True inside SetLevel, which calls ApplyUpgrade itself; that inner call isn't a new change.
    private static bool _inSetLevel;

    internal static void Init(Harmony harmony)
    {
        Type? playerUpgrade = AccessTools.TypeByName("REPOLib.Modules.PlayerUpgrade");
        Type? upgrades = AccessTools.TypeByName("REPOLib.Modules.Upgrades");
        if (playerUpgrade == null || upgrades == null)
        {
            SharedUpgrades.Logger.LogInfo("REPOLib not found; only vanilla upgrades are shared");
            return;
        }

        Type[] stringInt = { typeof(string), typeof(int) };
        _getUpgrade = AccessTools.Method(upgrades, "GetUpgrade", new[] { typeof(string) });
        _getLevel = AccessTools.Method(playerUpgrade, "GetLevel", new[] { typeof(string) });
        _addLevel = AccessTools.Method(playerUpgrade, "AddLevel", stringInt);
        _upgradeId = AccessTools.Field(playerUpgrade, "UpgradeId");
        MethodInfo? setLevel = AccessTools.Method(playerUpgrade, "SetLevel", stringInt);
        MethodInfo? applyUpgrade = AccessTools.Method(playerUpgrade, "ApplyUpgrade", stringInt);

        if (_getUpgrade == null || _getLevel == null || _addLevel == null || _upgradeId == null
            || setLevel == null || applyUpgrade == null)
        {
            _addLevel = null;
            SharedUpgrades.Logger.LogWarning("This REPOLib version isn't supported; its upgrades won't be shared");
            return;
        }

        harmony.Patch(setLevel,
            prefix: new HarmonyMethod(typeof(RepoLibUpgrades), nameof(SetLevelPrefix)),
            postfix: new HarmonyMethod(typeof(RepoLibUpgrades), nameof(SetLevelPostfix)),
            finalizer: new HarmonyMethod(typeof(RepoLibUpgrades), nameof(SetLevelFinalizer)));
        harmony.Patch(applyUpgrade,
            prefix: new HarmonyMethod(typeof(RepoLibUpgrades), nameof(ApplyUpgradePrefix)),
            postfix: new HarmonyMethod(typeof(RepoLibUpgrades), nameof(ApplyUpgradePostfix)));

        SharedUpgrades.Logger.LogInfo("REPOLib found; its upgrades are shared too");
    }

    internal static bool IsRepoLibKey(string upgrade) => upgrade.StartsWith(KeyPrefix);

    // False when REPOLib or the mod that added this upgrade isn't installed (any more).
    internal static bool CanApply(string upgrade) => GetUpgrade(upgrade) != null;

    // REPOLib sends the new level to the other players itself.
    internal static void AddLevel(string steamID, string upgrade, int amount)
    {
        object? playerUpgrade = GetUpgrade(upgrade);
        if (playerUpgrade == null)
        {
            SharedUpgrades.Logger.LogWarning($"Can't apply unknown upgrade {upgrade}");
            return;
        }
        _addLevel!.Invoke(playerUpgrade, new object[] { steamID, amount });
    }

    private static object? GetUpgrade(string upgrade)
    {
        if (_addLevel == null || !IsRepoLibKey(upgrade))
        {
            return null;
        }
        return _getUpgrade!.Invoke(null, new object[] { upgrade.Substring(KeyPrefix.Length) });
    }

    private static int GetLevel(object playerUpgrade, string steamId)
    {
        return (int)_getLevel!.Invoke(playerUpgrade, new object[] { steamId });
    }

    // Host only. Unlike vanilla, there's no way to tell an item use from other level changes
    // (debug commands, other mods), so every increase counts.
    private static void Record(object playerUpgrade, string steamId, int increase)
    {
        if (increase <= 0 || UpgradeLedger.Granting || !SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }
        UpgradeLedger.RecordActivation(KeyPrefix + (string)_upgradeId!.GetValue(playerUpgrade), steamId, increase);
    }

    // Runs where the change starts: the host's own item use, or our grants.
    private static void SetLevelPrefix(object __instance, string steamId, out int __state)
    {
        __state = GetLevel(__instance, steamId);
        _inSetLevel = true;
    }

    private static void SetLevelPostfix(object __instance, string steamId, int __result, int __state)
    {
        Record(__instance, steamId, __result - __state);
    }

    private static void SetLevelFinalizer() => _inSetLevel = false;

    // Runs when another player's change arrives through REPOLib's network event.
    private static void ApplyUpgradePrefix(object __instance, string steamId, out int __state)
    {
        __state = GetLevel(__instance, steamId);
    }

    private static void ApplyUpgradePostfix(object __instance, string steamId, int level, int __state)
    {
        if (!_inSetLevel)
        {
            Record(__instance, steamId, level - __state);
        }
    }
}
