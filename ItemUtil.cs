using System.Collections.Generic;
using RoR2;

namespace AutoCommandQueuePickup
{
    public class ItemUtil
    {

        private ItemUtil() { }

        public static List<PickupIndex> GetItemsFromIndex(PickupIndex index)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(index);
            if (pickupDef == null) return null;
            ItemTier tier = pickupDef.itemTier;
            switch (tier)
            {
                case ItemTier.Tier1:
                    return Storage.Tier1;
                case ItemTier.Tier2:
                    return Storage.Tier2;
                case ItemTier.Tier3:
                    return Storage.Tier3;
                case ItemTier.Boss:
                    return Storage.Boss;
                case ItemTier.Lunar:
                    return Storage.Lunar;
                case ItemTier.NoTier:
                    // Lunar equipment is apparently not cool enough to be a tier
                    if (RoR2.Run.instance.availableLunarEquipmentDropList.Contains(index))
                    {
                        return Storage.LunarEquipment;
                    }
                    else
                    {
                        return Storage.Equipment;
                    }
                case ItemTier.VoidTier1:
                    return Storage.Void1;
                case ItemTier.VoidTier2:
                    return Storage.Void2;
                case ItemTier.VoidTier3:
                    return Storage.Void3;
                default:
                    return null;
            }
        }

        public static bool IsItemScrap(PickupIndex index)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(index);
            ItemIndex itemIndex = pickupDef != null ? pickupDef.itemIndex : ItemIndex.None;

            if (itemIndex.Equals(RoR2Content.Items.ScrapWhite.itemIndex))
            {
                return true;
            }
            if (itemIndex.Equals(RoR2Content.Items.ScrapGreen.itemIndex))
            {
                return true;
            }
            if (itemIndex.Equals(RoR2Content.Items.ScrapRed.itemIndex))
            {
                return true;
            }
            if (itemIndex.Equals(RoR2Content.Items.ScrapYellow.itemIndex))
            {
                return true;
            }
            return false;
        }
    }


}