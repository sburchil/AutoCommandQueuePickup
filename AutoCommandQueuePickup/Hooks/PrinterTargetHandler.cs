using AutoCommandQueuePickup.ItemDistributors;
using RoR2;

namespace AutoCommandQueuePickup.Hooks;

public class PrinterTargetHandler : AbstractHookHandler
{
    public override void RegisterHooks()
    {
        On.RoR2.ShopTerminalBehavior.DropPickup += On_ShopTerminalBehavior_DropPickup;
    }

    public override void UnregisterHooks()
    {
        On.RoR2.ShopTerminalBehavior.DropPickup -= On_ShopTerminalBehavior_DropPickup;
    }

    private void On_ShopTerminalBehavior_DropPickup(On.RoR2.ShopTerminalBehavior.orig_DropPickup orig,
        ShopTerminalBehavior self)
    {
        if (ModConfig.overridePrinter.Value)
        {
            var interaction = self.GetComponent<PurchaseInteraction>();

            if (interaction && CostTypeCatalog.GetCostTypeDef(interaction.costType)?.itemTier != ItemTier.NoTier)
            {
                var interactor = interaction.lastActivator;
                var body = interactor != null ? interactor.GetComponent<CharacterBody>() : null;
                var target = body != null ? body.master : null;

                if (target)
                    hookManager.GetHandler<CreatePickupDropletHandler>().DistributorOverride =
                        new FixedTargetDistributor(Plugin, target);
            }
        }

        orig(self);
        hookManager.GetHandler<CreatePickupDropletHandler>().DistributorOverride = null;
    }
}