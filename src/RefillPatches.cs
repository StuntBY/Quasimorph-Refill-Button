using HarmonyLib;
using MGSC;

namespace Quasimorph_Refill_Button
{
    [HarmonyPatch(typeof(ScreenWithShipCargo), "DragControllerShowContextMenuCallback")]
    public static class ShipShowContextMenuPatch
    {
        public static void Postfix(ScreenWithShipCargo __instance, ItemSlot __0)
        {
            if (CanAddRefill(__instance, __0?.Item))
            {
                ContextMenuCommandHelper.AddRefillCommand();
            }
        }

        private static bool CanAddRefill(ScreenWithShipCargo screen, BasePickupItem item)
        {
            if (!RefillService.CanShowRefill(item))
            {
                return false;
            }
            Mercenary merc = Traverse.Create(screen).Field("_merc").GetValue<Mercenary>();
            return merc?.CreatureData?.Inventory?.AllContainers.Contains(item.Storage) == true;
        }
    }

    [HarmonyPatch(typeof(InventoryScreen), "DragControllerShowContextMenuCallback")]
    public static class RaidShowContextMenuPatch
    {
        public static void Postfix(InventoryScreen __instance, ItemSlot __0)
        {
            if (CanAddRefill(__instance, __0?.Item))
            {
                ContextMenuCommandHelper.AddRefillCommand();
            }
        }

        private static bool CanAddRefill(InventoryScreen screen, BasePickupItem item)
        {
            if (!RefillService.CanShowRefill(item))
            {
                return false;
            }
            Creatures creatures = Traverse.Create(screen).Field("_creatures").GetValue<Creatures>();
            return creatures?.Player?.CreatureData?.Inventory?.AllContainers.Contains(item.Storage) == true;
        }
    }

    [HarmonyPatch(typeof(ScreenWithShipCargo), "ContextMenuOnCmdSelected")]
    public static class ShipContextMenuCommandPatch
    {
        public static bool Prefix(ScreenWithShipCargo __instance, int bindValue)
        {
            if (bindValue != RefillService.RefillCommandValue)
            {
                return true;
            }
            RefillService.HandleShipRefill(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryScreen), "ContextMenuOnCmdSelected")]
    public static class RaidContextMenuCommandPatch
    {
        public static bool Prefix(InventoryScreen __instance, int bindValue)
        {
            if (bindValue != RefillService.RefillCommandValue)
            {
                return true;
            }
            RefillService.HandleRaidRefill(__instance);
            return false;
        }
    }

    internal static class ContextMenuCommandHelper
    {
        public static void AddRefillCommand()
        {
            CommonContextMenu menu = UI.Get<CommonContextMenu>();
            if (menu == null || !menu.gameObject.activeSelf)
            {
                return;
            }
            menu.SetupCommand(RefillService.RefillCaption, RefillService.RefillCommandValue);
            AccessTools.Method(typeof(CommonContextMenu), "InitSize").Invoke(menu, new object[] { 0 });
            AccessTools.Method(typeof(CommonContextMenu), "RecalculatePosition").Invoke(menu, null);
        }
    }
}