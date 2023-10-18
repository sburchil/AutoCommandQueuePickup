using System;
using System.Collections.Generic;
using System.Linq;
using AutoCommandQueuePickup.CommandQueue;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2;

namespace AutoCommandQueuePickup.Configuration;

public class Config
{
    public readonly AutoCommandQueuePickup Plugin;
    private readonly ManualLogSource logger;

    //start auto item pickup config
    public ConfigEntry<Mode> distributionMode;

    public ConfigEntry<bool> printerOverrideTarget;
    public ConfigEntry<bool> scrapperOverrideTarget;
    public ConfigEntry<bool> teleportCommandObeyTier;
    public ConfigEntry<bool> distributeToDeadPlayers;

    public ConfigEntry<bool> distributeOnDrop;
    public ConfigEntry<bool> distributeOnTeleport;

    public ItemFilter OnDrop;
    public ItemFilter OnTeleport;

    public ConfigEntry<bool> teleportCommandOnDrop;
    public ConfigEntry<bool> teleportCommandOnTeleport;

    private bool OnDropReady => OnDrop?.Ready ?? false;
    private bool OnTeleportReady => OnTeleport?.Ready ?? false;
    public bool Ready => OnDropReady && OnTeleportReady;
    public event Action OnConfigReady;
    public ConfigMigrator migrator;

    //start command queue config 

    public ConfigEntry<ItemTierSet> enabledTabs;
    public ConfigEntry<bool> bigItemButtonContainer;
    public ConfigEntry<float> bigItemButtonScale;
    public ConfigEntry<bool> rightClickRemovesStack;

