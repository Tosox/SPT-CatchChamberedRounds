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
            // Load the packet assembly now, so a broken install is reported here and not on the first send
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

            // Explicit delegates, since cached lambdas land in a class that loads Fika.Core too early
            FikaEventDispatcher.SubscribeEvent(new Action<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated));
            FikaEventDispatcher.SubscribeEvent(new Action<FikaGameEndedEvent>(OnGameEnded));

            CatchSync.CatchAnnouncer = new Action<string, string>(SendCatch);
            CatchSync.AnimationCue = new Action<string, string>(SendAnimationCue);
            CatchSync.ReloadAbortAnnouncer = new Action<string, bool>(SendReloadAbort);

            Plugin.Log.LogInfo("Fika detected, catch sync enabled");
        }

        private static void OnNetworkManagerCreated(FikaNetworkManagerCreatedEvent args)
        {
            args.Manager.RegisterPacket(new Action<CatchRoundPacket>(OnPacketReceived));
        }

        private static void OnGameEnded(FikaGameEndedEvent args)
        {
            // A transit continues the same raid
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

            // Broadcast, so the host also relays a client's packet to everyone
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

            // Reply once so the other side learns our version. Each side warns only once, so no loop
            Send(CatchRoundPacket.KindCatch, null, null);
        }

        // Separate method so a missing assembly throws when called, not when Init is compiled
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ProbePacketAssembly()
        {
            _ = typeof(CatchRoundPacket).TypeHandle;
        }
    }
}
