using EFT;
using EFT.InventoryLogic;

namespace Tosox.CatchChamberedRounds.Helpers
{
    /// <summary>
    /// A reload marks its weapon as being modified for as long as it runs, which is what makes the
    /// game refuse a weapon swap part way through one. The catch in front of it needs the same mark,
    /// or the swap is accepted, the hands change opens, and the host then refuses to equip anything
    /// at all until an operation that never finishes does. Raised and lowered in pairs, because one
    /// left raised makes the weapon impossible to draw for the rest of the raid.
    /// </summary>
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
