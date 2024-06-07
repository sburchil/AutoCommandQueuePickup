using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoCommandQueuePickup;
using BepInEx.Configuration;
using BepInEx.Logging;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup.Configuration;

public class Config
{
    public readonly AutoCommandQueuePickup Plugin;
    private readonly ManualLogSource logger;

    public ConfigFile config => Plugin.Config;
    public ConfigEntry<Mode> distributionMode;
    public ConfigEntry<Distribution> timeOfDistribution;

    public ItemFilter OnDrop;
    public ItemFilter OnTeleport;

    private bool OnDropReady => OnDrop?.Ready ?? false;
    private bool OnTeleportReady => OnTeleport?.Ready ?? false;
    public bool Ready => OnDropReady && OnTeleportReady;
    public event Action OnConfigReady;

    public ConfigEntry<bool> overrideScrapper;
    public ConfigEntry<bool> overridePrinter;

    //command queue config
    public ConfigEntry<string> enabledTabs;
    public List<ConfigEntry<bool>> enabledTabsConfig = new();
    public ConfigEntry<bool> bigItemButtonContainer;
    public ConfigEntry<float> bigItemButtonScale;
    public ConfigEntry<bool> rightClickRemovesStack;

    public ConfigMigrator migrator;

    public Texture2D ModIcon;

