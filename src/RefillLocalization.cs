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

            Dictionary<string, Dictionary<string, string>> translations;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                translations = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                if (translations == null)
                {
                    var flat = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    translations = new Dictionary<string, Dictionary<string, string>>();
                    if (flat != null)
                    {
                        foreach (KeyValuePair<string, string> pair in flat)
                        {
                            translations[pair.Key] = new Dictionary<string, string> { { "caption", pair.Value } };
                        }
                    }
                }
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
                if (!translations.TryGetValue(lang.ToString(), out Dictionary<string, string> values) || values == null)
                {
                    continue;
                }
                Dictionary<string, string> target = db[(Localization.Lang)lang];
                SetKey(target, RefillService.RefillCaptionKey, values, "caption");
                SetKey(target, RefillService.RefillUsagesCaptionKey, values, "usagescaption");
            }
        }

        private static void SetKey(Dictionary<string, string> target, string key, Dictionary<string, string> values, string field)
        {
            if (values.TryGetValue(field, out string value) && !string.IsNullOrEmpty(value))
            {
                target[key] = value;
            }
        }
    }
}