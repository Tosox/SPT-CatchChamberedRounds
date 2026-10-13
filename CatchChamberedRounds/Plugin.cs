using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Tosox.CatchChamberedRounds.Configuration;
using Tosox.CatchChamberedRounds.Fika;
using Tosox.CatchChamberedRounds.Patches;

namespace Tosox.CatchChamberedRounds
{
    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    [BepInDependency(FikaGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private const string FikaGuid = "com.fika.core";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        public void Awake()
        {
            Instance = this;
            Log = Logger;
            Settings.Init(Config);

            new ThrowPatronAsLootPatch().Enable();
            new ReloadWithAmmoPatch().Enable();
            new PlannedReloadPatch().Enable();
            new QuickReloadCommandPatch().Enable();
            new OperationFactoryPatch().Enable();
            new SuppressReloadAnimationPatch().Enable();
            new SuppressStaleEventsPatch().Enable();

            if (Chainloader.PluginInfos.ContainsKey(FikaGuid))
                FikaSync.Init();

            Logger.LogInfo("Plugin loaded successfully");
        }
    }
}
