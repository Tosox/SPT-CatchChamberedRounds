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
            // Placement helpers all read Item.Parent, which throws instead of returning null.
            // An owner of its own is what lets this one merge into a stack rather than take a cell.
            if (patron.CurrentAddress == null)
            {
                var loose = new ItemController(patron, patron.Id, patron.ShortName);
                patron.CurrentAddress = loose.CreateItemAddress();
            }

            var placement = Find(patron, controller, false);
            if (placement.Failed || placement.Value == null)
                return false;

            placement.Value.RaiseEvents(controller, CommandStatus.Begin);
            placement.Value.RaiseEvents(controller, CommandStatus.Succeed);
            return true;
        }
    }
}
