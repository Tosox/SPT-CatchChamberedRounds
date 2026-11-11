using EFT;
using EFT.InventoryLogic;

namespace Tosox.CatchChamberedRounds.Helpers
{
    // Marks the weapon as being modified so the game refuses a swap mid-catch, as it does mid-reload.
    // Always pair Raise with Lower, or the weapon can never be drawn again
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
