using Comfort.Common;
using EFT;
using EFT.Communications;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using System;
using System.Runtime.CompilerServices;
using Tosox.CatchChamberedRounds.Helpers;
using Tosox.CatchChamberedRounds.Net;
using Tosox.CatchChamberedRounds.Sync;

namespace Tosox.CatchChamberedRounds.Fika
{
    internal static class FikaSync
    {
        private static bool warnedVersionMismatch;

        internal static void Init()
        {
            // The packet lives in its own assembly, so a half copied install only shows up once the
            // first packet is built. Load it here instead, where it can still be reported properly
            try
            {
                ProbePacketAssembly();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not load Tosox.CatchChamberedRounds.Net.dll, catches will not "
                    + "be synced and the other clients will see your rounds hit the ground. "
                    + $"Reinstall the mod with all of its files. ({ex.Message})");
                return;
            }

            // The explicit delegates are required. Without them the compiler caches them in a generated
            // class that the game walks while loading types, which force loads Fika.Core
            FikaEventDispatcher.SubscribeEvent(new Action<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated));
            FikaEventDispatcher.SubscribeEvent(new Action<FikaGameEndedEvent>(OnGameEnded));

            CatchSync.CatchAnnouncer = new Action<string, string>(SendCatch);
            CatchSync.AnimationCue = new Action<string, string>(SendAnimationCue);
            CatchSync.ReloadAbortAnnouncer = new Action<string, bool>(SendReloadAbort);

            Plugin.Log.LogInfo("Fika detected, catch sync enabled");
        }

        private static void OnNetworkManagerCreated(FikaNetworkManagerCreatedEvent args)
        {
            // Covers both roles, since the host has to receive the clients' packets as well
            args.Manager.RegisterPacket(new Action<CatchRoundPacket>(OnPacketReceived));
        }

        private static void OnGameEnded(FikaGameEndedEvent args)
        {
            // A transit keeps the same raid going, so anything still in flight stays valid
            if (args.ExitStatus == ExitStatus.Transit)
                return;

            CatchSync.Clear();
            warnedVersionMismatch = false;
        }

        private static void SendCatch(string profileId, string roundId)
        {
            Send(CatchRoundPacket.KindCatch, profileId, roundId);
        }

        private static void SendAnimationCue(string profileId, string roundId)
        {
            Send(CatchRoundPacket.KindAnimationCue, profileId, roundId);
        }

        private static void SendReloadAbort(string profileId, bool fastHide)
        {
            Send(CatchRoundPacket.KindReloadAborted, profileId, null, fastHide);
        }

        private static void Send(byte kind, string profileId, string roundId, bool fastHide = false)
        {
            var manager = Singleton<IFikaNetworkManager>.Instance;
            if (manager == null)
                return;

            var packet = CatchRoundPacket.Create(kind, ModInfo.Version, profileId, roundId, fastHide);

            // Broadcasting reaches every client from the host and gets relayed by the host from a client
            manager.SendData(ref packet, DeliveryMethod.ReliableOrdered, true);
        }

        private static void OnPacketReceived(CatchRoundPacket packet)
        {
            if (packet.Version != ModInfo.Version)
            {
                OnVersionMismatch(packet.Version);
                return;
            }

            switch (packet.Kind)
            {
                case CatchRoundPacket.KindCatch:
                    CatchSync.SetAnnouncement(packet.ProfileId, packet.RoundId);
                    break;

                case CatchRoundPacket.KindAnimationCue:
                    OnAnimationCue(packet.ProfileId, packet.RoundId);
                    break;

                case CatchRoundPacket.KindReloadAborted:
                    OnReloadAborted(packet.ProfileId, packet.FastHide);
                    break;

                default:
                    Plugin.Log.LogWarning($"Received unknown packet kind {packet.Kind}");
                    break;
            }
        }

        private static void OnAnimationCue(string profileId, string roundId)
        {
            var player = FindHuman(profileId);
            if (player != null)
                ObservedCatch.Play(player, roundId);
        }

        // Fika's own list of humans, so a bot can never be handed anything
        private static FikaPlayer FindHuman(string profileId)
        {
            var handler = Singleton<IFikaNetworkManager>.Instance?.CoopHandler;
            if (handler == null)
                return null;

            foreach (var human in handler.HumanPlayers)
            {
                if (human != null && !human.IsYourPlayer && human.ProfileId == profileId)
                    return human;
            }

            return null;
        }

        private static void OnReloadAborted(string profileId, bool fastHide)
        {
            var player = FindHuman(profileId);
            if (player != null)
                ObservedCatch.AbortReload(player, fastHide);
        }

        private static void OnVersionMismatch(string otherVersion)
        {
            CatchSync.Incompatible = true;

            if (warnedVersionMismatch)
                return;

            warnedVersionMismatch = true;

            Plugin.Log.LogError($"{ModInfo.Name} version mismatch, you have {ModInfo.Version} and another client has {otherVersion}");
            NotificationManager.DisplayWarningNotification(
                $"{ModInfo.Name} version mismatch! You: {ModInfo.Version}, other client: {otherVersion}. "
                + "Catching is off for this raid because different versions may handle the chambered round differently.",
                ENotificationDurationType.Long);

            // The other client cannot see our version until we send it something, so answer once. Both
            // sides go quiet after their own warning, which keeps this from bouncing back and forth.
            Send(CatchRoundPacket.KindCatch, null, null);
        }

        // Kept out of Init so the failed type load surfaces as an exception there rather than
        // while Init itself is being compiled
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ProbePacketAssembly()
        {
            _ = typeof(CatchRoundPacket).TypeHandle;
        }
    }
}
