using HarmonyLib;
using MGSC;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Quasimorph_Refill_Button
{
    public static class RefillHotkeyHandler
    {
        private static readonly MethodInfo OnContextCommandClickMethod =
            AccessTools.Method(typeof(CommonContextMenu), "OnContextCommandClick");

        [Hook(ModHookType.DungeonUpdateAfterGameLoop)]
        public static void DungeonUpdateAfterGameLoop(IModContext context)
        {
            TryTriggerRefill();
        }

        [Hook(ModHookType.SpaceUpdateBeforeGameLoop)]
        public static void SpaceUpdateBeforeGameLoop(IModContext context)
        {
            TryTriggerRefill();
        }

        private static void TryTriggerRefill()
        {
            TryTriggerCommand(RefillService.RefillHotkeyKey, RefillService.RefillCommandValue);
            TryTriggerCommand(RefillService.RefillUsagesHotkeyKey, RefillService.RefillUsagesCommandValue);
        }

        private static void TryTriggerCommand(KeyCode key, int commandValue)
        {
            if (key == KeyCode.None || !Input.GetKeyUp(key))
            {
                return;
            }
            CommonContextMenu menu = UI.GetActiveViews().FirstOrDefault(x => x is CommonContextMenu) as CommonContextMenu;
            if (menu == null || !menu.isActiveAndEnabled)
            {
                return;
            }
            Dictionary<CommonButton, int> binds = Traverse.Create(menu).Field("_commandBinds").GetValue<Dictionary<CommonButton, int>>();
            if (binds == null)
            {
                return;
            }
            CommonButton refillButton = binds.FirstOrDefault(x => x.Value == commandValue).Key;
            if (refillButton == null)
            {
                if (commandValue == RefillService.RefillUsagesCommandValue && RefillService.RefillUsagesHiddenInRaid
                    && UI.IsShowing<InventoryScreen>())
                {
                    InventoryScreen screen = UI.Get<InventoryScreen>();
                    UI.Hide<CommonContextMenu>();
                    RefillService.HandleRaidRefill(screen, usagesOnly: true);
                }
                return;
            }
            OnContextCommandClickMethod.Invoke(menu, new object[] { refillButton, 1 });
        }
    }

    public static class InputHelperSuppressPatch
    {
        public static void Patch(Harmony harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(InputHelper), nameof(InputHelper.GetKey)),
                new HarmonyMethod(AccessTools.Method(typeof(InputHelperSuppressPatch), nameof(GetKeyPrefix))));
            harmony.Patch(
                AccessTools.Method(typeof(InputHelper), nameof(InputHelper.GetKeyDown), new[] { typeof(KeyCode) }),
                new HarmonyMethod(AccessTools.Method(typeof(InputHelperSuppressPatch), nameof(GetKeyPrefix))));
        }

        private static bool GetKeyPrefix(KeyCode keyCode)
        {
            if (keyCode == KeyCode.Menu || keyCode == KeyCode.Escape)
            {
                return true;
            }
            return !UI.GetActiveViews().Any(x => x is CommonContextMenu);
        }
    }
}