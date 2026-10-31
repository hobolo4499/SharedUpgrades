using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SharedUpgrades;

[BepInPlugin("SolEclips.SharedUpgrades", "SharedUpgrades", "1.1.1")]
public class SharedUpgrades : BaseUnityPlugin
{
    internal static SharedUpgrades Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    private void Awake()
    {
        Instance = this;
        
        // Prevent the plugin from being deleted
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        Patch();
        UpgradeNetwork.Init();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    internal void Patch()
    {
        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        Harmony?.UnpatchSelf();
    }

    private void Update()
    {
        UpgradeLedger.HostTick();
    }
}