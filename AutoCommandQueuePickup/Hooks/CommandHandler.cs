using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System.Collections;
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
        IL.RoR2.Artifacts.CommandArtifactManager.OnDropletHitGroundServer +=
    IL_CommandArtifactManager_OnDropletHitGroundServer;

        TeleporterInteraction.onTeleporterChargedGlobal += OnTeleporterCharged;
    }

    private void IL_CommandArtifactManager_OnDropletHitGroundServer(ILContext il)
    {
        var cursor = new ILCursor(il);
        cursor.GotoNext(MoveType.After,
            i => i.MatchLdsfld("RoR2.Artifacts.CommandArtifactManager", "commandCubePrefab"));

        var labels = cursor.IncomingLabels;

        cursor.Emit(OpCodes.Ldarg_0);

        foreach (var label in labels) label.Target = cursor.Prev;

        cursor.EmitDelegate<ModifyCommandCubeSpawnDelegate>(
            (ref GenericPickupController.CreatePickupInfo pickupInfo) =>
            {
                ItemTier itemTier = PickupCatalog.GetPickupDef(pickupInfo.pickupIndex).itemTier;
                IEnumerable<(ItemTier, PickupIndex)> itemQueue = QueueManager.PeekAll();
                if (!itemQueue.Any() || !QueueManager.PeekForItemTier(itemTier))
                {
                    AutoCommandQueuePickup.dontDestroy = true;
                    if (ModConfig.ShouldDistributeCommand(pickupInfo.pickupIndex, Cause.Drop))
                    {
                        pickupInfo.position = GetTargetLocation();
                    }
                    else if (TeleporterInteraction.instance &&
                         TeleporterInteraction.instance.isCharged &&
                         ModConfig.ShouldDistributeCommand(pickupInfo.pickupIndex, Cause.Teleport))
                    {
                        pickupInfo.position = GetTeleporterCommandTargetPosition();
                    }
                }
                else
                {
                    AutoCommandQueuePickup.dontDestroy = false;
                    PickupIndex poppedIndex = QueueManager.Pop(itemTier);
                    PickupDef poppedDef = PickupCatalog.GetPickupDef(poppedIndex);
                    List<PickupIndex> tierList = ItemUtil.GetItemsFromIndex(poppedIndex);
                    CharacterMaster master = CharacterMasterManager.playerCharacterMasters.First().Value;
                    if (tierList.Contains(poppedIndex))
                    {
                        ItemIndex itemIndex = PickupCatalog.GetPickupDef(poppedIndex).itemIndex;
                        master.inventory.GiveItem(itemIndex, 1);
                        var networkUser = master.playerCharacterMasterController?.networkUser;
                        Chat.AddMessage(new Chat.PlayerPickupChatMessage
                            {
                                subjectAsNetworkUser = networkUser,
                                baseToken = "PLAYER_PICKUP",
                                pickupToken = poppedDef?.nameToken ?? PickupCatalog.invalidPickupToken,
                                pickupColor = poppedDef?.baseColor ?? Color.black,
                                pickupQuantity = (uint)master.inventory.GetItemCount(itemIndex)
                            }.ConstructChatString());
                        GenericPickupController.SendPickupMessage(master, poppedIndex);
                    }
                }
            });
    }
    public override void UnregisterHooks()
    {
        IL.RoR2.Artifacts.CommandArtifactManager.OnDropletHitGroundServer -=
            IL_CommandArtifactManager_OnDropletHitGroundServer;
    }

    private Vector3 GetTargetLocation()
    {
        return LocalUserManager.GetFirstLocalUser().cachedBodyObject.transform.position + Vector3.up * 2;
    }
    private Vector3 GetTeleporterCommandTargetPosition()
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