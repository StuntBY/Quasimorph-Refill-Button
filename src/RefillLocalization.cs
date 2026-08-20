using HarmonyLib;
using MGSC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Quasimorph_Refill_Button
{
    public static class RefillLocalization
    {
        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void OnAfterConfigsLoaded(IModContext context)
        {
            string path = Path.Combine(context.ModContentPath, "localization.json");
            if (!File.Exists(path))
            {
                return;
            }

            Dictionary<string, string> translations;
            try
            {
                translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception)
            {
                return;
            }
            if (translations == null)
            {
                return;
            }

            var db = Traverse.Create(Singleton<Localization>.Instance)
                .Field("db")
                .GetValue<Dictionary<Localization.Lang, Dictionary<string, string>>>();
            if (db == null)
            {
                return;
            }
            foreach (object lang in Enum.GetValues(typeof(Localization.Lang)))
            {
                if (translations.TryGetValue(lang.ToString(), out string value) && !string.IsNullOrEmpty(value))
                {
                    db[(Localization.Lang)lang][RefillService.RefillCaptionKey] = value;
                }
            }
        }
    }
}