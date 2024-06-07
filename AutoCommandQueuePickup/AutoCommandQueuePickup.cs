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
using System.IO;

[assembly: AssemblyVersion(AutoCommandQueuePickup.AutoCommandQueuePickup.PluginVersion)]
namespace AutoCommandQueuePickup;

[BepInDependency("com.bepis.r2api")]
[BepInDependency("com.KingEnderBrine.ProperSave")]
[BepInDependency("com.rune580.riskofoptions")]
[BepInIncompatibility("com.kuberoot.commandqueue")]
[BepInIncompatibility("com.kuberoot.autoitempickup")] 
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class AutoCommandQueuePickup : BaseUnityPlugin
{
    public const string PluginAuthor = "symmys";
    public const string PluginName = "AutoCommandQueuePickup";
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginVersion = "1.0.4";
    private static readonly MethodInfo GenericPickupController_AttemptGrant =
        typeof(GenericPickupController).GetMethod("AttemptGrant",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static readonly FieldInfo GenericPickupController_consumed =
        typeof(GenericPickupController).GetField("consumed",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    public ItemDistributor Distributor { get; private set; }
    public static Config config { get; private set; }
    private bool distributorNeedsUpdate;

    //start init CommandQueue config
    public static Action PluginUnloaded;
    public static bool IsLoaded;    
    public static bool dontDestroy = false;
    private HookManager hookManager;
    public readonly string LastCommandQueuePath = Path.Combine(Application.persistentDataPath, "ProperSave", "Saves") + "\\" + "LastCommandQueue" + ".csv";
    private static GameObject commandUIPrefab;
    public static readonly FieldInfo commandCubePrefabField = typeof(RoR2.Artifacts.CommandArtifactManager).GetField("commandCubePrefab", BindingFlags.Static | BindingFlags.Public);
    private static readonly FieldInfo PickupPickerController_options = typeof(PickupPickerController).GetField("options", BindingFlags.Instance | BindingFlags.NonPublic);

    private void Awake(){
        Log.Init(Logger);
        //AutoPickupItem config init
        config = new Config(this, Logger);
    }

    private void SaveFile_OnGatherSaveData(Dictionary<string, object> obj)
        {
            SaveAndLoad.Save(LastCommandQueuePath);
        }

        private void Loading_OnLoadingEnded(ProperSave.SaveFile _)
        {
            if (File.Exists(LastCommandQueuePath))
            {
                SaveAndLoad.Load(LastCommandQueuePath);
            }
            else
            {
                Log.Warning("CommandQueue save file does not exist!");
            }
        }
    public void OnEnable()
    {
        hookManager = new HookManager(this);
        hookManager.RegisterHooks();
        commandUIPrefab = (commandCubePrefabField.GetValue(null) as GameObject)?.GetComponent<PickupPickerController>().panelPrefab;
        
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
        //end init AutoPickupItem config

        //AutoPickupItem config
        PlayerCharacterMasterController.onPlayerAdded += UpdateTargetsWrapper;
        PlayerCharacterMasterController.onPlayerRemoved += UpdateTargetsWrapper;
        config.distributionMode.SettingChanged += (_, _) =>
        Distributor = ItemDistributor.GetItemDistributor(config.distributionMode.Value, this);

        //CommandQueue config init
        IsLoaded = true;

        On.RoR2.UI.ScoreboardController.Awake += ScoreboardController_Awake;
        On.RoR2.PickupPickerController.OnDisplayBegin += HandleCommandDisplayBegin;
        On.RoR2.Artifacts.CommandArtifactManager.Init += CommandArtifactManager_Init;
        QueueManager.Enable();
        foreach (var component in FindObjectsOfType<HUD>())
        {
            component.scoreboardPanel.AddComponent<UIManager>();
        }
        //end init CommandQueue config
        //proper save integration
        ProperSave.SaveFile.OnGatherSaveData += SaveFile_OnGatherSaveData;
        ProperSave.Loading.OnLoadingEnded += Loading_OnLoadingEnded;
    }
    public void OnDisable()
    {
        CharacterMasterManager.playerCharacterMasters.Clear();
        hookManager.UnregisterHooks();
        // TODO: Check if this works for non-hooks
        // Cleanup any leftover hooks
        HookEndpointManager.RemoveAllOwnedBy(HookEndpointManager.GetOwner(OnDisable));

        PlayerCharacterMasterController.onPlayerAdded -= UpdateTargetsWrapper;
        PlayerCharacterMasterController.onPlayerRemoved -= UpdateTargetsWrapper;
        IsLoaded = false;
        PluginUnloaded?.Invoke();
        On.RoR2.UI.ScoreboardController.Awake -= ScoreboardController_Awake;
        QueueManager.Disable();
    }
    private void CommandArtifactManager_Init(On.RoR2.Artifacts.CommandArtifactManager.orig_Init orig)
    {
        orig();
        commandUIPrefab = (commandCubePrefabField.GetValue(null) as GameObject)?.GetComponent<PickupPickerController>().panelPrefab;
    }


    private void HandleCommandDisplayBegin(On.RoR2.PickupPickerController.orig_OnDisplayBegin orig, PickupPickerController self, NetworkUIPromptController networkUIPromptController, LocalUser localUser, CameraRigController cameraRigController)
    {
        if (self.panelPrefab == commandUIPrefab)
        {
            foreach(var (tier, index) in QueueManager.PeekAll())
            {
                if (self.IsChoiceAvailable(index))
                {
                    QueueManager.Pop(tier);
                    PickupPickerController.Option[] options = (PickupPickerController.Option[])PickupPickerController_options.GetValue(self);

                    for (int j = 0; j < options.Length; j++)
                    {
                        if(options[j].pickupIndex == index && options[j].available)
                        {
                            IEnumerator submitChoiceNextFrame()
                            {
                                yield return 0;
                                self.SubmitChoice(j);
                            }
                            self.StartCoroutine(submitChoiceNextFrame());
                            break;
                        }
                    }
                    
                    return;
                }
            }
        }

        orig(self, networkUIPromptController, localUser, cameraRigController);
    }

    private void ScoreboardController_Awake(On.RoR2.UI.ScoreboardController.orig_Awake orig, ScoreboardController self)
    {
        UIManager u = self.gameObject.AddComponent<UIManager>();
        orig(self);
    }
    
    private void UpdateTargetsWrapper(PlayerCharacterMasterController player)
    {
        if(CharacterMasterManager.playerCharacterMasters.ContainsKey(player.master.netId.Value)) return;
        CharacterMasterManager.playerCharacterMasters.Add(player.master.netId.Value, player.master);
        UpdateTargets();
    }

    private void OnPlayerAwake(On.RoR2.PlayerCharacterMasterController.orig_Awake orig,
        PlayerCharacterMasterController self)
    {
        orig(self);

        if (!NetworkServer.active) return;
        var master = self.master;
        if (master) master.onBodyStart += obj => UpdateTargets();
    }

    private void ModifyDropletCollision(ILContext il)
    {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.After, i => i.MatchCall<PickupDropletController>("CreatePickup"));
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Dup);
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

    public static bool GrantCommandItem(PickupIndex pickupIndex, CharacterMaster master)
    {
        var itemIndex = PickupCatalog.GetPickupDef(pickupIndex)?.itemIndex ?? ItemIndex.None;
        if (itemIndex != ItemIndex.None)
        {
            master.inventory.GiveItem(itemIndex);
            var playerCharacterMasterController = master.playerCharacterMasterController;
            var networkUser = playerCharacterMasterController?.networkUser;
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

            return true;
        }
        return false;
    }
    private void UpdateTargets()
    {
        distributorNeedsUpdate = true;
    }

    public void PreDistributeItemInternal(Cause cause)
    {
        if (Distributor == null)
            Distributor = ItemDistributor.GetItemDistributor(config.distributionMode.Value, this);

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
