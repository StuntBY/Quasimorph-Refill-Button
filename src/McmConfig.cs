using ModConfigMenu;
using ModConfigMenu.Contracts;
using ModConfigMenu.Implementations;
using ModConfigMenu.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Quasimorph_Refill_Button
{
    internal static class McmConfig
    {
        public static void Register()
        {
            List<IConfigValue> configData = new List<IConfigValue>
            {
                new ConfigValue(
                    nameof(ModConfig.HideRefillKeybindHighlight),
                    Plugin.Config.HideRefillKeybindHighlight,
                    "Refill button settings",
                    Plugin.Config.HideRefillKeybindHighlight,
                    "Hides the keybind from the Refill command caption.",
                    "Hide keybind highlight"),
                CreateKeyDropdown(
                    nameof(ModConfig.RefillHotkeyKey),
                    Plugin.Config.RefillHotkeyKey,
                    "Refill button settings",
                    "Select key"),
                new ConfigValue(
                    nameof(ModConfig.HideRefillUsagesKeybindHighlight),
                    Plugin.Config.HideRefillUsagesKeybindHighlight,
                    "Refill usages button settings",
                    Plugin.Config.HideRefillUsagesKeybindHighlight,
                    "Hides the keybind from the Refill usages command caption.",
                    "Hide keybind highlight"),
                new ConfigValue(
                    nameof(ModConfig.HideRefillUsagesInRaid),
                    Plugin.Config.HideRefillUsagesInRaid,
                    "Refill usages button settings",
                    Plugin.Config.HideRefillUsagesInRaid,
                    "Hides the Refill usages button in raids. The hotkey still works.",
                    "Hide in raid"),
                CreateKeyDropdown(
                    nameof(ModConfig.RefillUsagesHotkeyKey),
                    Plugin.Config.RefillUsagesHotkeyKey,
                    "Refill usages button settings",
                    "Select key"),
            };

            ModConfigMenuAPI.RegisterModConfig("Refill Button", configData, OnConfigSaved);
        }

        private static DropdownConfig CreateKeyDropdown(string propertyName, KeyCode current, string header, string label)
        {
            List<object> names = new List<object> { "None" };
            names.AddRange(Enum.GetNames(typeof(KeyCode))
                .Where(x => x != "None")
                .OrderBy(x => x)
                .ToList<object>());
            string currentName = current == KeyCode.None ? "None" : current.ToString();
            return new DropdownConfig(
                propertyName,
                currentName,
                header,
                currentName,
                "The hotkey for the command. Set to None to disable.",
                label,
                names);
        }

        private static bool OnConfigSaved(Dictionary<string, object> currentConfig, out string feedbackMessage)
        {
            feedbackMessage = string.Empty;
            try
            {
                Plugin.Config.HideRefillKeybindHighlight = Convert.ToBoolean(currentConfig[nameof(ModConfig.HideRefillKeybindHighlight)]);
                Plugin.Config.RefillHotkeyKey = ParseKey(currentConfig[nameof(ModConfig.RefillHotkeyKey)], KeyCode.F);
                Plugin.Config.HideRefillUsagesKeybindHighlight = Convert.ToBoolean(currentConfig[nameof(ModConfig.HideRefillUsagesKeybindHighlight)]);
                Plugin.Config.HideRefillUsagesInRaid = Convert.ToBoolean(currentConfig[nameof(ModConfig.HideRefillUsagesInRaid)]);
                Plugin.Config.RefillUsagesHotkeyKey = ParseKey(currentConfig[nameof(ModConfig.RefillUsagesHotkeyKey)], KeyCode.U);
                Plugin.Config.Save(Plugin.ConfigDirectories.ConfigPath);
                return true;
            }
            catch (Exception ex)
            {
                feedbackMessage = ex.Message;
                return false;
            }
        }

        private static KeyCode ParseKey(object value, KeyCode fallback)
        {
            if (value == null)
            {
                return fallback;
            }
            return Enum.TryParse(value.ToString(), out KeyCode key) ? key : fallback;
        }
    }
}