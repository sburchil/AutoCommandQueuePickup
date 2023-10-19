using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;

namespace AutoCommandQueuePickup.Hooks;

/// <summary>
///     Handler for cases where PickupDropletController.CreatePickupDroplet is used.
///     Patches RoR2.PickupDropletController.CreatePickupDroplet.
///     Uses a global field to override the target.
/// </summary>
public class CreatePickupDropletHandler : AbstractHookHandler
{
    public ItemDistributor DistributorOverride;

    public override void RegisterHooks()
    {
        IL.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 +=
            IL_PickupDropletController_CreatePickupDroplet;
    }

    public override void UnregisterHooks()
    {
        IL.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 -=
            IL_PickupDropletController_CreatePickupDroplet;
    }

    private void IL_PickupDropletController_CreatePickupDroplet(ILContext il)
    {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.After, i => i.MatchCall<Object>("Instantiate"));
        cursor.Emit(OpCodes.Dup);
        cursor.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Action<GameObject>>(obj =>
        {
            if (DistributorOverride != null)
            {
                var behaviour = obj.AddComponent<OverrideDistributorBehaviour>();
                behaviour.Distributor = DistributorOverride;
            }
        });
    }
}