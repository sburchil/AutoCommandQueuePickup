using System.Linq;
using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup.ItemDistributors;

internal class RandomDistributor : ItemDistributor
{
    private CharacterMaster[] targets;

    public RandomDistributor(AutoCommandQueuePickup plugin) : base(plugin) { }

    public override void UpdateTargets()
    {
        targets = GetValidTargets().Select(pcmc => pcmc.master).ToArray();
    }

    public override CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null)
    {
        var filteredTargets = targets.AsEnumerable();
        if (extraFilter != null)
            filteredTargets = filteredTargets.Where(target => extraFilter(target));
        var characterMasters = filteredTargets.ToArray();
        return characterMasters.ElementAtOrDefault(Random.Range(0, characterMasters.Length));
    }
}