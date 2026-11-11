using AnimationSystem;
using EFT;
using EFT.InventoryLogic;
using Tosox.CatchChamberedRounds.Sync;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal class ObservedCatchOperation : Player.FirearmController.DischargeOperation
    {
        private AnimatorProfile _profile;

        public ObservedCatchOperation(Player.FirearmController controller) : base(controller)
        {
        }

        public override void Reset()
        {
            _profile = null;
            base.Reset();
        }

        internal void StartCatch(Ammo chambered, AnimatorProfile profile)
        {
            _profile = profile;

            // Start overwrites aim, blindfire and the left hand, which Fika owns on an observed player
            var hands = Controller.FirearmsAnimator?.Animator;
            var aiming = Controller.IsAiming;
            var blindFire = Player.MovementContext.BlindFire;
            var reloadFloat = Player.BodyAnimatorCommon.GetFloat(PlayerAnimator.RELOAD_FLOAT_PARAM_HASH);
            var useLeftHand = hands != null
                && hands.GetBool(AnimationControllerParametersTable.BOOL_USELEFTHAND);

            Start(chambered, 0, null, _ => { });

            Controller.IsAiming = aiming;
            Player.MovementContext.SetBlindFire(blindFire);
            Player.BodyAnimatorCommon.SetFloat(PlayerAnimator.RELOAD_FLOAT_PARAM_HASH, reloadFloat);

            if (hands != null)
                AnimationControllerParametersTable.SetUseLeftHand(hands, useLeftHand);
        }

        // Fika fast-forwards this when the owner's reload packet arrives, which is when the owner spliced
        public override void FastForward()
        {
            Splice();
            base.FastForward();
        }

        private void Splice()
        {
            if (_profile == null || _profile.FeedState == 0 || State != Player.EOperationState.Executing)
                return;

            var animator = Controller?.FirearmsAnimator;
            var unity = animator.UnityAnimator();
            if (unity == null)
                return;

            // Same check as the owner, without a bolt-close event the owner didn't splice either
            var current = unity.GetCurrentAnimatorStateInfo(FirearmsAnimator.HANDS_LAYER_INDEX);
            if (!_profile.TryGetBoltCloseTime(current, out _))
                return;

            // The splice skips the reload's opening, where the round would be thrown, so stow it here
            if (!StowChamberedRound())
                return;

            var leavingState = current.shortNameHash;

            ReloadSplice.Arm(animator, _profile, Controller.AnimationEventsEmitter, leavingState);
        }

        private bool StowChamberedRound()
        {
            if (!Weapon.HasChambers || !(Weapon.Chambers[0].ContainedItem is Ammo chambered))
                return false;

            // Only if the owner stowed it, otherwise the reload drops it as usual
            if (!CatchSync.TakeAnnouncement(Player.ProfileId, chambered.Id))
                return false;

            return InventoryPlacement.Commit(chambered, Player.InventoryController);
        }
    }
}
