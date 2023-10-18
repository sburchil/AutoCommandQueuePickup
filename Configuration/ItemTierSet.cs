using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;

namespace AutoCommandQueuePickup.Configuration;

public class ItemTierSet : SortedSet<ItemTier>
{
    public static string Serialize(ItemTierSet self)
    {
        return string.Join(", ", self.Select(x => ItemTierCatalog.GetItemTierDef(x).name));
    }

    public static ItemTierSet Deserialize(string src)
    {
        var self = new ItemTierSet();
        foreach (var entry in src.Split(',').Select(s => s.Trim()))
            if (Enum.TryParse(entry, out ItemTier result))
                self.Add(result);
            else if (ItemTierCatalog.FindTierDef(entry) is var tierDef && tierDef != null)
                self.Add(tierDef.tier);
            else if (int.TryParse(entry, out var index)) self.Add((ItemTier)index);
        return self;
    }

    public override string ToString()
    {
        return Serialize(this);
    }
}