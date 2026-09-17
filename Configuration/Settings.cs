using BepInEx.Configuration;

namespace Tosox.CatchChamberedRounds.Configuration
{
    internal static class Settings
    {
        private const string GeneralSection = "1. General";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> PlayCatchAnimation;
        internal static ConfigEntry<bool> QuickReloadBypass;
        internal static ConfigEntry<bool> InvertReloadKeys;
        internal static ConfigEntry<bool> BlockPointlessReloads;
        internal static ConfigEntry<bool> ShowNotification;

        internal static void Init(ConfigFile config)
        {
            Enabled = config.Bind(GeneralSection, "Enabled", true,
                new ConfigDescription(
                    "Master switch. When off, the chambered round drops on the floor like vanilla.",
                    null, Order(0)));

            PlayCatchAnimation = config.Bind(GeneralSection, "Play Catch Animation", true,
                new ConfigDescription(
                    "Pull the chambered round out with the vanilla catch animation before reloading an internal magazine weapon. " +
                    "This looks better but adds the length of that animation to the reload. Turn off to keep the round silently with no extra delay.",
                    null, Order(1)));

            QuickReloadBypass = config.Bind(GeneralSection, "Quick Reload Skips The Catch", true,
                new ConfigDescription(
                    "Using Tarkov's Quick reload key skips the catch animation entirely and reloads at full vanilla speed, " +
                    "throwing the chambered round on the floor. Your emergency out when someone is pushing you. " +
                    "Requires Quick reload to be bound under Settings > Controls.",
                    null, Order(2)));

            InvertReloadKeys = config.Bind(GeneralSection, "Invert Reload Keys", false,
                new ConfigDescription(
                    "Swap the two keys around: the normal reload becomes the fast vanilla one that drops the round, " +
                    "and Quick reload becomes the one that catches it. Needs 'Quick Reload Skips The Catch' enabled, " +
                    "and note that with nothing bound to Quick reload this leaves you no way to catch the round at all.",
                    null, Order(3)));

            BlockPointlessReloads = config.Bind(GeneralSection, "Block Pointless Reloads", true,
                new ConfigDescription(
                    "Refuse a reload that cannot add anything. Four rounds in the magazine and one chambered is already " +
                    "the five a reload would leave you with, so it would only swap the chambered round for another one " +
                    "out of your gear. Turn off if you would rather reload anyway.",
                    null, Order(4)));

            ShowNotification = config.Bind(GeneralSection, "Notify When A Round Drops", false,
                new ConfigDescription(
                    "Show a message when a live round ends up on the floor instead of in your gear, either because you " +
                    "reloaded in a hurry or because there was nowhere to put it. Silent when the round is caught, since " +
                    "that is the normal outcome and does not need announcing.",
                    null, Order(5)));
        }

        /// <summary>
        /// Configuration Manager sorts a section by descending Order and falls back to
        /// alphabetical, so counting positions down from a high number keeps the settings on
        /// screen in the order they are written here.
        /// </summary>
        private static ConfigurationManagerAttributes Order(int position)
        {
            return new ConfigurationManagerAttributes { Order = short.MaxValue - position };
        }
    }
}
