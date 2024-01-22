using System.Linq;
using UnityEngine.Networking;

namespace AutoCommandQueuePickup.Hooks;

public class ItemHandler : AbstractHookHandler
{
    public override void RegisterHooks()
    {
        On.RoR2.SceneExitController.Begin += On_SceneExitController_Begin;
        On.RoR2.GenericPickupController.Start += On_GenericPickupController_Start;
    }

    public override void UnregisterHooks()
    {
        On.RoR2.SceneExitController.Begin -= On_SceneExitController_Begin;
        On.RoR2.GenericPickupController.Start -= On_GenericPickupController_Start;
    }

    private void On_GenericPickupController_Start(On.RoR2.GenericPickupController.orig_Start orig,
        RoR2.GenericPickupController self)
    {
        if (NetworkServer.active && ModConfig.timeOfDistribution.Value.Equals(Distribution.OnDrop))
            if (ModConfig.ShouldDistribute(self.pickupIndex, Cause.Drop))
                Plugin.DistributeItem(self, Cause.Drop);

        orig(self);
    }

    private void On_SceneExitController_Begin(On.RoR2.SceneExitController.orig_Begin orig,
        RoR2.SceneExitController self)
    {
        if (NetworkServer.active && ModConfig.timeOfDistribution.Value.Equals(Distribution.OnTeleport))
        {
            var originalPickups = RoR2.InstanceTracker.GetInstancesList<RoR2.GenericPickupController>();

            var pickups = new RoR2.GenericPickupController[originalPickups.Count];

            originalPickups.CopyTo(pickups);

            Plugin.DistributeItems(
                pickups.Where(pickup => ModConfig.ShouldDistribute(pickup.pickupIndex, Cause.Teleport)),
                Cause.Teleport);
        }

        orig(self);
    }
}