using System.Linq;
using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup.ItemDistributors;

internal class ClosestDistributor : ItemDistributor
{
    private CharacterMaster[] targets;

    public ClosestDistributor(AutoCommandQueuePickup plugin) : base(plugin) { }

    public override void UpdateTargets()
    {
        targets = GetValidTargets().Select(player => player.master).ToArray();
    }

    public override CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null)
    {
        CharacterMaster closestTarget = null;
        var closestDistance = float.MaxValue;

        var itemPosition = position;

        foreach (var target in targets)
        {
            if (extraFilter != null && !extraFilter(target)) continue;
            var targetPos = target.hasBody ? target.GetBodyObject().transform.position : target.deathFootPosition;

            var distance = (itemPosition - targetPos).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestTarget = target;
                closestDistance = distance;
            }
        }

        return closestTarget;
    }
}