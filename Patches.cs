using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SharedUpgrades;

// ItemUpgrade.PlayerUpgrade runs on every client (ItemToggle syncs via RPC to All), including
// the host, so the host sees each activation directly and needs no extra report from clients.
[HarmonyPatch(typeof(ItemUpgrade), nameof(ItemUpgrade.PlayerUpgrade))]
static class ItemUpgradePatch
{
    // True while an upgrade item is being consumed, so only item upgrades are recorded
    // (not save loading, debug commands or our own grants).
    internal static bool Active { get; private set; }

    [HarmonyPrefix]
    private static void Prefix() => Active = true;

    [HarmonyFinalizer]
    private static void Finalizer() => Active = false;
}

[HarmonyPatch]
static class PunManagerUpgradePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => UpgradeLedger.UpgradeMethods.Values;

    [HarmonyPostfix]
    private static void Postfix(MethodBase __originalMethod, string _steamID, int value)
    {
        if (!ItemUpgradePatch.Active || UpgradeLedger.Granting || !SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }

        UpgradeLedger.RecordActivation(__originalMethod.Name, _steamID, value);
    }
}

[HarmonyPatch(typeof(StatsManager), "Start")]
static class StatsManagerStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(StatsManager __instance) => UpgradeLedger.Register(__instance);
}