    public Config(AutoCommandQueuePickup plugin, ManualLogSource _logger)
    {
        Plugin = plugin;
        logger = _logger;

        migrator = new(config, this);

        OnTeleport = new ItemFilter("OnTeleportFilter", config);
        OnDrop = new ItemFilter("OnDropFilter", config);

        distributionMode = config.Bind("AutoItemPickup", "DistributionMode", Mode.Sequential,
            @"Decide how to distribute items among the players
            Sequential - Goes over all players, giving each of them one item
            Random - Chooses which player receives the item randomly
            Closest - Gives the item to the nearest player
            LeastItems - Gives the item to the player with least total items of the item's tier");

        timeOfDistribution = config.Bind("AutoItemPickup", "TimeOfDistribution", Distribution.OnDrop,
            @"When should items be distributed?
            OnTeleport - When the teleporter is charged
            OnDrop - Immediately when the item drops");

        overrideScrapper = config.Bind("AutoItemPickup", "OverrideScrapper", true, "Should items dropped by scrappers be distributed?");
        overridePrinter = config.Bind("AutoItemPickup", "OverridePrinter", true, "Should items dropped by 3D printers be distributed?");

       // enabledTabs = config.Bind(new ConfigDefinition("CommandQueue", "EnabledQueues"), "Tier1,Tier2,Tier3,Lunar,Boss,VoidTier1,VoidTier2,VoidTier3,VoidBoss", new ConfigDescription($"Which item tiers should have queues?\nValid values: {string.Join(", ", Enum.GetNames(typeof(ItemTier)))}"));
        ItemTier[] itemTiers = (ItemTier[])Enum.GetValues(typeof(ItemTier));
        foreach(ItemTier tier in itemTiers){
            ConfigEntry<bool> entry = config.Bind("CommandQueue", $"{tier}", true, new ConfigDescription($"Should {tier} items be queued?"));
            entry.SettingChanged += UpdateSettings;
            enabledTabsConfig.Add(entry);
        }
        bigItemButtonContainer = config.Bind(new ConfigDefinition("UI", "BigItemSelectionContainer"), true, new ConfigDescription("false: Default command button layout\ntrue: Increase the space for buttons, helps avoid overflow with modded items"));
        bigItemButtonScale = config.Bind(new ConfigDefinition("UI", "BigItemSelectionScale"), 1f, new ConfigDescription("Scale applied to item buttons in the menu - decrease it if your buttons don't fit\nApplies only if BigItemSelectionContainer is true"));
        rightClickRemovesStack = config.Bind(new ConfigDefinition("UI", "RightClickRemovesStack"), true, new ConfigDescription("Should right-clicking an item in the queue remove the whole stack?"));

        overridePrinter.SettingChanged += UpdateSettings;
        overrideScrapper.SettingChanged += UpdateSettings;
        distributionMode.SettingChanged += UpdateSettings;
        timeOfDistribution.SettingChanged += UpdateSettings;

        bigItemButtonContainer.SettingChanged += UpdateSettings;
        bigItemButtonScale.SettingChanged += UpdateSettings;
        rightClickRemovesStack.SettingChanged += UpdateSettings;

        config.SettingChanged += ConfigOnSettingChanged;

        OnTeleport.OnReady += CheckReadyStatus;
        OnDrop.OnReady += CheckReadyStatus;
        OnConfigReady += DoMigrationIfReady;

        TomlTypeConverter.AddConverter(typeof(ItemTierSet), new TypeConverter
        {
            ConvertToObject = (str, type) => ItemTierSet.Deserialize(str),
            ConvertToString = (obj, type) => obj.ToString()
        });


        ModIcon = new Texture2D(2, 2);
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoCommandQueuePickup.icon.png"))
        using (MemoryStream memoryStream = new())
        {
            stream.CopyTo(memoryStream);
            byte[] data = memoryStream.ToArray();
            ModIcon.LoadImage(data);
        }

        if(!Directory.Exists(saveDirectory)) Directory.CreateDirectory(saveDirectory);
        CreateRiskOfOptionsConfig();
        DoMigrationIfReady();
    }

    public void CreateRiskOfOptionsConfig()
    {
        ModSettingsManager.SetModDescription("Automatically pickups up items that drop as well as CommandCubes if the command queue has been populated (Plus ProperSave Integration!)");
        ModSettingsManager.SetModIcon(Sprite.Create(ModIcon, new Rect(0, 0, ModIcon.width, ModIcon.height), new Vector2(0.5f, 0.5f)));
        ModSettingsManager.AddOption(new ChoiceOption(distributionMode));
        ModSettingsManager.AddOption(new ChoiceOption(timeOfDistribution));
        ModSettingsManager.AddOption(new CheckBoxOption(overrideScrapper));
        ModSettingsManager.AddOption(new CheckBoxOption(overridePrinter));
        //ModSettingsManager.AddOption(new StringInputFieldOption(enabledTabs));
        foreach(ConfigEntry<bool> entry in enabledTabsConfig){
            ModSettingsManager.AddOption(new CheckBoxOption(entry));
        }
        ModSettingsManager.SetModDescriptionToken("AUTO_COMMAND_QUEUE_PICKUP_DESCRIPTION");
        ModSettingsManager.AddOption(new SliderOption(bigItemButtonScale, new SliderConfig() { min = 0.1f, max = 2f }));
        ModSettingsManager.AddOption(new CheckBoxOption(bigItemButtonContainer));
        ModSettingsManager.AddOption(new CheckBoxOption(rightClickRemovesStack));
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


    public bool CheckTarget(CharacterMaster master)
    {
        return master != null && master.hasBody;
    }

    public bool ShouldDistribute(PickupIndex index, Cause cause)
    {
        // var distributeWrapper = cause == Cause.Drop ? Distribution.OnDrop : Distribution.OnTeleport;

        // if (!distributeWrapper.Value)
        //     return false;

        var pickupDef = PickupCatalog.GetPickupDef(index);

        if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None)
            return false;

        var itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);

        if (itemDef == null)
            return false;

        var filter = cause == Cause.Drop ? OnDrop : OnTeleport;

        return filter.CheckFilter(itemDef.itemIndex);
    }
    //Exact order:
    //Check config if teleportation is enabled at all. If not, don't distribute.
    //Check if PickupIndex refers to a valid pickup, and if that pickup has an ItemIndex. If not, don't distribute.
    //Check if command should be filtered by tier at all. If not, we don't care about the actual item, distribute.
    //Check if ItemIndex has an actual corresponding ItemDef. If not, don't distribute.
    //If we get to this point, rely on the correct ItemFilter to decide if we should distribute.
    public bool ShouldDistributeCommand(PickupIndex index, Cause cause)
    {
        // var teleportWrapper = cause == Cause.Drop ? teleportCommandOnDrop : teleportCommandOnTeleport;

        // if (!teleportWrapper.Value)
        //     return false;

        var pickupDef = PickupCatalog.GetPickupDef(index);

        if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None)
            return false;

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
            ItemTierSet self = new();
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
        public static string SerializeForConfig(string self)
        {
            return Serialize(Deserialize(self));
        }

        public override string ToString()
        {
            return Serialize(this);
        }
    }

