using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace AutoCommandQueuePickup.Hooks;

public class CommandHandler : AbstractHookHandler
{
    private static ManualLogSource log = Logger.CreateLogSource("ItemDistributor");

    public override void RegisterHooks()
    {
        IL.RoR2.Artifacts.CommandArtifactManager.OnDropletHitGroundServer +=
    IL_CommandArtifactManager_OnDropletHitGroundServer;

        TeleporterInteraction.onTeleporterChargedGlobal += OnTeleporterCharged;
    }

    private void IL_CommandArtifactManager_OnDropletHitGroundServer(ILContext il)
    {
        var cursor = new ILCursor(il);
        GenericPickupController.CreatePickupInfo targetPickupInfo = new();
        cursor.GotoNext(MoveType.Before,
            i => i.MatchLdsfld("RoR2.Artifacts.CommandArtifactManager", "commandCubePrefab"));

        var labels = cursor.IncomingLabels;

        cursor.Emit(OpCodes.Ldarg_0);

        foreach (var label in labels) label.Target = cursor.Prev;

        cursor.EmitDelegate<ModifyCommandCubeSpawnDelegate>(
            (ref GenericPickupController.CreatePickupInfo pickupInfo) =>
            {
                if (ModConfig.ShouldDistributeCommand(pickupInfo.pickupIndex, Cause.Drop))
                {
                    targetPickupInfo = pickupInfo;
                    pickupInfo.position = GetTargetLocation();
                }
                else if (TeleporterInteraction.instance &&
                     TeleporterInteraction.instance.isCharged &&
                     ModConfig.ShouldDistributeCommand(pickupInfo.pickupIndex, Cause.Teleport))
                {
                    targetPickupInfo = pickupInfo;
                    pickupInfo.position = GetTeleporterCommandTargetPosition();
                }
            });
        log.LogWarning($"CommandCube: {targetPickupInfo.position}");
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