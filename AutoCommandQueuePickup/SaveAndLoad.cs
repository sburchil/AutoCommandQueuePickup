using System;
using System.Collections.Generic;
using System.IO;
using AutoCommandQueuePickup;
using RoR2;

namespace AutoCommandQueuePickup
{
    public static class SaveAndLoad
    {
        public static void Load(string path)
        {
            if (!File.Exists(path)) return;

            string content = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(content)) return;

            QueueManager.UpdateQueueAvailability();
            try
            {
                using (StreamReader sr = new(path))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line == "") continue;
                        string[] rawLinesSplit = line.Split(',');
                        ItemTier currTier = (ItemTier)Enum.Parse(typeof(ItemTier), rawLinesSplit[0]);
                        bool doesRepeat = Convert.ToBoolean(rawLinesSplit[1]);
                        if (doesRepeat) QueueManager.ToggleRepeat(currTier);
                        for (int i = 2; i < rawLinesSplit.Length; i++)
                        {
                            if (string.IsNullOrEmpty(rawLinesSplit[i])) continue;

                            string[] s = rawLinesSplit[i].Split('*');
                            for (int j = 0; j < int.Parse(s[1]); j++)
                            {
                                QueueManager.Enqueue(new PickupIndex(Convert.ToInt32(s[0])));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Debug(e);
            }
        }
        public static void Save(string path)
        {
            try
            {
                string textToSave = "";
                ItemTier[] tiers = (ItemTier[])Enum.GetValues(typeof(ItemTier));
                foreach (ItemTier tier in tiers)
                {
                    if (QueueManager.Peek(tier) == null) continue;
                    textToSave += tier.ToString() + "," + QueueManager.DoesRepeat(tier).ToString() + ",";
                    foreach (QueueManager.QueueEntry entry in QueueManager.mainQueues[tier])
                    {
                        textToSave += $"{entry.pickupIndex.value}*{entry.count},";
                    }
                    textToSave += "\n";
                }
                if (!File.Exists(path)) File.Create(path).Dispose();
                using (StreamWriter tw = new StreamWriter(path))
                {
                    tw.WriteLine(textToSave);
                    tw.Dispose();
                }
            }
            catch (Exception e)
            {
                Log.Debug(e);
            }
        }
    }
}