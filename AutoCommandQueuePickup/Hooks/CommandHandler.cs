using R2API.Utils;
using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Random = UnityEngine.Random;

namespace AutoCommandQueuePickup.Hooks;

public class CommandHandler : AbstractHookHandler
{
    public override void RegisterHooks()
    {
        On.RoR2.Artifacts.CommandArtifactManager.OnDropletHitGroundServer +=
    On_CommandArtifactManager_OnDropletHitGroundServer;
        TeleporterInteraction.onTeleporterChargedGlobal += OnTeleporterCharged;
    }
    public static void On_CommandArtifactManager_OnDropletHitGroundServer(On.RoR2.Artifacts.CommandArtifactManager.orig_OnDropletHitGroundServer orig, 
        ref GenericPickupController.CreatePickupInfo pickupInfo, ref bool shouldSpawn)
    {
        PickupIndex pickupIndex = pickupInfo.pickupIndex;
        PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
        if (pickupDef == null || pickupDef.itemIndex == ItemIndex.None && pickupDef.equipmentIndex == EquipmentIndex.None && pickupDef.itemTier == ItemTier.NoTier)
        {
            return;
        }
        shouldSpawn = false;
        ItemTier itemTier = PickupCatalog.GetPickupDef(pickupInfo.pickupIndex).itemTier;
        IEnumerable<(ItemTier, PickupIndex)> itemQueue = QueueManager.PeekAll();
        if (!itemQueue.Any() || !QueueManager.PeekForItemTier(itemTier))
        {
            AutoCommandQueuePickup.dontDestroy = true;
            if (AutoCommandQueuePickup.config.ShouldDistributeCommand(pickupInfo.pickupIndex, Cause.Drop))
            {
                pickupInfo.position = GetTargetLocation();
            }
            else if (TeleporterInteraction.instance &&
                    TeleporterInteraction.instance.isCharged &&
                    AutoCommandQueuePickup.config.ShouldDistributeCommand(pickupInfo.pickupIndex, Cause.Teleport))
            {
                pickupInfo.position = GetTeleporterCommandTargetPosition();
            }
            orig(ref pickupInfo, ref shouldSpawn);
        }
        else
        {
            GameObject commandPrefab =(GameObject)AutoCommandQueuePickup.commandCubePrefabField.GetValue(null);
            GameObject gameObject = Object.Instantiate<GameObject>(commandPrefab, pickupInfo.position, pickupInfo.rotation);
            gameObject.GetComponent<PickupIndexNetworker>().NetworkpickupIndex = pickupIndex;
            gameObject.GetComponent<PickupPickerController>().SetOptionsFromPickupForCommandArtifact(pickupIndex);
            PickupIndex poppedIndex = QueueManager.Pop(itemTier);
            CharacterMaster master = LocalUserManager.GetFirstLocalUser().cachedMaster;
            AutoCommandQueuePickup.GrantCommandItem(poppedIndex, master);
            GameObject.Destroy(gameObject);
            pickupInfo = new GenericPickupController.CreatePickupInfo();
            orig(ref pickupInfo, ref shouldSpawn);
        }
    }

    public override void UnregisterHooks()
    {
        // IL.RoR2.Artifacts.CommandArtifactManager.OnDropletHitGroundServer -=
        //     IL_CommandArtifactManager_OnDropletHitGroundServer;
        On.RoR2.Artifacts.CommandArtifactManager.OnDropletHitGroundServer -=
            On_CommandArtifactManager_OnDropletHitGroundServer;
    }

    private static Vector3 GetTargetLocation()
    {
        return LocalUserManager.GetFirstLocalUser().cachedBodyObject.transform.position + Vector3.up * 2;
    }
    private static Vector3 GetTeleporterCommandTargetPosition()
    {
        Vector3 spawnposition;

        var center = TeleporterInteraction.instance.transform.position;

        var angle = Random.Range(0, Mathf.PI * 2f);
        float distance = Random.Range(4, 15);
        spawnposition = center + Mathf.Sin(angle) * distance * Vector3.forward +
                        Mathf.Cos(angle) * distance * Vector3.right + Vector3.up * 10;

        return spawnposition;
    }

    private void TeleportToTeleporter(NetworkBehaviour obj)
    {
        TeleportHelper.TeleportGameObject(obj.gameObject, GetTeleporterCommandTargetPosition());
    }
    private void OnTeleporterCharged(TeleporterInteraction obj)
    {
        if (!NetworkServer.active || !ModConfig.teleportCommandOnTeleport.Value) return;

        var originalPickups = InstanceTracker.GetInstancesList<PickupPickerController>();

        var pickups = new PickupPickerController[originalPickups.Count];

        originalPickups.CopyTo(pickups);

        var teleporterPosition = TeleporterInteraction.instance.transform.position;

        foreach (var pickup in pickups)
        {
            var pickupIndexNetworker = pickup.GetComponent<PickupIndexNetworker>();
            if (pickupIndexNetworker && (pickup.transform.position - teleporterPosition).sqrMagnitude > 100 &&
                ModConfig.ShouldDistributeCommand(pickupIndexNetworker.pickupIndex, Cause.Teleport))
                TeleportToTeleporter(pickup);
        }
    }

    protected delegate void ModifyCommandCubeSpawnDelegate(ref GenericPickupController.CreatePickupInfo pickupinfo);
}