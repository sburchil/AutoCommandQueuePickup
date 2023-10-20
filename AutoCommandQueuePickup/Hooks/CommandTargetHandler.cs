using System;
using System.Reflection;
using AutoCommandQueuePickup.ItemDistributors;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

namespace AutoCommandQueuePickup.Hooks;

public class CommandTargetHandler : AbstractHookHandler
{
    private static readonly FieldInfo Field_PickupPickerController_networkUIPromptController =
        typeof(PickupPickerController).GetField("networkUIPromptController",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    public override void RegisterHooks()
    {
        IL.RoR2.PickupPickerController.CreatePickup_PickupIndex += IL_PickupPickerController_CreatePickup;
    }

    public override void UnregisterHooks()
    {
        IL.RoR2.PickupPickerController.CreatePickup_PickupIndex -= IL_PickupPickerController_CreatePickup;
    }

    private void IL_PickupPickerController_CreatePickup(ILContext il)
    {
        var cursor = new ILCursor(il);
        cursor.GotoNext(MoveType.After, i => i.MatchCallOrCallvirt<GenericPickupController>("CreatePickup"));
        cursor.Emit(OpCodes.Dup);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Action<GenericPickupController, PickupPickerController>>((pickup, self) =>
        {
            var pickupDef = PickupCatalog.GetPickupDef(pickup.pickupIndex);
            if (pickupDef == null) return;
            var itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
            if (itemDef == null) return;
            var networkUIPromptController =
                (NetworkUIPromptController)Field_PickupPickerController_networkUIPromptController.GetValue(self);
            var participantMaster = networkUIPromptController.currentParticipantMaster;
            ItemDistributor overrideDistributor = new FixedTargetDistributor(Plugin, participantMaster);
            var behaviour = pickup.gameObject.AddComponent<OverrideDistributorBehaviour>();
            behaviour.Distributor = overrideDistributor;
        });
    }
}