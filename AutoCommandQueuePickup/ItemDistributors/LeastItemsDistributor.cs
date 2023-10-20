using System.Linq;
using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup.ItemDistributors;

internal class LeastItemsDistributor : ItemDistributor
{
    private CharacterMaster[] targets;

    public LeastItemsDistributor(AutoCommandQueuePickup plugin) : base(plugin) { }

    public override void UpdateTargets()
    {
        var filteredPlayers = GetValidTargets();
        targets = new CharacterMaster[filteredPlayers.Count];

        for (var i = 0; i < filteredPlayers.Count; i++)
        {
            var player = filteredPlayers[i];

            targets[i] = player.master;
        }
    }

    public override CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null)
    {
        CharacterMaster leastItems = null;
        var leastItemCount = int.MaxValue;

        var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);

        if (pickupDef == null)
            return targets.First();

        var tier = ItemCatalog.GetItemDef(pickupDef.itemIndex).tier;

        foreach (var player in targets)
        {
            if (extraFilter != null && !extraFilter(player)) continue;
            var count = player.inventory.GetTotalItemCountOfTier(tier);
            if (count < leastItemCount)
            {
                leastItems = player;
                leastItemCount = count;
            }
        }

        return leastItems;
    }
}