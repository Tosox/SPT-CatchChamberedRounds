using EFT;
using EFT.InventoryLogic;

namespace Tosox.CatchChamberedRounds.Helpers
{
    // A reload marks its weapon as being modified, which is what makes the game refuse a weapon swap
    // part way through one. Raised and lowered in pairs, or the weapon can never be drawn again
    internal static class WeaponMark
    {
        internal static bool Raise(Player.FirearmController controller)
        {
            var weapon = controller?.Item;
            if (weapon?.Parent == null)
                return false;

            weapon.Parent.RaiseRemoveEvent(weapon, CommandStatus.Begin, controller._player.InventoryController);
            return true;
        }

        internal static void Lower(Player.FirearmController controller)
        {
            var weapon = controller?.Item;
            if (weapon?.Parent == null)
                return;

            controller._player.InventoryController.RaiseRemoveEvent(
                new RemoveItemEventArgs(weapon, weapon.Parent, CommandStatus.Failed,
                    controller._player.InventoryController));
        }
    }
}
