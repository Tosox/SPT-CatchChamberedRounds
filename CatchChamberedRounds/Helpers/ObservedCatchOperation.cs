using AnimationSystem;
using EFT;
using EFT.InventoryLogic;
using Tosox.CatchChamberedRounds.Sync;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal class ObservedCatchOperation : Player.FirearmController.DischargeOperation
    {
        private const string DischargeState = "DISCHARGE";

        private AnimatorProfile _profile;

        public ObservedCatchOperation(Player.FirearmController controller) : base(controller)
        {
        }

        public override void Reset()
        {
            _profile = null;
            base.Reset();
        }

        internal void StartCatch(Ammo chambered, ItemAddress destination, AnimatorProfile profile)
        {
            _profile = profile;

            // The discharge writes aim, blindfire and the left hand on its way in, and on an observed
            // player those belong to Fika, so they go back as they were
            var hands = Controller.FirearmsAnimator?.Animator;
            var aiming = Controller.IsAiming;
            var blindFire = Player.MovementContext.BlindFire;
            var reloadFloat = Player.BodyAnimatorCommon.GetFloat(PlayerAnimator.RELOAD_FLOAT_PARAM_HASH);
            var useLeftHand = hands != null
                && hands.GetBool(AnimationControllerParametersTable.BOOL_USELEFTHAND);

            Start(chambered, 0, destination, _ => { });

            Controller.IsAiming = aiming;
            Player.MovementContext.SetBlindFire(blindFire);
            Player.BodyAnimatorCommon.SetFloat(PlayerAnimator.RELOAD_FLOAT_PARAM_HASH, reloadFloat);

            if (hands != null)
                AnimationControllerParametersTable.SetUseLeftHand(hands, useLeftHand);

            EnterDischargeState();
        }

        // Fika fast forwards this the instant the owner's reload packet lands, which is the instant
        // the owner spliced
        public override void FastForward()
        {
            Splice();
            base.FastForward();
        }

        public override void OnEnd()
        {
            // The discharge raises this to get the catch variant, and on a client that only watches
            // there is no chambering afterwards to put it back down
            Controller?.FirearmsAnimator?.LoadOneTrigger(false);
            base.OnEnd();
        }

        private void Splice()
        {
            if (_profile == null || _profile.FeedState == 0 || State != Player.EOperationState.Executing)
                return;

            var animator = Controller?.FirearmsAnimator;
            var unity = animator.UnityAnimator();
            if (unity == null)
                return;

            // The same test the owner makes before splicing. A weapon whose catch state says nothing
            // about its bolt closing is one the owner plays in full, so it is not spliced here either
            var current = unity.GetCurrentAnimatorStateInfo(FirearmsAnimator.HANDS_LAYER_INDEX);
            if (!_profile.TryGetBoltCloseTime(current, out _))
                return;

            // The splice skips the reload's opening, which is where the round would have been thrown,
            // so it is stowed right here instead
            if (!StowChamberedRound())
                return;

            var leavingState = current.shortNameHash;

            ReloadSplice.Arm(animator, _profile, Controller.AnimationEventsEmitter, leavingState);
        }

        // The transition out of idle only comes round on the loop, and the load-one flag sends it to
        // the load-a-round branch anyway, so the state is entered rather than waited for
        private void EnterDischargeState()
        {
            var unity = Controller?.FirearmsAnimator.UnityAnimator();
            if (unity == null)
                return;

            var state = UnityEngine.Animator.StringToHash(DischargeState);
            if (!unity.HasState(FirearmsAnimator.HANDS_LAYER_INDEX, state))
                return;

            // Cut rather than blend, so the hands layer arrives at the same instant the left hand
            // reacts to the parameters instead of a tenth of a second behind it
            unity.Play(state, FirearmsAnimator.HANDS_LAYER_INDEX, 0f);
        }

        private bool StowChamberedRound()
        {
            if (!Weapon.HasChambers || !(Weapon.Chambers[0].ContainedItem is Ammo chambered))
                return false;

            // Only if the owner got the round into their own gear, otherwise it belongs on the floor
            // and the untouched reload is what puts it there
            if (!CatchSync.TakeAnnouncement(Player.ProfileId, chambered.Id))
                return false;

            return InventoryPlacement.Commit(chambered, Player.InventoryController);
        }
    }
}
