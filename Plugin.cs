using BepInEx;
using Tosox.CatchChamberedRounds.Configuration;
using Tosox.CatchChamberedRounds.Patches;

namespace Tosox.CatchChamberedRounds
{
    [BepInPlugin("de.tosox.catchchamberedrounds", PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginName = "Catch Chambered Rounds";
        public const string PluginVersion = "1.0.0";
        public const string PluginAuthor = "Tosox";
        public const string PluginSource = "https://github.com/Tosox/SPT-CatchChamberedRounds";

        internal static Plugin Instance { get; private set; }

        public void Awake()
        {
            Instance = this;
            Settings.Init(Config);

            new ThrowPatronAsLootPatch().Enable();
            new ReloadWithAmmoPatch().Enable();
            new QuickReloadCommandPatch().Enable();

            Logger.LogInfo("Plugin loaded successfully");
        }
    }
}
