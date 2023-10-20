using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup.ItemDistributors;

internal class FixedTargetDistributor : ItemDistributor
{
    private readonly CharacterMaster target;

    public FixedTargetDistributor(AutoCommandQueuePickup plugin, PlayerCharacterMasterController target) : this(plugin,
        target.master)
    { }

    public FixedTargetDistributor(AutoCommandQueuePickup plugin, CharacterMaster target) : base(plugin)
    {
        this.target = target;
    }

    public override bool IsValid()
    {
        return IsValidTarget(target);
    }

    public override void UpdateTargets()
    {
    }

    public override CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null)
    {
        if (extraFilter != null && !extraFilter(target)) return null;
        return target;
    }
}