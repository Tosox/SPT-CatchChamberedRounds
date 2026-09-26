using AnimationSystem;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System.Collections;
using Tosox.CatchChamberedRounds.Configuration;
using Tosox.CatchChamberedRounds.Patches;

namespace Tosox.CatchChamberedRounds.Helpers
{
    /// <summary>
    /// Decides whether a reload should catch the chambered round first, and sets the catch going.
    ///
    /// The animation itself belongs to <see cref="CatchAndReloadOperation"/>, which normally
    /// splices the catch into the reload rather than playing the two end to end. Animation events
    /// are dispatched to whichever operation is current when they fire, not to the one that
    /// started the clip, which is what makes handing the reload over mid animation work at all.
    /// </summary>
    internal static class ChamberDischarge
    {
        private static bool _chaining;
        private static bool _bypassArmed;

        /// <summary>
        /// Set for the duration of EFT's quick reload command, which reaches the reload call
        /// synchronously, so a reload arriving while this is true was asked for in a hurry.
        /// </summary>
        internal static bool QuickReloadRequested { get; set; }

        /// <summary>
        /// True when the player asked to skip the catch, so the round the reload is about to
        /// eject should be left to drop. Consumed once, by whichever throw it applies to.
        /// </summary>
        internal static bool ConsumeBypass()
        {
            if (!_bypassArmed)
                return false;

            _bypassArmed = false;
            return true;
        }

        /// <summary>
        /// Stands down and says why. Every path that hands the reload back to vanilla goes through
        /// here, so with verbose logging on there is a single line in the log naming the reason the
        /// catch animation did not play, rather than silence and a round that turned up in a pocket.
        /// </summary>
        private static bool Decline(string reason)
        {
            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo($"Catch declined: {reason}");

            return false;
        }

        internal static bool TryStart(Player.FirearmController controller, AmmoPack ammoPack, Callback callback)
        {
            // The chained reload must run as vanilla, and is not worth a line in the log
            if (_chaining)
                return false;

            if (controller == null || ammoPack == null)
                return Decline("no controller or no ammo pack");

            if (!Settings.Enabled.Value)
                return false;

            // Only the weapons whose reload opens the bolt and spits the chambered round out
            var weapon = controller.Item;
            if (weapon == null || !weapon.MustBoltBeOpennedForInternalReload)
                return Decline("weapon does not open its bolt for an internal reload");

            // Safety net for a catch that never reached its callback, which happens if the weapon
            // is holstered part way through the animation. The flag would otherwise stay up and
            // wedge the weapon, and a fresh discharge raises it again a few lines below anyway.
            ClearLoadOne(controller);

            if (!weapon.HasChambers || weapon.ChamberAmmoCount <= 0)
                return Decline($"nothing chambered (HasChambers={weapon.HasChambers}, ChamberAmmoCount={weapon.ChamberAmmoCount})");

            // Leave vanilla to reject reloads it would not have started anyway
            var magazine = weapon.GetCurrentMagazine();
            if (magazine == null || controller.Blindfire || !controller.CanStartReload())
                return Decline($"vanilla would not start this reload (magazine={magazine != null}, blindfire={controller.Blindfire}, canStartReload={controller.CanStartReload()})");

            // A full magazine underneath a chambered round is the weapon at its true capacity,
            // and there is nothing left to load. Reloading regardless pulls the chambered round
            // out and then loads nothing, and EFT only chambers a fresh round when it actually
            // loaded one, so the gun ends up empty on a full magazine and cannot be fired.
            // Refuse the reload instead and leave the weapon exactly as it is.
            if (magazine.Count >= magazine.MaxCount || ammoPack.AmmoCount <= 0)
            {
                callback?.Fail("Weapon is already fully loaded");
                return true;
            }

            // Four in the magazine and one chambered is already the five rounds a reload can
            // leave you holding. Going through with it takes the chambered round out, feeds a
            // replacement in from your gear, and chambers that instead, so the count never
            // moves and the animation is spent for nothing. Refuse it unless asked not to.
            if (Settings.BlockPointlessReloads.Value &&
                magazine.Count + weapon.ChamberAmmoCount >= magazine.MaxCount)
            {
                callback?.Fail("Reloading would not add any rounds");
                return true;
            }

            // One key catches the round, the other reloads at vanilla speed and lets it drop.
            // Quick reload is the fast one by default, and inverting swaps which key is which.
            if (Settings.QuickReloadBypass.Value && QuickReloadRequested != Settings.InvertReloadKeys.Value)
            {
                _bypassArmed = true;
                return Decline($"reload key asked to skip the catch (quickReload={QuickReloadRequested}, invert={Settings.InvertReloadKeys.Value})");
            }

            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null || !ReferenceEquals(player.HandsController, controller))
                return Decline($"reload is not the main player's (player={player != null}, handsMatch={player != null && ReferenceEquals(player.HandsController, controller)})");

