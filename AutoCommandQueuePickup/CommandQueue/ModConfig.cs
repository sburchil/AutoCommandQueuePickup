using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using RoR2;
using System.Linq;
using UnityEngine;

namespace AutoCommandQueuePickup
{
    public static class ModConfig
    {
        private static ConfigFile config;
        
        internal static void InitConfig(ConfigFile _config)
        {
            config = _config; // No config available at the moment

            TomlTypeConverter.AddConverter(typeof(ItemTierSet), new TypeConverter
            {
                ConvertToObject = (str, type) => ItemTierSet.Deserialize(str),
                ConvertToString = (obj, type) => obj.ToString()
            });

            enabledTabs = config.Bind(new ConfigDefinition("General", "EnabledQueues"), new ItemTierSet { ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3 }, new ConfigDescription($"Which item tiers should have queues?\nValid values: {string.Join(", ", Enum.GetNames(typeof(ItemTier)))}"));
            bigItemButtonContainer = config.Bind(new ConfigDefinition("General", "BigItemSelectionContainer"), true, new ConfigDescription("false: Default command button layout\ntrue: Increase the space for buttons, helps avoid overflow with modded items"));
            bigItemButtonScale = config.Bind(new ConfigDefinition("General", "BigItemSelectionScale"), 1f, new ConfigDescription("Scale applied to item buttons in the menu - decrease it if your buttons don't fit\nApplies only if BigItemSelectionContainer is true"));
            rightClickRemovesStack = config.Bind(new ConfigDefinition("General", "RightClickRemovesStack"), true, new ConfigDescription("Should right-clicking an item in the queue remove the whole stack?"));
        }

        public static ConfigEntry<ItemTierSet> enabledTabs;
        public static ConfigEntry<bool> bigItemButtonContainer;
        public static ConfigEntry<float> bigItemButtonScale;
        public static ConfigEntry<bool> rightClickRemovesStack;

        public class ItemTierSet : SortedSet<ItemTier>
        {
            public static string Serialize(ItemTierSet self)
            {
                return string.Join(", ", self.Select(x => x.ToString()));
            }
            public static ItemTierSet Deserialize(string src)
            {
                ItemTierSet self = new ItemTierSet();
                foreach(var entry in src.Split(',').Select(s => s.Trim()))
                {
                    if(Enum.TryParse(entry, out ItemTier result))
                    {
                        self.Add(result);
                    }
                    else if(int.TryParse(entry, out int index))
                    {
                        self.Add((ItemTier)index);
                    }
                }
                return self;
            }

            public override string ToString()
            {
                return Serialize(this);
            }
        }
        
        const string fileName = "commandQueueSlot_";
        const string extension = "save";

        public static void SaveQueue(int slot) {
            string path = $"{fileName}{slot}.{extension}";
            string queueString = QueueManager.mainQueues
                .Where(entry => entry.Value.Count > 0)
                .SelectMany(pair => pair.Value)
                .Aggregate("", (current, queueEntry) => current + $"{queueEntry.pickupIndex.value}*{queueEntry.count},");

            if (!File.Exists(path)) File.Create(path).Dispose();
            using(StreamWriter tw = new StreamWriter(path)) {
                tw.WriteLine(queueString);
                tw.Dispose();
            }
        }
        
        public static void LoadQueue(int slot) {
            string path = $"{fileName}{slot}.{extension}";
            if (!File.Exists(path)) return;
            
            QueueManager.ClearAllQueues();
            string content = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(content)) return;
            
            try { 
                foreach (string entry in content.Split(',')) { 
                    if (string.IsNullOrEmpty(entry)) continue;
                    string[] s = entry.Split('*');
                    for (int i = 0; i < int.Parse(s[1]); i++) { 
                        QueueManager.Enqueue(new PickupIndex(int.Parse(s[0]))); 
                    } 
                }
            } catch (Exception) {
                Debug.Log($"File for save slot {slot} contains errors.");
            }
        }
        
        public static string PreviewSlot(int slot) {
            string path = $"{fileName}{slot}.{extension}";
            if (!File.Exists(path)) return "No Save";
            
            string content = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(content)) return "Empty";
            
            int totalItems = 0;
            try {
                totalItems += content.Split(',').Where(o => !string.IsNullOrEmpty(o)).Select(o => o.Split('*')).Select(o => int.Parse(o[1])).Sum();
            } catch (Exception) {
                return "Error";
            }
            return totalItems > 0 ? $"{totalItems} item{(totalItems > 1 ? "s" : "")}" : "Empty";
        }
    }
}
