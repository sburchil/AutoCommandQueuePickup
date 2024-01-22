using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using RoR2;

namespace AutoCommandQueuePickup.Configuration;

public class ConfigMigrator
{
    private static readonly PropertyInfo Property_ConfigFiles_OrphanedEntries =
        typeof(ConfigFile).GetProperty("OrphanedEntries",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private readonly ConfigFile config;
    private readonly Dictionary<ConfigDefinition, string> OrphanedEntries;
    private readonly Config AutoPickupConfig;

    public ConfigMigrator(ConfigFile config, Config modConfig)
    {
        this.config = config;
        AutoPickupConfig = modConfig;
        OrphanedEntries = (Dictionary<ConfigDefinition, string>)Property_ConfigFiles_OrphanedEntries.GetValue(config);
    }

    private static void MigrateItemTier(ItemTierSet set, ItemTier tier, string value)
    {
        if (value == "true")
            set.Add(tier);
        else
            set.Remove(tier);
    }

    private static Dictionary<ConfigDefinition, Action<Config, string>> migrations = new()
    {
        {new("General", "DebugItemDefinitions"), (config, value) => { } },
        {new("General", "TimeOfDistribution"), (config, value) => config.timeOfDistribution.Value = (Distribution)Enum.Parse(typeof(Distribution), value)},
        {new("OnDrop", "ItemDistributionMode"), (config, value) => config.distributionMode.Value = (Mode)Enum.Parse(typeof(Mode), value)},
        {new("OnDrop", "DistributeWhiteItems"), (config, value) => MigrateItemTier(config.OnDrop.TierWhitelist, ItemTier.Tier1, value)},
        {new("OnDrop", "DistributeGreenItems"), (config, value) => MigrateItemTier(config.OnDrop.TierWhitelist, ItemTier.Tier2, value)},
        {new("OnDrop", "DistributeRedItems"), (config, value) => MigrateItemTier(config.OnDrop.TierWhitelist, ItemTier.Tier3, value)},
        {new("OnDrop", "DistributeLunarItems"), (config, value) => MigrateItemTier(config.OnDrop.TierWhitelist, ItemTier.Lunar, value)},
        {new("OnDrop", "DistributeBossItems"), (config, value) => MigrateItemTier(config.OnDrop.TierWhitelist, ItemTier.Boss, value)},
        {new("OnDrop", "ItemBlacklist"), (config, value) => config.OnDrop.ItemBlacklistEntry.Value = ItemSet.Deserialize(value)},

        {new("OnTeleport", "ItemDistributionMode"), (config, value) => { }},
        {new("OnTeleport", "DistributeWhiteItems"), (config, value) => MigrateItemTier(config.OnTeleport.TierWhitelist, ItemTier.Tier1, value)},
        {new("OnTeleport", "DistributeGreenItems"), (config, value) => MigrateItemTier(config.OnTeleport.TierWhitelist, ItemTier.Tier2, value)},
        {new("OnTeleport", "DistributeRedItems"), (config, value) => MigrateItemTier(config.OnTeleport.TierWhitelist, ItemTier.Tier3, value)},
        {new("OnTeleport", "DistributeLunarItems"), (config, value) => MigrateItemTier(config.OnTeleport.TierWhitelist, ItemTier.Lunar, value)},
        {new("OnTeleport", "DistributeBossItems"), (config, value) => MigrateItemTier(config.OnTeleport.TierWhitelist, ItemTier.Boss, value)},
        {new("OnTeleport", "ItemBlacklist"), (config, value) => config.OnTeleport.ItemBlacklistEntry.Value = ItemSet.Deserialize(value)},
    
        {new("CommandQueue", "BigItemButtonContainer"), (config, value) => config.bigItemButtonContainer.Value = value == "true"},
        {new("CommandQueue", "BigItemButtonScale"), (config, value) => config.bigItemButtonScale.Value = float.Parse(value)},
        {new("CommandQueue", "BightClickRemovesStack"), (config, value) => config.rightClickRemovesStack.Value = value == "true"},
    };

    public bool NeedsMigration => migrations.Keys.Any(def => OrphanedEntries.ContainsKey(def));

    public void DoMigration()
    {
        List<ConfigDefinition> migratedKeys = new();

        foreach (var entry in migrations)
        {
            var def = entry.Key;
            var migration = entry.Value;

            if (OrphanedEntries.TryGetValue(def, out var orphanedEntry))
            {
                migration(AutoPickupConfig, orphanedEntry);
                migratedKeys.Add(def); // Don't mutate dictionary while iterating
            }
        }

        foreach (var def in migratedKeys)
        {
            OrphanedEntries.Remove(def);
        }

        config.Save();
    }
}