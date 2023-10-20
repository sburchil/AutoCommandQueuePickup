using AutoCommandQueuePickup.Configuration;
using AutoCommandQueuePickup.Hooks;
using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using RoR2;
using RoR2.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Path = System.IO.Path;
using UnityEngine;
using UnityEngine.Networking;


namespace AutoCommandQueuePickup;

[BepInDependency("com.bepis.r2api")]
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[R2API.Utils.NetworkCompatibility(R2API.Utils.CompatibilityLevel.NoNeedForSync, R2API.Utils.VersionStrictness.DifferentModVersionsAreOk)]
public class AutoCommandQueuePickup : BaseUnityPlugin
{
    public const string PluginAuthor = "symmys";
    public const string PluginName = "AutoCommandQueuePickup";
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginVersion = "1.0.0";
    private static readonly MethodInfo GenericPickupController_AttemptGrant =
        typeof(GenericPickupController).GetMethod("AttemptGrant",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static readonly FieldInfo GenericPickupController_consumed =
        typeof(GenericPickupController).GetField("consumed",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    public ItemDistributor Distributor { get; private set; }
    public static Config AutoPickupConfig { get; private set; }
    private bool distributorNeedsUpdate;

    //start init CommandQueue config
    public static Action PluginUnloaded;
    public static bool IsLoaded;    

    public static bool dontDestroy = false;
    private HookManager hookManager;
    private static readonly FieldInfo CommandCubePrefabField = typeof(RoR2.Artifacts.CommandArtifactManager).GetField("commandCubePrefab", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly FieldInfo PickupPickerControllerOptions = typeof(PickupPickerController).GetField("options", BindingFlags.Instance | BindingFlags.NonPublic);
    
    public static string bepinExPluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    public void Awake(){
        //CommandQueue config init
        ModConfig.InitConfig(Config);
        ModConfig.enabledTabs.SettingChanged += (_, __) => FakeReload();
        ModConfig.bigItemButtonContainer.SettingChanged += (_, __) => FakeReload();
        ModConfig.bigItemButtonScale.SettingChanged += (_, __) => FakeReload();
    }
    
    public void OnEnable()
    {
        Log.Init(Logger);
        //AutoPickupItem config init
        AutoPickupConfig = new Config(this, Logger);

        hookManager = new HookManager(this);
        hookManager.RegisterHooks();
        
        On.RoR2.PlayerCharacterMasterController.OnBodyDeath += (orig, self) =>
        {
            orig(self);
            UpdateTargets();
        };
        On.RoR2.PlayerCharacterMasterController.OnBodyStart += (orig, self) =>
        {
            orig(self);
            UpdateTargets();
        };

        IL.RoR2.PickupDropletController.OnCollisionEnter += ModifyDropletCollision;

        On.RoR2.PlayerCharacterMasterController.Awake += OnPlayerAwake;

        //AutoPickupItem config
        PlayerCharacterMasterController.onPlayerAdded += UpdateTargetsWrapper;
        PlayerCharacterMasterController.onPlayerRemoved += UpdateTargetsWrapper;
        AutoPickupConfig.distributeToDeadPlayers.SettingChanged += (_, _) => UpdateTargets();
        AutoPickupConfig.distributionMode.SettingChanged += (_, _) =>
        Distributor = ItemDistributor.GetItemDistributor(AutoPickupConfig.distributionMode.Value, this);

        //CommandQueue config init
        IsLoaded = true;

        On.RoR2.UI.ScoreboardController.Awake += ScoreboardController_Awake;
        QueueManager.Enable();
        foreach (var component in FindObjectsOfType<HUD>())
        {
            component.scoreboardPanel.AddComponent<UIManager>();
        }
        //end init CommandQueue config
       
        On.RoR2.Run.Start += (orig, self) =>
        {
            orig(self);
            Storage.InitStorage();
        };

        NetworkIdentity.onNetworkIdAssigned += (identity) =>
        {
            GameObject gameObject = identity.gameObject;
            if(gameObject.name == "CommandCube(Clone)"){
                // destroy the command cube only if the queue is empty or if the tier is not in the queue
                if(!dontDestroy) Destroy(gameObject);
            }
        };
    }
    public void OnDisable()
    {
        hookManager.UnregisterHooks();
        // TODO: Check if this works for non-hooks
        // Cleanup any leftover hooks
        HookEndpointManager.RemoveAllOwnedBy(
            HookEndpointManager.GetOwner(OnDisable));

        PlayerCharacterMasterController.onPlayerAdded -= UpdateTargetsWrapper;
        PlayerCharacterMasterController.onPlayerRemoved -= UpdateTargetsWrapper;
    }
    private void ScoreboardController_Awake(On.RoR2.UI.ScoreboardController.orig_Awake orig, ScoreboardController self)
    {
        self.gameObject.AddComponent<UIManager>();
        orig(self);
    }

    private bool isFakeReloading = false;

    private void FakeReload()
    {
        if (isFakeReloading) return;
        isFakeReloading = true;
        IEnumerator doFakeReload()
        {
            yield return 0;
            OnDisable();
            yield return 0;
            OnEnable();
            isFakeReloading = false;
        }
        StartCoroutine(doFakeReload());
    }

    private void UpdateTargetsWrapper(PlayerCharacterMasterController player)
    {

        UpdateTargets();
    }

    private void OnPlayerAwake(On.RoR2.PlayerCharacterMasterController.orig_Awake orig,
        PlayerCharacterMasterController self)
    {
        orig(self);

        if (!NetworkServer.active) return;
        CharacterMasterManager.playerCharacterMasters.Add(self.master.netId.Value, self.master);
        var master = self.master;
        if (master) master.onBodyStart += obj => UpdateTargets();
    }

    private void ModifyDropletCollision(ILContext il)
    {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.After, i => i.MatchCall<GenericPickupController>("CreatePickup"));
        cursor.Emit(OpCodes.Dup);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Action<GenericPickupController, PickupDropletController>>(
            (pickupController, self) =>
            {
                var behaviour = self.GetComponent<OverrideDistributorBehaviour>();
                if (behaviour)
                {
                    var newBehaviour = pickupController.gameObject.AddComponent<OverrideDistributorBehaviour>();
                    newBehaviour.Distributor = behaviour.Distributor;
                }
            });
    }

    [Server]
    public static void GrantItem(GenericPickupController item, CharacterMaster master)
    {
        if (master.hasBody)
        {
            GenericPickupController_AttemptGrant.Invoke(item, new object[] { master.GetBody() });
        }
        else
        {
            // The game no longer supports granting items to dead players; do it manually instead
            var itemIndex = PickupCatalog.GetPickupDef(item.pickupIndex)?.itemIndex ?? ItemIndex.None;
            if (itemIndex != ItemIndex.None)
            {
                master.inventory.GiveItem(itemIndex);

                var playerCharacterMasterController = master.playerCharacterMasterController;
                var networkUser = playerCharacterMasterController != null
                    ? playerCharacterMasterController.networkUser
                    : null;
                var pickupDef = PickupCatalog.GetPickupDef(item.pickupIndex);
                // Based on RoR2.GenericPickupController.HandlePickupMessage
                Chat.AddMessage(new Chat.PlayerPickupChatMessage
                {
                    subjectAsNetworkUser = networkUser,
                    baseToken = "PLAYER_PICKUP",
                    pickupToken = pickupDef?.nameToken ?? PickupCatalog.invalidPickupToken,
                    pickupColor = pickupDef?.baseColor ?? Color.black,
                    pickupQuantity = (uint)master.inventory.GetItemCount(itemIndex)
                }.ConstructChatString());

                GenericPickupController_consumed.SetValue(item, true);
                Destroy(item.gameObject);
            }
        }
    }

    public static void GrantCommandItem(PickupIndex pickupIndex, CharacterMaster master)
    {
        var itemIndex = PickupCatalog.GetPickupDef(pickupIndex)?.itemIndex ?? ItemIndex.None;
        if (itemIndex != ItemIndex.None)
        {
            master.inventory.GiveItem(itemIndex);

            var playerCharacterMasterController = master.playerCharacterMasterController;
            var networkUser = playerCharacterMasterController != null
                ? playerCharacterMasterController.networkUser
                : null;
            var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            // Based on RoR2.GenericPickupController.HandlePickupMessage
            Chat.AddMessage(new Chat.PlayerPickupChatMessage
            {
                subjectAsNetworkUser = networkUser,
                baseToken = "PLAYER_PICKUP",
                pickupToken = pickupDef?.nameToken ?? PickupCatalog.invalidPickupToken,
                pickupColor = pickupDef?.baseColor ?? Color.black,
                pickupQuantity = (uint)master.inventory.GetItemCount(itemIndex)
            }.ConstructChatString());
        }
    }
    private void UpdateTargets()
    {
        distributorNeedsUpdate = true;
    }

    public void PreDistributeItemInternal(Cause cause)
    {
        if (Distributor == null)
            Distributor = ItemDistributor.GetItemDistributor(AutoPickupConfig.distributionMode.Value, this);

        if (distributorNeedsUpdate)
        {
            distributorNeedsUpdate = false;
            Distributor?.UpdateTargets();
        }
    }

    public void DistributeItemInternal(GenericPickupController item, Cause cause)
    {
        var distributor = GetDistributorInternal(item.gameObject);

        try
        {
            distributor.DistributeItem(item);
        }
        catch (Exception e)
        {
            Logger.LogError($"Caught AutoItemPickup distributor exception:\n{e}\n{e.StackTrace}");
        }
    }

    public void DistributeItem(GenericPickupController item, Cause cause)
    {
        PreDistributeItemInternal(cause);
        DistributeItemInternal(item, cause);
    }

    public void DistributeItems(IEnumerable<GenericPickupController> items, Cause cause)
    {
        PreDistributeItemInternal(cause);
        foreach (var item in items) DistributeItemInternal(item, cause);
    }

    public ItemDistributor GetDistributorInternal(GameObject item)
    {
        if (item != null)
        {
            var overrideDistributorBehaviour = item.GetComponent<OverrideDistributorBehaviour>();
            if (overrideDistributorBehaviour != null)
                return overrideDistributorBehaviour.Distributor;
        }

        return Distributor;
    }

    public ItemDistributor GetDistributor(GameObject item, Cause cause)
    {
        PreDistributeItemInternal(cause);
        return GetDistributorInternal(item);
    }

}

