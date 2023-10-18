using AutoCommandQueuePickup.CommandQueue;
using AutoCommandQueuePickup.Configuration;
using AutoCommandQueuePickup.Hooks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using RoR2;
using RoR2.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace AutoCommandQueuePickup;

[BepInDependency("com.bepis.r2api")]
[BepInPlugin("dev.symmys.AutoCommandQueuePickup", "AutoCommandQueuePickup", "1.0.0")]
public class AutoCommandQueuePickup : BaseUnityPlugin
{
    private static readonly MethodInfo GenericPickupController_AttemptGrant =
        typeof(GenericPickupController).GetMethod("AttemptGrant",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static readonly FieldInfo GenericPickupController_consumed =
        typeof(GenericPickupController).GetField("consumed",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private bool distributorNeedsUpdate;

    private HookManager hookManager;

    //create public static logger for all classes under namespace
    public static ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("AutoCommandQueuePickup - DEBUG");

    public ItemDistributor Distributor { get; private set; }

    public static Config ModConfig { get; private set; }


    public static event Action PluginUnloaded;
    public static bool IsLoaded;

    public static Dictionary<GameObject, ItemTier> commandArtifactsActive = new();
    public static GameObject commandUIPrefab;
    public static readonly FieldInfo commandCubePrefabField = typeof(RoR2.Artifacts.CommandArtifactManager).GetField("commandCubePrefab", BindingFlags.Static | BindingFlags.NonPublic);
    public static readonly FieldInfo PickupPickerController_options = typeof(PickupPickerController).GetField("options", BindingFlags.Instance | BindingFlags.NonPublic);

    public void OnEnable()
    {
        // Debug code for local multiplayer testing
        // On.RoR2.Networking.NetworkManagerSystemSteam.OnClientConnect += (s, u, t) => { };
        var harmony = new Harmony("dev.symmys.AutoCommandQueuePickup");
        harmony.PatchAll();
        ModConfig = new Config(this, Logger);

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

        PlayerCharacterMasterController.onPlayerAdded += UpdateTargetsWrapper;
        PlayerCharacterMasterController.onPlayerRemoved += UpdateTargetsWrapper;

        ModConfig.distributeToDeadPlayers.SettingChanged += (_, _) => UpdateTargets();
        ModConfig.distributionMode.SettingChanged += (_, _) =>
        Distributor = ItemDistributor.GetItemDistributor(ModConfig.distributionMode.Value, this);

        //CommandQueue config
        ModConfig.enabledTabs.SettingChanged += (_, __) => FakeReload();
        ModConfig.bigItemButtonContainer.SettingChanged += (_, __) => FakeReload();
        ModConfig.bigItemButtonScale.SettingChanged += (_, __) => FakeReload();

        commandUIPrefab = (commandCubePrefabField.GetValue(null) as GameObject)?.GetComponent<PickupPickerController>().panelPrefab;
        IsLoaded = true;

        On.RoR2.PickupPickerController.OnDisplayBegin += HandleCommandDisplayBegin;
        On.RoR2.UI.ScoreboardController.Awake += ScoreboardController_Awake;
        On.RoR2.Artifacts.CommandArtifactManager.Init += CommandArtifactManager_Init;


        QueueManager.Enable();

        foreach (var component in FindObjectsOfType<HUD>())
        {
            component.scoreboardPanel.AddComponent<UIManager>();
        }


        StartCoroutine(CheckForCommandDrops());

    }

    private IEnumerator CheckForCommandDrops()
    {
        while (true)
        {
            if (commandArtifactsActive.Count > 0)
            {
                foreach (var pair in commandArtifactsActive)
                {
                    GameObject obj = pair.Key;
                    ItemTier tier = pair.Value;

                    IEnumerable<(ItemTier, PickupIndex)> queue = QueueManager.PeekAll();
                    if (queue == null) break;
                    foreach (var (itemTier, index) in queue)
                    {
                        if (itemTier == ItemTier.NoTier) continue;
                        if (tier == itemTier)
                        {
                            AutoCommandQueuePickup.log.LogInfo($"Command Item Spawned: {tier}");
                            //QueueManager.Pop(tier);
                            break;
                        }
                    }
                }
            } else
            {
                //GameObject.FindObjectsOfType<GameObject>().Where(go => go.GetComponent<PickupPickerController>() != null).Select(go => go)
                foreach (GameObject obj in FindObjectsOfType<GameObject>())
                {
                    if (obj.name.Contains("CommandCube(Clone)") && !commandArtifactsActive.ContainsKey(obj))
                    {
                        commandArtifactsActive.Add(obj, ItemTier.Tier1);
                        log.LogWarning($"command cube added at : {obj} position: {obj.transform.position}");
                        break;
                    }
                }

            }
            yield return new WaitForSeconds(1f);
        }
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
            foreach (var (tier, index) in QueueManager.PeekAll())
            {
                if (self.IsChoiceAvailable(index))
                {
                    QueueManager.Pop(tier);
                    PickupPickerController.Option[] options = (PickupPickerController.Option[])AutoCommandQueuePickup.PickupPickerController_options.GetValue(self);

                    for (int j = 0; j < options.Length; j++)
                    {
                        if (options[j].pickupIndex == index && options[j].available)
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
        self.gameObject.AddComponent<UIManager>();
        orig(self);
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
            Distributor = ItemDistributor.GetItemDistributor(ModConfig.distributionMode.Value, this);

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

    public static void OnCollide(Collision collision)
    {
        foreach (ContactPoint pt in collision.contacts)
        {
            GameObject collidingObject = pt.otherCollider.gameObject;
            AutoCommandQueuePickup.log.LogWarning(collidingObject);
        }
    }

    public ItemDistributor GetDistributor(GameObject item, Cause cause)
    {
        PreDistributeItemInternal(cause);
        return GetDistributorInternal(item);
    }

    [HarmonyPatch(typeof(RoR2.PickupDropletController), "OnCollisionEnter")]
    class CommandCubeInitializationPatch
    {
        static void Postfix(Collision collision)
        {
            log.LogWarning($"Command Cube collision detected at {collision}");

            Ray ray = new(collision.transform.position, Vector3.up);
            // Check if this collision involves a Command Cube droplet
/*            if (collision.gameObject.name == "CommandCube(Clone)")
            {
                GameObject commandCube = collision.gameObject;
                
                log.LogWarning($"Command Cube collision detected at {commandCube.transform.position}");
                // Modify the Command Cube's properties after collision but before full initialization
                //commandCube.transform.localScale = new Vector3(2f, 2f, 2f);
            }*/
        }
    }

}