    public void UpdateSettings(object sender, EventArgs e){
        if(e is SettingChangedEventArgs args){
            if(Enum.TryParse(args.ChangedSetting.Definition.Key, out ItemTier result)){
                enabledTabsConfig[(int)result].Value = Convert.ToBoolean(args.ChangedSetting.BoxedValue);
            } else if (args.ChangedSetting.Definition.Key == "BigItemSelectionContainer"){
                bigItemButtonContainer.Value = Convert.ToBoolean(args.ChangedSetting.BoxedValue);
            } else if (args.ChangedSetting.Definition.Key == "BigItemSelectionScale"){
                bigItemButtonScale.Value = Convert.ToSingle(args.ChangedSetting.BoxedValue);
            } else if (args.ChangedSetting.Definition.Key == "RightClickRemovesStack"){
                rightClickRemovesStack.Value = Convert.ToBoolean(args.ChangedSetting.BoxedValue);
            } else if(args.ChangedSetting.Definition.Key == "OverridePrinter"){
                overridePrinter.Value = Convert.ToBoolean(args.ChangedSetting.BoxedValue);
            } else if(args.ChangedSetting.Definition.Key == "OverrideScrapper"){
                overrideScrapper.Value = Convert.ToBoolean(args.ChangedSetting.BoxedValue);
            } else if(args.ChangedSetting.Definition.Key == "DistributionMode"){
                distributionMode.Value = (Mode)Enum.Parse(typeof(Mode), args.ChangedSetting.BoxedValue.ToString());
            } else if(args.ChangedSetting.Definition.Key == "TimeOfDistribution"){
                timeOfDistribution.Value = (Distribution)Enum.Parse(typeof(Distribution), args.ChangedSetting.BoxedValue.ToString());
            }
        }
        QueueManager.UpdateQueueAvailability();
    }
    public SortedSet<ItemTier> GetTierSetFromConfig()
    {
        return ItemTierSet.Deserialize(enabledTabs.Value);
    }
  
    public readonly string saveDirectory = System.IO.Path.Combine(Application.persistentDataPath, "AutoCommandQueuePickup");
    const string fileName = "commandQueueSlot_";
    const string extension = "csv";
    public void SaveQueue(int slot)
    {
        try
        {
            string pathToSave = System.IO.Path.Combine(saveDirectory, $"{fileName}{slot}.{extension}");
            string textToSave = "";
            ItemTier[] tiers = (ItemTier[])Enum.GetValues(typeof(ItemTier));
            foreach (ItemTier tier in tiers)
            {
                if(QueueManager.Peek(tier) == null) continue;
                textToSave += tier.ToString() + "," + QueueManager.DoesRepeat(tier).ToString() + ",";
                foreach (QueueManager.QueueEntry entry in QueueManager.mainQueues[tier])
                {
                    textToSave += $"{entry.pickupIndex.value}*{entry.count},";
                }
                textToSave += "\n";
            }
           if (!File.Exists(pathToSave)) File.Create(pathToSave).Dispose();
            using (StreamWriter tw = new StreamWriter(pathToSave))
            {
                tw.WriteLine(textToSave);
                tw.Dispose();
            }
        }
        catch (Exception e)
        {
            Log.Debug(e);
        }
    }
    public void LoadQueue(int slot)
    {
        string path = System.IO.Path.Combine(saveDirectory,$"{fileName}{slot}.{extension}");
        if (!File.Exists(path)) return;

        string content = File.ReadAllText(path).Trim();
        if (string.IsNullOrEmpty(content)) return;

        QueueManager.mainQueues.Clear();
        try
        {
            using(StreamReader sr = new (path)){
                string line;
                while ((line = sr.ReadLine()) != null){
                    if(line == "") continue;
                    string[] rawLinesSplit = line.Split(',');
                    ItemTier currTier = (ItemTier)Enum.Parse(typeof(ItemTier), rawLinesSplit[0]);
                    bool doesRepeat = Convert.ToBoolean(rawLinesSplit[1]);
                    QueueManager.ToggleRepeat(currTier);
                    for (int i = 2; i < rawLinesSplit.Length; i++)
                    {
                        if(string.IsNullOrEmpty(rawLinesSplit[i])) continue;

                        string[] s = rawLinesSplit[i].Split('*');
                        for(int j = 0; j < int.Parse(s[1]); j++){
                            QueueManager.Enqueue(new PickupIndex(Convert.ToInt32(s[0])));
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug(e);
        }
    }
    public static int totalItems = 0;
    public string PreviewSlot(int slot)
    {
        string path = System.IO.Path.Combine(saveDirectory,$"{fileName}{slot}.{extension}");        
        if (!File.Exists(path)) return "No Save";

        string content = File.ReadAllText(path).Trim();
        if (string.IsNullOrEmpty(content)) return "Empty";

        totalItems = 0;
        try
        {
            using(StreamReader sr = new (path)){
                string line;
                while ((line = sr.ReadLine()) != null){
                    if(line == "") continue;
                    string[] rawLinesSplit = line.Split(',');
                    for (int i = 2; i < rawLinesSplit.Length; i++)
                    {
                        if(rawLinesSplit[i] == "") continue;
                        string[] s = rawLinesSplit[i].Split('*');
                        totalItems += int.Parse(s[1]);
                    }
                }
            }
        }
        catch (Exception)
        {
            return "Error";
        }
        return totalItems > 0 ? $"{totalItems} item{(totalItems > 1 ? "s" : "")}" : "Empty";
    }
}