using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup.Hooks;

public class PickupDropletOnCollisionOverrideHandler : AbstractHookHandler
{
    public PickupDropletController CurrentDroplet;

    public override void RegisterHooks()
    {
        On.RoR2.PickupDropletController.OnCollisionEnter += On_PickupDropletController_OnCollisionEnter;
    }
    public override void UnregisterHooks()
    {
        On.RoR2.PickupDropletController.OnCollisionEnter -= On_PickupDropletController_OnCollisionEnter;
    }

    private void On_PickupDropletController_OnCollisionEnter(On.RoR2.PickupDropletController.orig_OnCollisionEnter orig,
        PickupDropletController self, Collision collision)
    {
        CurrentDroplet = self;
        orig(self, collision);
        CurrentDroplet = null;
    }
}