using EFT.InventoryLogic;

namespace Tosox.CatchChamberedRounds.Helpers
{
    /// <summary>
    /// Shared "put this cartridge somewhere sensible on the player" logic.
    /// </summary>
    internal static class InventoryPlacement
    {
        /// <summary>
        /// UnloadAmmo is the order EFT itself uses when you drag a round out of the chamber
        /// on the inventory screen, so the round lands where the player expects and merges
        /// into an existing stack when there is one. IgnoreItemParent is added because the
        /// patron is either still sitting in the chamber slot or already detached, and in
        /// neither case is the weapon a sensible place to prioritise putting it back into.
        /// </summary>
        private const InteractionsHandlerClass.EMoveItemOrder Order =
            InteractionsHandlerClass.EMoveItemOrder.UnloadAmmo |
            InteractionsHandlerClass.EMoveItemOrder.IgnoreItemParent;

        internal static GStruct154<GInterface424> Find(Item patron, InventoryController controller, bool simulate)
        {
            var targets = controller.Inventory.Equipment.ToEnumerable<InventoryEquipment>();
            return InteractionsHandlerClass.QuickFindAppropriatePlace(patron, controller, targets, Order, simulate);
        }

        /// <summary>
        /// The address a simulated placement resolved to, either a free spot or the stack it
        /// would merge into. Used to tell the animator that the round has somewhere to go.
        /// </summary>
        internal static ItemAddress AddressOf(GInterface424 placement)
        {
            if (placement is GInterface428 move)
                return move.To;

            if (placement is GInterface429 merge)
                return merge.TargetItem?.Parent;

            return null;
        }

        /// <summary>
        /// Actually puts the cartridge into the player's gear and raises the events the
        /// inventory UI needs to notice it. Returns false when there is no room.
        /// </summary>
        internal static bool Commit(Item patron, InventoryController controller)
        {
            // EFT's own placement helper walks its candidate slots with
            // "!this.item.Parent.Equals(address)", and Item.Parent throws outright on an item
            // that has already been detached from the weapon. Reload operations hand us exactly
            // such an item, so those have to be placed the long way round. CurrentAddress is
            // the same value without the throwing, which is what makes it safe to test here.
            if (patron.CurrentAddress == null)
                return CommitDetached(patron, controller);

            var placement = Find(patron, controller, false);
            if (placement.Failed || placement.Value == null)
                return false;

            placement.Value.RaiseEvents(controller, CommandStatus.Begin);
            placement.Value.RaiseEvents(controller, CommandStatus.Succeed);
            return true;
        }

        /// <summary>
        /// Places a cartridge that no longer belongs to anything, by finding it a free spot and
        /// adding it there. Unlike the normal path this cannot merge into an existing stack,
        /// but it does keep the round, which is the point.
        /// </summary>
        private static bool CommitDetached(Item patron, InventoryController controller)
        {
            var equipment = controller.Inventory.Equipment;

            foreach (var container in equipment.GetPrioritizedContainersForLoot(patron))
            {
                if (!container.TryFindLocationForItem(patron, out var address))
                    continue;

                var add = InteractionsHandlerClass.Add(patron, address, controller, false);
                if (add.Failed || add.Value == null)
                    continue;

                add.Value.RaiseEvents(controller, CommandStatus.Begin);
                add.Value.RaiseEvents(controller, CommandStatus.Succeed);
                return true;
            }

            return false;
        }
    }
}
