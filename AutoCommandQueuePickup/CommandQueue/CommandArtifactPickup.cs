
using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup;
public class CommandArtifactPickup : MonoBehaviour
{
    public PickupIndex pickupIndex;
    public PickupDropletController pickupDropletController;
    public CharacterMaster characterMaster;
    public bool isCommand;
}