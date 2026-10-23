using BepInEx.Configuration;

namespace Tosox.CatchChamberedRounds.Configuration
{
    internal static class Settings
    {
        private const string GeneralSection = "1. General";
        private const string DebugSection = "2. Debug";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> PlayCatchAnimation;
        internal static ConfigEntry<bool> CatchOnQuickReload;
        internal static ConfigEntry<bool> BlockPointlessReloads;

        internal static ConfigEntry<bool> VerboseLogging;

        internal static void Init(ConfigFile config)
        {
            Enabled = config.Bind(GeneralSection, "Enabled", true,
                new ConfigDescription("Untick to disable catching the chambered round",
                    null, Order(0)));

            PlayCatchAnimation = config.Bind(GeneralSection, "Play Catch Animation", true,
                new ConfigDescription("Pull the round out with a catch animation instead of stowing it silently",
                    null, Order(1)));

            CatchOnQuickReload = config.Bind(GeneralSection, "Catch on Quick Reload Instead", false,
                new ConfigDescription("Catch the round on a quick reload and drop it on a normal reload",
                    null, Order(2)));

            BlockPointlessReloads = config.Bind(GeneralSection, "Block Pointless Reloads", true,
                new ConfigDescription("Refuse a reload that cannot add any rounds",
                    null, Order(3)));

            VerboseLogging = config.Bind(DebugSection, "Verbose Logging", false,
                new ConfigDescription("Write additional details to the log for troubleshooting",
                    null, Order(0)));
        }

        private static ConfigurationManagerAttributes Order(int position)
        {
            return new ConfigurationManagerAttributes { Order = short.MaxValue - position };
        }
    }
}