            if (!(weapon.Chambers[0].ContainedItem is Ammo chambered) || chambered.IsUsed)
                return Decline($"chamber holds no live round (contained={weapon.Chambers[0].ContainedItem?.GetType().Name ?? "null"})");

            var inventory = player.InventoryController;
            if (inventory?.Inventory?.Equipment == null)
                return Decline("no inventory to place the round into");

            // Read what this weapon's animator has to say about itself, once, here rather than on
            // every frame of the animation. The splice needs it to find its cut point and target.
            var profile = AnimatorProfile.Read(
                (controller.FirearmsAnimator?.Animator as UnityAnimatorWrapper)?.Animator);

            // A weapon the splice cannot read is not an error, it just plays both animations in
            // sequence like the mod always used to. Worth a line when you are wondering why.
            if (Settings.VerboseLogging.Value && profile != null && profile.FeedStateProblem != null)
                Plugin.Log.LogInfo($"Will not splice {weapon.LocalizedName()}: {profile.FeedStateProblem}");

            // With the animation off, stow the round right here, while it is still sitting in
            // the chamber slot and therefore still has a parent to be moved out of. Waiting for
            // the reload to eject it would hand us an item with no parent, which is a far more
            // awkward thing to place. The reload then finds an empty chamber and throws nothing.
            if (!Settings.PlayCatchAnimation.Value)
            {
                _bypassArmed = false;
                InventoryPlacement.Commit(chambered, inventory);
                return Decline("catch animation turned off, round stowed silently");
            }

            // Check there is somewhere to put the round before committing to the animation,
            // otherwise the player would watch a catch animation and still lose it
            var preview = InventoryPlacement.Find(chambered, inventory, true);
            if (preview.Failed || preview.Value == null)
                return Decline($"nowhere to put the round ({preview.Error?.ToString() ?? "no result"})");

            var destination = InventoryPlacement.AddressOf(preview.Value);
            if (destination == null)
            {
                // The placement worked but its address could not be read back. Worth naming the
                // result type, because that is the thing AddressOf has to learn to understand.
                return Decline($"could not read an address out of a {preview.Value.GetType().FullName}");
            }

            // A non-null destination address is what makes the operation raise LoadOne, which
            // is the animator's catch-and-stow variant rather than a plain eject
            var discharge = controller.InitiateOperation<CatchAndReloadOperation>();

            // Never let a stale bypass from an earlier reload leak into this one
            _bypassArmed = false;

            // And never let a skip armed by an abandoned splice swallow this reload's animation
            SuppressReloadAnimationPatch.Clear();

            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo($"Catch starting: discharging {chambered.LocalizedName()} to {destination}");

            discharge.StartCatch(chambered, destination, inventory, ammoPack, profile, callback);

            return true;
        }

        /// <summary>
        /// Puts the animator's <c>LoadOne</c> flag back down.
        ///
        /// The discharge raises it from the destination address we hand it, and in vanilla that is
        /// only ever another chamber, so a LoadCartridgeToChamber follows and lowers the flag again
        /// when it chambers the round. This mod raises it to get the catch variant of the animation
        /// and then chains a reload instead, so nothing lowers it and the flag stays up for as long
        /// as the weapon is in hand. An empty Mosin with the flag still up loops the load-a-round
        /// animation and cannot be interrupted, so it has to come down the moment the catch is over.
        /// </summary>
        private static void ClearLoadOne(Player.FirearmController controller)
        {
            controller?.FirearmsAnimator?.LoadOneTrigger(false);
        }

        /// <summary>
        /// The sequential path: the catch animation ran to its end, so the round is stowed and the
        /// reload is issued behind it. Used when the splice is turned off or could not be made.
        /// </summary>
        internal static void ChainReload(IResult result, Player.FirearmController controller,
            InventoryController inventory, Item chambered, AmmoPack ammoPack, Callback callback)
        {
            ClearLoadOne(controller);

            if (result == null || result.Failed)
            {
                _bypassArmed = false;
                Plugin.Log.LogWarning($"Chamber discharge did not complete: {result?.Error?.ToString() ?? "no result"}");
                callback?.Fail("Chamber discharge did not complete");
                return;
            }

            if (Settings.VerboseLogging.Value)
                Plugin.Log.LogInfo("Catch animation finished, chaining the reload");

            // This runs from inside the operation, before it re-reads ChamberAmmoCount to
            // update the animator, which is exactly where vanilla completes the inventory move
            if (!InventoryPlacement.Commit(chambered, inventory))
            {
                callback?.Fail("No room left for the chambered round");
                return;
            }

            Plugin.Instance.StartCoroutine(ReloadNextFrame(controller, ammoPack, callback));
        }

        private static IEnumerator ReloadNextFrame(Player.FirearmController controller, AmmoPack ammoPack, Callback callback)
        {
            // Let the discharge operation finish unwinding into idle before replacing it
            yield return null;

            _chaining = true;
            try
            {
                controller.ReloadWithAmmo(ammoPack, callback);
            }
            finally
            {
                _chaining = false;
            }
        }
    }
}
