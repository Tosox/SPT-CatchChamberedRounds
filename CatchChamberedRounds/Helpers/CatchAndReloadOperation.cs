using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System;
using Tosox.CatchChamberedRounds.Configuration;
using Tosox.CatchChamberedRounds.Sync;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal class CatchAndReloadOperation : Player.FirearmController.DischargeOperation
    {
        private const float BoltCloseMargin = 0.03f;

        private AmmoPack _ammoPack;
        private Callback _reloadCallback;
        private Callback _reloadStartCallback;
        private InventoryController _inventory;
        private Item _chambered;
        private bool _spliced;
        private bool _spliceAttempted;
        private bool _weaponMarked;

        private AnimatorProfile _profile;

        public CatchAndReloadOperation(Player.FirearmController controller) : base(controller)
        {
        }

        public override void Reset()
        {
            _ammoPack = null;
            _reloadCallback = null;
            _reloadStartCallback = null;
            _inventory = null;
            _chambered = null;
            _spliced = false;
            _spliceAttempted = false;
            _weaponMarked = false;
            _profile = null;
            base.Reset();
        }

        public override void OnEnd()
        {
            // Nothing else puts the flag this operation raised back down once the weapon is someone
            // else's problem and left up it wedges an empty one into a load-a-round loop
            Controller?.FirearmsAnimator?.LoadOneTrigger(false);

            UnmarkWeapon();
            base.OnEnd();
        }

        internal void StartCatch(Ammo chambered, ItemAddress destination, InventoryController inventory,
            AmmoPack ammoPack, AnimatorProfile profile, Callback callback, Callback startCallback)
        {
            _profile = profile;
            _ammoPack = ammoPack;
            _reloadCallback = callback;
            _reloadStartCallback = startCallback;
            _inventory = inventory;
            _chambered = chambered;
            _spliced = false;
            _spliceAttempted = false;

            Start(chambered, 0, destination, new Callback(OnDischargeFinished));

            MarkWeapon();
        }

        private void MarkWeapon()
        {
            if (_weaponMarked)
                return;

            _weaponMarked = WeaponMark.Raise(Controller);
        }

        private void UnmarkWeapon()
        {
            if (!_weaponMarked)
                return;

            _weaponMarked = false;
            WeaponMark.Lower(Controller);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (_spliceAttempted || _profile == null || _profile.FeedState == 0 || State != Player.EOperationState.Executing)
                return;

            var animator = Controller?.FirearmsAnimator.UnityAnimator();
            if (animator == null)
                return;

            var current = animator.GetCurrentAnimatorStateInfo(FirearmsAnimator.HANDS_LAYER_INDEX);

            if (!_profile.TryGetBoltCloseTime(current, out var boltCloses))
                return;

            // Cut a hair before the bolt starts closing, so the close never plays
            if (current.normalizedTime < boltCloses - BoltCloseMargin)
                return;

            Splice(current.shortNameHash, current.normalizedTime);
        }

        private void Splice(int leavingState, float normalizedTime)
        {
            // One go either way, so a refusal does not come back every frame
            _spliceAttempted = true;

            try
            {
                // The round is still sitting in the chamber slot at this point, so it has a parent
                // to be moved out of and can merge into a stack you already carry
                if (!InventoryPlacement.Commit(_chambered, _inventory))
                {
                    // Nowhere to put it after all. Let the discharge play out and drop the round,
                    // which is what the sequential path would have done too.
                    Plugin.Log.LogWarning("No room for the chambered round at the splice, letting the catch finish");
                    return;
                }

                _spliced = true;

                // Tell the other clients to catch this round as well
                CatchSync.CatchAnnouncer?.Invoke(Controller._player.ProfileId, _chambered.Id);

                // Armed before anything else moves, because the flush lands as soon as the
                // crossfade starts and the whole point is to be ready for it
                ReloadSplice.Arm(FirearmsAnimator, _profile, Controller.AnimationEventsEmitter, leavingState);

                EndDischarge();
                StartReload();

                if (Settings.VerboseLogging.Value)
                    Plugin.Log.LogInfo($"Spliced catch into reload at nt {normalizedTime:F3}");
            }
            catch (Exception ex)
            {
                // Nothing downstream lowers the splice once it gave up on itself
                ReloadSplice.Clear();

                Plugin.Log.LogError($"Failed to splice the catch into the reload: {ex}");
            }
        }

        private void EndDischarge()
        {
            _magPuttedToRig = true;
            State = Player.EOperationState.Finished;

            FirearmsAnimator.Discharge(false);

            // Raised by the discharge to get the catch variant, and nothing downstream lowers it.
            // Left up it wedges the weapon into a looping load-a-round animation.
            FirearmsAnimator.LoadOneTrigger(false);

            FirearmsAnimator.SetAmmoInChamber(Weapon.ChamberAmmoCount);
            FirearmsAnimator.SetShellsInWeapon(Weapon.ShellsInWeaponCount);

            Firearms.DestroyPatronInWeapon(_chamberIndex);
        }

        private void StartReload()
        {
            var reload = Controller.InitiateOperation<Player.FirearmController.ReloadInternalMagBase>();
            reload.Start(_ammoPack, _reloadCallback);

            // Vanilla raises this once the reload is under way and leaves it null, Fika hangs the
            // packet that replays the reload on the other clients off it
            _reloadStartCallback?.Succeed();
        }

        private void OnDischargeFinished(IResult result)
        {
            if (_spliced)
                return;

            ChamberDischarge.ChainReload(result, Controller, _inventory, _chambered, _ammoPack, _reloadCallback,
                _reloadStartCallback);
        }
    }
}
