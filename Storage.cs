using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace AutoCommandQueuePickup
{
    public class Storage
    {
        public static List<RoR2.PickupIndex> Tier1 = null;
        public static List<RoR2.PickupIndex> Tier2 = null;
        public static List<RoR2.PickupIndex> Tier3 = null;
        public static List<RoR2.PickupIndex> Void1 = null;
        public static List<RoR2.PickupIndex> Void2 = null;
        public static List<RoR2.PickupIndex> Void3 = null;
        public static List<RoR2.PickupIndex> Boss = null;
        public static List<RoR2.PickupIndex> Lunar = null;
        public static List<RoR2.PickupIndex> Equipment = null;
        public static List<RoR2.PickupIndex> LunarEquipment = null;
        public static GameObject Prefab = null;
        public static GameObject Garbage = null;

        public static void InitStorage()
        {
            Run run = RoR2.Run.instance;

            Tier1 = run.availableTier1DropList;
            Tier2 = run.availableTier2DropList;
            Tier3 = run.availableTier3DropList;
            Void1 = run.availableVoidTier1DropList;
            Void2 = run.availableVoidTier2DropList;
            Void3 = run.availableVoidTier3DropList;
            Boss = run.availableBossDropList;
            Lunar = run.availableLunarItemDropList;
            Equipment = run.availableEquipmentDropList;
            LunarEquipment = run.availableLunarEquipmentDropList;
            Prefab = RoR2.LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/OptionPickup");
            Garbage = RoR2.LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/Tracers/TracerGolem");
            Log.Info("Storage initialized");
        }
    }
}