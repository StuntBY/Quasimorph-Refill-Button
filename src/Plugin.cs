using HarmonyLib;
using MGSC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Quasimorph_Refill_Button
{
    public static class Plugin
    {

        public static ConfigDirectories ConfigDirectories = new ConfigDirectories();

        public static ModConfig Config { get; private set; }

        public static Logger Logger = new Logger();

        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void AfterConfig(IModContext context)
        {

            Directory.CreateDirectory(ConfigDirectories.ModPersistenceFolder);

            Config = ModConfig.LoadConfig(ConfigDirectories.ConfigPath);

            RefillLocalization.InjectLocalization(context);

            try
            {
                McmConfig.Register();
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Mod Configuration Menu is not available. Config menu registration skipped.");
                Logger.LogException(ex);
            }

            Harmony harmony = new Harmony("Stunt_" + ConfigDirectories.ModAssemblyName);
            InputHelperSuppressPatch.Patch(harmony);
            harmony.PatchAll();
        }

    }
}
