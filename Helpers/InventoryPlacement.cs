using Diz.LanguageExtensions;
using Diz.Utils;
using EFT.InventoryLogic;

namespace Tosox.CatchChamberedRounds.Helpers
{
    internal static class InventoryPlacement
    {
        private const ItemManipulator.EMoveItemOrder Order =
            ItemManipulator.EMoveItemOrder.UnloadAmmo |
            ItemManipulator.EMoveItemOrder.IgnoreItemParent;

        internal static OperationResult<IItemOperationResult> Find(Item patron, InventoryController controller, bool simulate)
        {
            var targets = controller.Inventory.Equipment.ToEnumerable<InventoryEquipment>();
            return ItemManipulator.QuickFindAppropriatePlace(patron, controller, targets, Order, simulate);
        }

        internal static ItemAddress AddressOf(IItemOperationResult placement)
        {
            if (placement is IToEmptyAddressResult move)
                return move.To;

            if (placement is ITransferOrMergeResult merge)
                return merge.TargetItem?.Parent;

            return null;
        }

        internal static bool Commit(Item patron, InventoryController controller)
        {
            if (patron.CurrentAddress == null)
                return CommitDetached(patron, controller);

            var placement = Find(patron, controller, false);
            if (placement.Failed || placement.Value == null)
                return false;

            placement.Value.RaiseEvents(controller, CommandStatus.Begin);
            placement.Value.RaiseEvents(controller, CommandStatus.Succeed);
            return true;
        }

        private static bool CommitDetached(Item patron, InventoryController controller)
        {
            var equipment = controller.Inventory.Equipment;

            foreach (var container in equipment.GetPrioritizedContainersForLoot(patron))
            {
                if (!container.TryFindLocationForItem(patron, out var address))
                    continue;

                var add = ItemManipulator.Add(patron, address, controller, false);
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
