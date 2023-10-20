using System.Collections.Generic;
using System.Linq;
using AutoCommandQueuePickup.ItemDistributors;
using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup;

public abstract class ItemDistributor
{
    public delegate bool TargetFilter(CharacterMaster target);

    protected ItemDistributor(AutoCommandQueuePickup plugin)
    {
        UpdateTargets();
    }

    public abstract CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null);

    public void DestributeCommandItem(ref GenericPickupController.CreatePickupInfo pickupInfo, TargetFilter extraFilter = null)
    {
        var target = GetTarget(pickupInfo.position, pickupInfo.pickupIndex, extraFilter);
        if (target == null) return;
        pickupInfo.position = target.transform.position;
    }

    public void DistributeItem(GenericPickupController item, TargetFilter extraFilter = null)
    {
        var target = GetTarget(item.transform.position, item.pickupIndex, extraFilter);
        if (target == null) return;
        AutoCommandQueuePickup.GrantItem(item, target);
    }

    public abstract void UpdateTargets();

    public virtual bool IsValid()
    {
        return true;
    }

    protected bool IsValidTarget(CharacterMaster master)
    {
        return AutoCommandQueuePickup.AutoPickupConfig.CheckTarget(master);
    }

    protected bool IsValidTarget(PlayerCharacterMasterController player)
    {
        return player != null && IsValidTarget(player.master);
    }

    protected List<PlayerCharacterMasterController> GetValidTargets()
    {
        return PlayerCharacterMasterController.instances.Where(IsValidTarget).ToList();
    }

    public static ItemDistributor GetItemDistributor(Mode mode, AutoCommandQueuePickup plugin)
    {
        switch (mode)
        {
            case Mode.Closest:
                return new ClosestDistributor(plugin);

            case Mode.LeastItems:
                return new LeastItemsDistributor(plugin);

            case Mode.Random:
                return new RandomDistributor(plugin);

            case Mode.Sequential:
            default:
                return new SequentialDistributor(plugin);
        }
    }
}