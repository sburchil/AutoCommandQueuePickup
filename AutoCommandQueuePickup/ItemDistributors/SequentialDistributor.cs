using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup.ItemDistributors;

internal class SequentialDistributor : ItemDistributor
{
    private int index;
    private CharacterMaster[] playerDistribution;

    public SequentialDistributor(AutoCommandQueuePickup plugin) : base(plugin) { }

    public override void UpdateTargets()
    {
        var oldTarget = playerDistribution?[index];

        var filteredPlayers = GetValidTargets();
        playerDistribution = new CharacterMaster[filteredPlayers.Count];

        index = -1;

        for (var i = 0; i < filteredPlayers.Count; i++)
        {
            var player = filteredPlayers[i];

            playerDistribution[i] = player.master;
            if (player.master == oldTarget)
                index = i;
        }

        if (index == -1)
            index = Random.Range(0, playerDistribution.Length);
    }

    public override CharacterMaster GetTarget(Vector3 position, PickupIndex pickupIndex,
        TargetFilter extraFilter = null)
    {
        if (playerDistribution.Length == 0)
            return null;

        CharacterMaster target = null;

        if (extraFilter != null)
        {
            for (var indexOffset = 0; indexOffset < playerDistribution.Length; indexOffset++)
            {
                var potentialTarget = playerDistribution[(index + indexOffset) % playerDistribution.Length];
                if (extraFilter(potentialTarget))
                {
                    target = potentialTarget;
                    index = (index + indexOffset + 1) % playerDistribution.Length;
                    break;
                }
            }
        }
        else
        {
            target = playerDistribution[index];
            index = ++index % playerDistribution.Length;
        }

        return target;
    }
}