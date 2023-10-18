using System;
using System.Linq;
using BepInEx.Configuration;
using RoR2;

namespace AutoCommandQueuePickup.Configuration;

public class ItemFilter
{
    private readonly ConfigFile config;
    public ConfigEntry<ItemSet> ItemBlacklistEntry;
    public ConfigEntry<ItemSet> ItemWhitelistEntry;
    public ConfigEntry<ItemTierSet> TierWhitelistEntry;

    public ItemTierSet TierWhitelist => TierWhitelistEntry.Value;
    public ItemSet ItemWhitelist => ItemWhitelistEntry.Value;
    public ItemSet ItemBlacklist => ItemBlacklistEntry.Value;

    public string Type;

    public bool Ready;
    public event Action OnReady;

    static ItemFilter()
    {
        TomlTypeConverter.AddConverter(typeof(ItemTierSet), new TypeConverter
        {
            ConvertToObject = (str, type) => ItemTierSet.Deserialize(str),
            ConvertToString = (obj, type) => obj.ToString()
        });

        TomlTypeConverter.AddConverter(typeof(ItemSet), new TypeConverter
        {
            ConvertToObject = (str, type) => ItemSet.Deserialize(str),
            ConvertToString = (obj, type) => obj.ToString()
        });
    }

    public ItemFilter(string type, ConfigFile config)
    {
        Type = type;
        this.config = config;

        if (ItemSet.Initialized)
            CreateSets();
        else
            ItemSet.OnInitialized += CreateSets;
    }

    public bool CheckFilter(ItemIndex index)
    {
        var definition = ItemCatalog.GetItemDef(index);
        var tier = definition.tier;

        return !ItemBlacklist.HasItem(index) && (TierWhitelist.Contains(tier) || ItemWhitelist.HasItem(index));
    }

    // Used for command
    public bool CheckFilterTier(ItemTier tier)
    {
        return TierWhitelist.Contains(tier);
    }

    private void CreateSets()
    {
        TierWhitelistEntry = config.Bind(new ConfigDefinition(Type, "TierWhitelist"),
            new ItemTierSet { ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3 },
            new ConfigDescription(
                $"Which item tiers should be distributed?\nValid values: {string.Join(", ", ItemTierCatalog.allItemTierDefs.Select(def => def.name))}"));
        ItemWhitelistEntry = config.Bind(new ConfigDefinition(Type, "ItemWhitelist"), new ItemSet(),
            new ConfigDescription(
                "Which items should be distributed, regardless of tier?\nAccepts localized english item names and internal names, use backslash for escapes"));
        ItemBlacklistEntry = config.Bind(new ConfigDefinition(Type, "ItemBlacklist"), new ItemSet(),
            new ConfigDescription(
                "Which items should NOT be distributed, regardless of tier?\nAccepts localized english item names and internal names, use backslash for escapes"));

        Ready = true;
        OnReady?.Invoke();
    }
}