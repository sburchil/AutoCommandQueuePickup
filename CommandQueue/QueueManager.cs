using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RoR2.Console;

namespace AutoCommandQueuePickup
{
    public static class QueueManager
    {
        public struct QueueEntry
        {
            public PickupIndex pickupIndex;
            public int count;
        }

        public enum QueueChange
        {
            Removed, Added, Changed, Moved
        }

        public static event Action<Run> OnRunQueueInit;
        public static event Action<QueueChange, ItemTier, int> OnQueueChanged;

        public static Dictionary<ItemTier, List<QueueEntry>> mainQueues = new Dictionary<ItemTier, List<QueueEntry>>
        {
            { ItemTier.Tier1, new List<QueueEntry>() },
            { ItemTier.Tier2, new List<QueueEntry>() },
            { ItemTier.Tier3, new List<QueueEntry>() }
        };

        public static Dictionary<ItemTier, bool> queueRepeat = new Dictionary<ItemTier, bool>();

        internal static void Enable()
        {
            Run.onRunStartGlobal += InitQueues;
            UpdateQueueAvailability();
        }

        internal static void Disable()
        {
            Run.onRunStartGlobal -= InitQueues;
        }

        public static void UpdateQueueAvailability()
        {
            var enabledTabs = AutoCommandQueuePickup.ModConfig.enabledTabs.Value;
            foreach (var key in mainQueues.Keys.ToArray())
            {
                if (!enabledTabs.Contains(key))
                    mainQueues.Remove(key);
            }
            foreach (ItemTier tier in enabledTabs)
            {
                if (!mainQueues.ContainsKey(tier))
                    mainQueues.Add(tier, new List<QueueEntry>());
            }
        }

        private static void InitQueues(Run run)
        {
            mainQueues.Clear();
            UpdateQueueAvailability();

            OnRunQueueInit?.Invoke(run);
        }

        public static bool DoesRepeat(ItemTier tier)
        {
            return queueRepeat.TryGetValue(tier, out var value) && value;
        }

        public static void ToggleRepeat(ItemTier tier)
        {
            queueRepeat[tier] = !DoesRepeat(tier);
        }

        public static void Enqueue(PickupIndex pickupIndex)
        {
            ItemTier tier = ItemCatalog.GetItemDef(PickupCatalog.GetPickupDef(pickupIndex).itemIndex).tier;

            List<QueueEntry> queue = mainQueues[tier];

            if (queue.Count > 0)
            {
                QueueEntry lastElement = queue[queue.Count - 1];
                if (lastElement.pickupIndex == pickupIndex)
                {
                    lastElement.count++;
                    queue[queue.Count - 1] = lastElement;
                    OnQueueChanged?.Invoke(QueueChange.Changed, tier, queue.Count - 1);
                    return;
                }
            }

            QueueEntry newElement = new QueueEntry
            {
                pickupIndex = pickupIndex,
                count = 1
            };

            queue.Add(newElement);
            OnQueueChanged?.Invoke(QueueChange.Added, tier, queue.Count - 1);
        }

        public static PickupIndex Peek(ItemTier tier)
        {
            var queue = mainQueues[tier];
            if (queue.Count == 0)
                return PickupIndex.none;
            return queue[0].pickupIndex;
        }

        public static PickupIndex Pop(ItemTier tier)
        {
            var index = Remove(tier, 0);
            if (DoesRepeat(tier))
                Enqueue(index);
            return index;
        }

        public static PickupIndex Remove(ItemTier tier, int index, int count = 1)
        {
            var queue = mainQueues[tier];
            var entry = queue[index];
            entry.count -= count;
            if (entry.count <= 0)
            {
                queue.RemoveAt(index);
                OnQueueChanged?.Invoke(QueueChange.Removed, tier, index);

                if (TryMerge(tier, index - 1, index))
                {
                    OnQueueChanged?.Invoke(QueueChange.Removed, tier, index);
                    OnQueueChanged?.Invoke(QueueChange.Changed, tier, index - 1);
                }
            }
            else
            {
                queue[index] = entry;
                OnQueueChanged?.Invoke(QueueChange.Changed, tier, index);
            }
            return entry.pickupIndex;
        }

        private static bool TryMerge(ItemTier tier, int into, int from)
        {
            var queue = mainQueues[tier];

            if (into < 0 || into >= queue.Count || from < 0 || from >= queue.Count) return false;

            var entry1 = queue[into];
            var entry2 = queue[from];

            if (entry1.pickupIndex != entry2.pickupIndex) return false;

            entry1.count += entry2.count;

            queue[into] = entry1;
            queue.RemoveAt(from);
            return true;
        }

        public static void Move(ItemTier tier, int oldIndex, int newIndex, int count = 1)
        {
            var queue = mainQueues[tier];

            newIndex = Math.Max(0, Math.Min(queue.Count, newIndex));

            if (newIndex == oldIndex)
                return;

            var entry = queue[oldIndex];

            if (entry.count < count) return;

            if (entry.count == count)
            {
                queue.RemoveAt(oldIndex);

                if (newIndex > oldIndex)
                    newIndex--;
            }
            else
            {
                var _entry = queue[oldIndex];
                _entry.count -= count;
                queue[oldIndex] = _entry;
            }

            entry.count = count;

            if (TryMerge(tier, oldIndex - 1, oldIndex) && newIndex > oldIndex) newIndex--;

            queue.Insert(newIndex, entry);
            TryMerge(tier, newIndex - 1, newIndex);
            TryMerge(tier, newIndex, newIndex + 1);


            // Do a full update instead of up to 6 smaller updates.
            OnQueueChanged?.Invoke(QueueChange.Moved, tier, -1);
        }

        public static IEnumerable<(ItemTier, PickupIndex)> PeekAll()
        {
            return mainQueues.Where(entry => entry.Value.Count > 0)
                .Select(entry => (entry.Key, entry.Value.First().pickupIndex));
        }

        public static bool PeekForItemTier(ItemTier tier)
        {
            if (mainQueues.ContainsKey(tier)) return mainQueues[tier].Count > 0;
            return false;
        }
    }
}
