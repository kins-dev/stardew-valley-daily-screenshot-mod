﻿using StardewModdingAPI;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;

namespace DailyScreenshot
{
    class ModConfig
    {

        void MTrace(string message) => ModEntry.DailySS.MTrace(message);

        void MWarn(string message) => ModEntry.DailySS.MWarn(message);
        private string m_launchGuid;
        public static string DEFAULT_STRING = "Default";
        public const float DEFAULT_ZOOM = 0.25f;
        public const int DEFAULT_START_TIME = 600;
        public const int DEFAULT_END_TIME = 2600;
        public List<ModRule> SnapshotRules { get; set; } = new List<ModRule>();

        // Place to put json that doesn't match properties here
        // This can be used to upgrade the config file
        // See: https://www.newtonsoft.com/json/help/html/SerializationAttributes.htm#JsonExtensionDataAttribute
        [JsonExtensionData]
        private IDictionary<string, JToken> _additionalData = null;

        [JsonIgnore]
        internal bool RulesModified { get; set; } = false;

#if false
        public SButton TakeScreenshotKey { get; set; }

        public float TakeScreenshotKeyZoomLevel { get; set; }

        public string FolderDestinationForKeypressScreenshots { get; set; }
#endif

        public ModConfig()
        {
            m_launchGuid = Guid.NewGuid().ToString();
            SnapshotRules.Add(new ModRule());
            SnapshotRules[0].Name = m_launchGuid;
        }

        private T GetOldData<T>(IDictionary<string, JToken> oldDatDict, string key, T defaultValue)
        {
            if(oldDatDict.TryGetValue(key, out JToken value))
            {
                return value.ToObject<T>();
            }
            return default;
        }

        /// <summary>
        /// If the user has the old mod configuration format,
        /// migrate it to the new format
        /// </summary>
        /// <param name="context"></param>
        [OnDeserialized]
        private void OnDeserializedFixup(StreamingContext context)
        {
            // If there's no extra Json attributes, there's nothing to fixup
            if(_additionalData == null)
                return;
            try
            {
                // Convert the automatic snapshot rules to the new format
                if (_additionalData.TryGetValue("HowOftenToTakeScreenshot", out JToken oldSSRules))
                {
                    ModRule autoRule;
                    if(SnapshotRules.Count == 1 && SnapshotRules[0].Name == m_launchGuid)
                        autoRule = SnapshotRules[0];
                    else
                    {
                        autoRule = new ModRule();
                        SnapshotRules.Add(autoRule);
                    }
                    ModTrigger autoTrigger = autoRule.Trigger;
                    autoRule.FileName = ModRule.FileNameFlags.Default;
                    autoTrigger.Location = ModTrigger.LocationFlags.Farm;
                    autoTrigger.EndTime = DEFAULT_END_TIME;
                    if (_additionalData.TryGetValue("TakeScreenshotOnRainyDays", out JToken rainyDays))
                    {
                        if (!(bool)rainyDays)
                            autoTrigger.Weather = ModTrigger.WeatherFlags.Snowy |
                                ModTrigger.WeatherFlags.Sunny |
                                ModTrigger.WeatherFlags.Windy;
                        else
                            autoTrigger.Weather = ModTrigger.WeatherFlags.Any;
                    }
                    else
                        autoTrigger.Weather = ModTrigger.WeatherFlags.Any;
                    // Clear the default for a new value
                    autoTrigger.Days = ModTrigger.DateFlags.Day_None;
                    Dictionary<string, bool> ssDict = oldSSRules.ToObject<Dictionary<string, bool>>();
                    foreach (string key in ssDict.Keys)
                    {
                        if (ssDict[key])
                        {
                            // Replace "Last Day of Month" with "LastDayOfTheMonth"
                            string key_to_enum = key.Replace("of", "OfThe").Replace(" ", "");
                            if (Enum.TryParse<ModTrigger.DateFlags>(key_to_enum, out ModTrigger.DateFlags date))
                                autoTrigger.Days |= date;
                            else
                                MWarn($"Unknown key: \"{key}\"");
                        }
                    }

                    autoRule.Directory = GetOldData<string>(_additionalData,
                        "FolderDestinationForDailyScreenshots", DEFAULT_STRING);
                    autoRule.ZoomLevel = GetOldData < float>(_additionalData,
                        "TakeScreenshotZoomLevel",DEFAULT_ZOOM);
                    autoTrigger.StartTime = GetOldData < int>(_additionalData,
                        "TimeScreenshotGetsTakenAfter", DEFAULT_START_TIME);
                    RulesModified = true;
                    SButton button = GetOldData<SButton>(_additionalData,
                        "TakeScreenshotKey", SButton.None);
                    if(button != SButton.None)
                    {
                        ModRule keyRule = new ModRule();
                        keyRule.Trigger.Key = button;
                        keyRule.Trigger.Location = ModTrigger.LocationFlags.Any;
                        keyRule.ZoomLevel = GetOldData<float>(_additionalData,
                            "TakeScreenshotKeyZoomLevel", DEFAULT_ZOOM);
                        keyRule.Directory = GetOldData<string>(_additionalData,
                            "FolderDestinationForKeypressScreenshots", DEFAULT_STRING);
                        keyRule.FileName = ModRule.FileNameFlags.None;
                        SnapshotRules.Add(keyRule);
                    }

                }
            }
            catch (Exception ex)
            {
                MWarn($"Unable to read old config. Technical details:{ex}");
            }
            _additionalData=new Dictionary<string, JToken>();
        }

        internal void SortRules()
        {
            SnapshotRules.Sort();
        }

        public void ValidateUserInput()
        {
            foreach (ModRule rule in SnapshotRules)
            {
                if(rule.ValidateUserInput())
                    RulesModified = true;
            }
        }

        public void NameRules()
        {
            int cnt = 0;
            foreach (ModRule rule in SnapshotRules)
            {
                if (string.IsNullOrEmpty(rule.Name) || rule.Name == m_launchGuid)
                {
                    cnt++;
                    rule.Name = $"Unnamed Rule {cnt}";
                    RulesModified = true;
                }
            }
        }
    }
}