    public Config(AutoCommandQueuePickup plugin, ManualLogSource _logger)
    {
        Plugin = plugin;
        logger = _logger;

        migrator = new(config, this);

        OnTeleport = new ItemFilter("OnTeleportFilter", config);
        OnDrop = new ItemFilter("OnDropFilter", config);

        distributeToDeadPlayers = config.Bind("General", "DistributeToDeadPlayers", true,
            "Should items be distributed to dead players?");
        printerOverrideTarget = config.Bind("General", "OverridePrinterTarget", true,
            "Should items from printers and cauldrons be distributed only to activator as long as they're a valid target?");
        scrapperOverrideTarget = config.Bind("General", "OverrideScrapperTarget", true,
            "Should scrap from scrappers be distributed only to activator as long as they're a valid target?");

        distributionMode = config.Bind("General", "DistributionMode", Mode.Sequential,
            @"Decide how to distribute items among the players
Sequential - Goes over all players, giving each of them one item
Random - Chooses which player receives the item randomly
Closest - Gives the item to the nearest player
LeastItems - Gives the item to the player with least total items of the item's tier");

        distributeOnDrop = config.Bind("Items", "DistributeOnDrop", false,
            "Should items be distributed when they drop?");
        distributeOnTeleport = config.Bind("Items", "DistributeOnTeleport", true,
            "Should items be distributed when the teleporter is activated?");

        teleportCommandOnDrop = config.Bind("Command", "DistributeOnDrop", true,
            @"Should Command essences be teleported to players?
If enabled, when an essence is spawned, it will teleport to a player using distribution mode rules. It will not be opened automatically.
Note: Doesn't work well with LeastItems, due to LeastItems only accounting for the current number of items and not including any unopened command essences.");
        teleportCommandOnTeleport = config.Bind("Command", "DistributeOnTeleport", true,
            @"Should Command essences be teleported to the teleporter when charged?
If enabled, when the teleporter is charged, all essences are teleported nearby the teleporter.
Afterwards, any new essences that do not fit the requirements for OnDrop distribution will also be teleported nearby the teleporter.");
        teleportCommandObeyTier = config.Bind("Command", "UseTierWhitelist", true,
            @"Should Command essence teleportation be restricted by item tiers?
If enabled, when deciding if a command essence should be teleported, its tier will be compared against the OnDrop/OnTeleport tier whitelist.");

        config.SettingChanged += ConfigOnSettingChanged;

        OnTeleport.OnReady += CheckReadyStatus;
        OnDrop.OnReady += CheckReadyStatus;
        OnConfigReady += DoMigrationIfReady;

        TomlTypeConverter.AddConverter(typeof(ItemTierSet), new TypeConverter
        {
            ConvertToObject = (str, type) => ItemTierSet.Deserialize(str),
            ConvertToString = (obj, type) => obj.ToString()
        });

        enabledTabs = config.Bind(new ConfigDefinition("CommandQueue", "EnabledQueues"), new ItemTierSet { ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3 }, new ConfigDescription($"Which item tiers should have queues?\nValid values: {string.Join(", ", Enum.GetNames(typeof(ItemTier)))}"));
        bigItemButtonContainer = config.Bind(new ConfigDefinition("CommandQueue", "BigItemSelectionContainer"), true, new ConfigDescription("false: Default command button layout\ntrue: Increase the space for buttons, helps avoid overflow with modded items"));
        bigItemButtonScale = config.Bind(new ConfigDefinition("CommandQueue", "BigItemSelectionScale"), 1f, new ConfigDescription("Scale applied to item buttons in the menu - decrease it if your buttons don't fit\nApplies only if BigItemSelectionContainer is true"));
        rightClickRemovesStack = config.Bind(new ConfigDefinition("CommandQueue", "RightClickRemovesStack"), true, new ConfigDescription("Should right-clicking an item in the queue remove the whole stack?"));

        DoMigrationIfReady();
    }

    private void CheckReadyStatus()
    {
        if (Ready)
            OnConfigReady?.Invoke();
    }

    private void DoMigrationIfReady()
    {
        if (Ready && migrator.NeedsMigration)
        {
            migrator.DoMigration();
        }
    }

    private ConfigFile config => Plugin.Config;

    public bool CheckTarget(CharacterMaster master)
    {
        return master != null && (master.hasBody || distributeToDeadPlayers.Value);
    }

    public bool ShouldDistribute(PickupIndex index, Cause cause)
    {
        var distributeWrapper = cause == Cause.Drop ? distributeOnDrop : distributeOnTeleport;

        if (!distributeWrapper.Value)
            return false;

        var pickupDef = PickupCatalog.GetPickupDef(index);

        if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None)
            return false;

        var itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);

        if (itemDef == null)
            return false;

        var filter = cause == Cause.Drop ? OnDrop : OnTeleport;

        return filter.CheckFilter(itemDef.itemIndex);
    }

    public bool ShouldDistributeCommand(ItemTier tier, Cause cause)
    {
        var teleportWrapper = cause == Cause.Drop ? teleportCommandOnDrop : teleportCommandOnTeleport;

        if (!teleportWrapper.Value)
            return false;

        if (!teleportCommandObeyTier.Value)
            return true;

        var filter = cause == Cause.Drop ? OnDrop : OnTeleport;

        return filter.CheckFilterTier(tier);
    }

    //Exact order:
    //Check config if teleportation is enabled at all. If not, don't distribute.
    //Check if PickupIndex refers to a valid pickup, and if that pickup has an ItemIndex. If not, don't distribute.
    //Check if command should be filtered by tier at all. If not, we don't care about the actual item, distribute.
    //Check if ItemIndex has an actual corresponding ItemDef. If not, don't distribute.
    //If we get to this point, rely on the correct ItemFilter to decide if we should distribute.
    public bool ShouldDistributeCommand(PickupIndex index, Cause cause)
    {
        var teleportWrapper = cause == Cause.Drop ? teleportCommandOnDrop : teleportCommandOnTeleport;

        if (!teleportWrapper.Value)
            return false;

        var pickupDef = PickupCatalog.GetPickupDef(index);

        if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None)
            return false;

        if (!teleportCommandObeyTier.Value)
            return true;

        var itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);

        if (itemDef == null)
            return false;

        var filter = cause == Cause.Drop ? OnDrop : OnTeleport;

        return filter.CheckFilterTier(itemDef.tier);
    }

    private void ConfigOnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        if (e.ChangedSetting.SettingType == typeof(ItemSet))
        {
            var entry = (ConfigEntry<ItemSet>)e.ChangedSetting;
            var itemSet = entry.Value;

            if (itemSet.ParseErrors?.Count > 0)
            {
                var error =
                    $"Errors found when parsing {entry.Definition.Key} for {entry.Definition.Section}:\n\t{string.Join("\n\t", itemSet.ParseErrors)}";
                logger.LogWarning(error);
            }
        }
    }
    public class ItemTierSet : SortedSet<ItemTier>
    {
        public static string Serialize(ItemTierSet self)
        {
            return string.Join(", ", self.Select(x => x.ToString()));
        }
        public static ItemTierSet Deserialize(string src)
        {
            ItemTierSet self = new ItemTierSet();
            foreach (var entry in src.Split(',').Select(s => s.Trim()))
            {
                if (Enum.TryParse(entry, out ItemTier result))
                {
                    self.Add(result);
                }
                else if (int.TryParse(entry, out int index))
                {
                    self.Add((ItemTier)index);
                }
            }
            return self;
        }

        public override string ToString()
        {
            return Serialize(this);
        }
    }

}