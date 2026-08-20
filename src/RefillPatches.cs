using HarmonyLib;
using MGSC;

namespace Quasimorph_Refill_Button
{
    [HarmonyPatch(typeof(ScreenWithShipCargo), "DragControllerShowContextMenuCallback")]
    public static class ShipShowContextMenuPatch
    {
        public static void Postfix(ScreenWithShipCargo __instance, ItemSlot __0)
        {
            BasePickupItem item = __0?.Item;
            if (CanAddRefill(__instance, item))
            {
                ContextMenuCommandHelper.AddCommand(RefillService.RefillCaptionWithHotkey, RefillService.RefillCommandValue);
            }
            if (CanAddRefillUsages(__instance, item))
            {
                ContextMenuCommandHelper.AddCommand(RefillService.RefillUsagesCaptionWithHotkey, RefillService.RefillUsagesCommandValue);
            }
        }

        private static bool CanAddRefill(ScreenWithShipCargo screen, BasePickupItem item)
        {
            return RefillService.CanShowRefill(item) && RefillService.IsValidShipRefillStorage(screen, item);
        }

        private static bool CanAddRefillUsages(ScreenWithShipCargo screen, BasePickupItem item)
        {
            return RefillService.CanShowRefillUsages(item) && RefillService.IsValidShipRefillStorage(screen, item);
        }
    }

    [HarmonyPatch(typeof(InventoryScreen), "DragControllerShowContextMenuCallback")]
    public static class RaidShowContextMenuPatch
    {
        public static void Postfix(InventoryScreen __instance, ItemSlot __0)
        {
            BasePickupItem item = __0?.Item;
            if (CanAddRefill(__instance, item))
            {
                ContextMenuCommandHelper.AddCommand(RefillService.RefillCaptionWithHotkey, RefillService.RefillCommandValue);
            }
            if (CanAddRefillUsages(__instance, item))
            {
                ContextMenuCommandHelper.AddCommand(RefillService.RefillUsagesCaptionWithHotkey, RefillService.RefillUsagesCommandValue);
            }
        }

        private static bool CanAddRefill(InventoryScreen screen, BasePickupItem item)
        {
            return CanAddForItem(screen, item, RefillService.CanShowRefill);
        }

        private static bool CanAddRefillUsages(InventoryScreen screen, BasePickupItem item)
        {
            return CanAddForItem(screen, item, RefillService.CanShowRefillUsages);
        }

        private static bool CanAddForItem(InventoryScreen screen, BasePickupItem item, System.Func<BasePickupItem, bool> canShow)
        {
            if (!canShow(item))
            {
                return false;
            }
            if (RefillService.IsShipCargoItem(item))
            {
                return true;
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
            if (bindValue == RefillService.RefillCommandValue)
            {
                RefillService.HandleShipRefill(__instance);
                return false;
            }
            if (bindValue == RefillService.RefillUsagesCommandValue)
            {
                RefillService.HandleShipRefill(__instance, usagesOnly: true);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(InventoryScreen), "ContextMenuOnCmdSelected")]
    public static class RaidContextMenuCommandPatch
    {
        public static bool Prefix(InventoryScreen __instance, int bindValue)
        {
            if (bindValue == RefillService.RefillCommandValue)
            {
                RefillService.HandleRaidRefill(__instance);
                return false;
            }
            if (bindValue == RefillService.RefillUsagesCommandValue)
            {
                RefillService.HandleRaidRefill(__instance, usagesOnly: true);
                return false;
            }
            return true;
        }
    }

    internal static class ContextMenuCommandHelper
    {
        public static void AddCommand(string caption, int commandValue)
        {
            CommonContextMenu menu = UI.Get<CommonContextMenu>();
            if (menu == null)
            {
                return;
            }
            bool wasActive = menu.gameObject.activeSelf;
            menu.SetupCommand(caption, commandValue);
            if (wasActive)
            {
                AccessTools.Method(typeof(CommonContextMenu), "InitSize").Invoke(menu, new object[] { 0 });
                AccessTools.Method(typeof(CommonContextMenu), "RecalculatePosition").Invoke(menu, null);
            }
            else
            {
                UI.Chain<CommonContextMenu>().Show().SetBackgroundOrder(-1).SetBackOnBackgroundClick(true);
            }
        }
    }
}