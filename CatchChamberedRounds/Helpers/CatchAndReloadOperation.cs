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
            UnmarkWeapon();
            base.OnEnd();
        }

        internal void StartCatch(Ammo chambered, InventoryController inventory, AmmoPack ammoPack,
            AnimatorProfile profile, Callback callback, Callback startCallback)
        {
            _profile = profile;
            _ammoPack = ammoPack;
            _reloadCallback = callback;
            _reloadStartCallback = startCallback;
            _inventory = inventory;
            _chambered = chambered;
            _spliced = false;
            _spliceAttempted = false;

            // No destination, it would raise LoadOne and make the left hand start feeding a round
            Start(chambered, 0, null, new Callback(OnDischargeFinished));

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

            // Splice just before the bolt closes, so the close never plays
            if (current.normalizedTime < boltCloses - BoltCloseMargin)
                return;

            Splice(current.shortNameHash, current.normalizedTime);
        }

        private void Splice(int leavingState, float normalizedTime)
        {
            // Only try once, so a failure isn't retried every frame
            _spliceAttempted = true;

            try
            {
                // The round is still in the chamber slot, so it can be moved and merged into a stack
                if (!InventoryPlacement.Commit(_chambered, _inventory))
                {
                    // No room after all, so finish the discharge and drop the round like the unspliced path
                    Plugin.Log.LogWarning("No room for the chambered round at the splice, letting the catch finish");
                    return;
                }

                _spliced = true;

                CatchSync.CatchAnnouncer?.Invoke(Controller._player.ProfileId, _chambered.Id);

                // Arm first, the stale events flush as soon as the crossfade starts
                ReloadSplice.Arm(FirearmsAnimator, _profile, Controller.AnimationEventsEmitter, leavingState);

                EndDischarge();
                StartReload();

                if (Settings.VerboseLogging.Value)
                    Plugin.Log.LogInfo($"Spliced catch into reload at nt {normalizedTime:F3}");
            }
            catch (Exception ex)
            {
                // Nothing else clears a splice that failed halfway
                ReloadSplice.Clear();

                Plugin.Log.LogError($"Failed to splice the catch into the reload: {ex}");
            }
        }

        private void EndDischarge()
        {
            _magPuttedToRig = true;
            State = Player.EOperationState.Finished;

            FirearmsAnimator.Discharge(false);
            FirearmsAnimator.SetAmmoInChamber(Weapon.ChamberAmmoCount);
            FirearmsAnimator.SetShellsInWeapon(Weapon.ShellsInWeaponCount);

            Firearms.DestroyPatronInWeapon(_chamberIndex);
        }

        private void StartReload()
        {
            var reload = Controller.InitiateOperation<Player.FirearmController.ReloadInternalMagBase>();
            reload.Start(_ammoPack, _reloadCallback);

            // Fika sends the reload to the other clients from this callback
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
