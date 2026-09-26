using AnimationSystem;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System;
using Tosox.CatchChamberedRounds.Configuration;
using Tosox.CatchChamberedRounds.Patches;
using UnityEngine;

namespace Tosox.CatchChamberedRounds.Helpers
{
    /// <summary>
    /// The vanilla chamber discharge, cut short and spliced into the reload.
    ///
    /// Left alone the two animations spend about a second and a quarter undoing each other: the
    /// discharge closes the bolt, drops to idle, and the reload then opens the same bolt again.
    /// Both halves are redundant once the round is in your pocket and the bolt is already back, so
    /// this leaves the discharge just before it starts closing and crossfades straight into the
    /// round-feeding part of the reload.
    ///
    /// Nothing about which state or which moment is hardcoded. Every weapon carries its own baked
    /// animation event table, and the two things the splice needs are read out of it: the bolt
    /// closes at the discharge's OffBoltCatch, and the state that feeds rounds into the magazine is
    /// the one that raises AddAmmoInMag. Reading them rather than naming them is what lets this
    /// work across weapons that call the same states different things, and on modded weapons whose
    /// state names are in no table at all.
    ///
    /// A weapon whose discharge has no OffBoltCatch never splices, which is correct rather than
    /// merely safe: no bolt closes, so there is nothing to cut away.
    /// </summary>
    internal class CatchAndReloadOperation : Player.FirearmController.DischargeOperation
    {
        /// <summary>
        /// How far before the bolt starts closing to cut away, as a fraction of the catch animation.
        /// The moment itself is read off each weapon, so this is only the room left in front of it,
        /// and it wants to be small: the round is in hand from about 0.43 and cutting early enough
        /// to matter would start hiding the part worth watching. Settled by eye against the Mosin
        /// at 0.723 and confirmed on the M700, AXMC and TRG, whose bolts close at 0.655.
        /// </summary>
        private const float BoltCloseMargin = 0.03f;

        /// <summary>
        /// How long the blend into the reload takes, in normalized time. MagCheckInterrupt uses the
        /// same figure for the same reason: much more than this and the hands read as skipping ahead
        /// rather than continuing a motion.
        /// </summary>
        private const float BlendDuration = 0.10f;

        private AmmoPack _ammoPack;
        private Callback _reloadCallback;
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
            _inventory = null;
            _chambered = null;
            _spliced = false;
            _profile = null;
            base.Reset();
        }

        /// <summary>
        /// Starts the catch, carrying everything the reload will need once the round is stowed.
        /// </summary>
        internal void StartCatch(Ammo chambered, ItemAddress destination, InventoryController inventory,
            AmmoPack ammoPack, AnimatorProfile profile, Callback callback)
        {
            _profile = profile;
            _ammoPack = ammoPack;
            _reloadCallback = callback;
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
                CrossFadeIntoReload(animator);

                if (Settings.VerboseLogging.Value)
                    Plugin.Log.LogInfo($"Spliced catch into reload at nt {normalizedTime:F3}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to splice the catch into the reload: {ex}");
            }
        }

        /// <summary>
        /// Finishes the discharge by hand, doing what <c>OnMagPuttedToRig</c> would have done at the
        /// end of the animation minus the two things that would work against the reload: it does not
        /// hop through Idling, since the reload replaces this operation immediately anyway, and it
        /// does not call SetCanReload(false), which would stop the feeding state looping before it
        /// fed a round.
        ///
        /// The flag is raised first because leaving the state flushes every event the discharge had
        /// left, MagHide included, and that must not run the vanilla version on top of this.
        /// </summary>
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

        /// <summary>
        /// Starts the vanilla reload with its own animation suppressed, because
        /// <c>ReloadInternalMagBase.Start</c> fires the Reload trigger unconditionally and that
        /// would send the animator to the reload's opening state and undo the crossfade.
        /// </summary>
        private void StartReload()
        {
            SuppressReloadAnimationPatch.SkipNext();
            Controller.InitiateOperation<Player.FirearmController.ReloadInternalMagBase>()
                .Start(_ammoPack, _reloadCallback);
        }

        private void CrossFadeIntoReload(Animator animator)
        {
            animator.CrossFade(
                _profile.FeedState,
                BlendDuration,
                FirearmsAnimator.HANDS_LAYER_INDEX,
                0f);

            // Two separate jobs, and dropping the flush only does the first of them. Suppressing
            // the discharge's leftover OffBoltCatch keeps the animator believing the bolt is open,
            // which it is, and silences the close nobody can see. But SetBoltCatch also drives
            // IdlePosition and the weight of the bolt catch layer, and that layer is what actually
            // holds the bolt open over the top of the blending hands layer. Without something
            // asserting it, the hands layer blend shows through as the bolt starting to close.
            Plugin.Instance.StartCoroutine(HoldBoltOpenThroughFlush());
        }

        /// <summary>
        /// Holds the bolt in its open pose across the splice, and lifts the event suppression once
        /// the flush has been and gone. The flush is queued by the animator and emitted later in the
        /// frame, so both have to outlive the splice itself by a frame or two.
        /// </summary>
        private System.Collections.IEnumerator HoldBoltOpenThroughFlush()
        {
            for (var frame = 0; frame < 2; frame++)
            {
                if (Controller?.FirearmsAnimator != null)
                    Controller.FirearmsAnimator.SetBoltCatch(true);

                yield return null;
            }

            SuppressStaleEventsPatch.Clear();
        }

        /// <summary>
        /// The sequential path, still in place for when the splice is off, refused, or impossible.
        /// </summary>
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
