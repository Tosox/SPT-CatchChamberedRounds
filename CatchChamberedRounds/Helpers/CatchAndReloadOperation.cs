using AnimationSystem;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections;
using Tosox.CatchChamberedRounds.Configuration;
using Tosox.CatchChamberedRounds.Patches;
using UnityEngine;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal class CatchAndReloadOperation : Player.FirearmController.DischargeOperation
    {
        private const float BoltCloseMargin = 0.03f;
        private const float BlendDuration = 0.10f;
        private const int MaxFlushFrames = 12;

        private AmmoPack _ammoPack;
        private Callback _reloadCallback;
        private Callback _reloadStartCallback;
        private InventoryController _inventory;
        private Item _chambered;
        private bool _spliced;

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
            _profile = null;
            base.Reset();
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

            Start(chambered, 0, destination, new Callback(OnDischargeFinished));
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (_spliced || _profile == null || _profile.FeedState == 0 || State != Player.EOperationState.Executing)
                return;

            var animator = ResolveAnimator();
            if (animator == null)
                return;

            var current = animator.GetCurrentAnimatorStateInfo(FirearmsAnimator.HANDS_LAYER_INDEX);

            if (!_profile.TryGetBoltCloseTime(current, out var boltCloses))
                return;

            // Cut a hair before the bolt starts closing, so the close never plays
            if (current.normalizedTime < boltCloses - BoltCloseMargin)
                return;

            Splice(animator, current.shortNameHash, current.normalizedTime);
        }

        private void Splice(Animator animator, int leavingState, float normalizedTime)
        {
            _spliced = true;

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

                // Armed before anything else moves, because the flush lands as soon as the
                // crossfade starts and the whole point is to be ready for it
                SuppressStaleEventsPatch.SuppressFrom(Controller.AnimationEventsEmitter, leavingState);

                EndDischarge();
                StartReload();
                CrossFadeIntoReload(animator, leavingState);

                if (Settings.VerboseLogging.Value)
                    Plugin.Log.LogInfo($"Spliced catch into reload at nt {normalizedTime:F3}");
            }
            catch (Exception ex)
            {
                // Nothing downstream lowers either of these once the splice gave up on itself
                SuppressReloadAnimationPatch.Clear();
                SuppressStaleEventsPatch.Clear();

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
            SuppressReloadAnimationPatch.SkipNext(FirearmsAnimator);
            Controller.InitiateOperation<Player.FirearmController.ReloadInternalMagBase>()
                .Start(_ammoPack, _reloadCallback);

            // Vanilla raises this once the reload is under way and leaves it null, Fika hangs the
            // packet that replays the reload on the other clients off it
            _reloadStartCallback?.Succeed();
        }

        private void CrossFadeIntoReload(Animator animator, int leavingState)
        {
            animator.CrossFade(_profile.FeedState, BlendDuration, FirearmsAnimator.HANDS_LAYER_INDEX, 0f);
            Plugin.Instance.StartCoroutine(HoldBoltOpenThroughFlush(leavingState));
        }

        private IEnumerator HoldBoltOpenThroughFlush(int leavingState)
        {
            for (var frame = 0; frame < MaxFlushFrames; frame++)
            {
                if (Controller?.FirearmsAnimator != null)
                    Controller.FirearmsAnimator.SetBoltCatch(true);

                yield return null;

                var animator = ResolveAnimator();
                if (animator == null ||
                    animator.GetCurrentAnimatorStateInfo(FirearmsAnimator.HANDS_LAYER_INDEX).shortNameHash != leavingState)
                    break;
            }

            SuppressStaleEventsPatch.Clear();
        }

        private void OnDischargeFinished(IResult result)
        {
            if (_spliced)
                return;

            ChamberDischarge.ChainReload(result, Controller, _inventory, _chambered, _ammoPack, _reloadCallback);
        }

        private Animator ResolveAnimator()
        {
            return (Controller?.FirearmsAnimator?.Animator as UnityAnimatorWrapper)?.Animator;
        }
    }
}
