using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace AutoCommandQueuePickup.CommandQueue
{
    public class CommandComponent : MonoBehaviour
    {
        public ItemTier _tier;
        public PickupIndex _pickupIndex;
        public Vector3 _position;
        public bool _isCommandArtifact;
        public String _name;

        public ItemTier GetTier() => _tier;
        public PickupIndex GetPickupIndex() => _pickupIndex;
        public Vector3 GetPosition() => _position;
        public bool GetIsCommandArtifact () => _isCommandArtifact;
        public String GetName() => _name;
        
        public void Awake()
        {
            this._tier = ItemTier.NoTier;
            this._pickupIndex = PickupIndex.none;
            this._position = Vector3.zero;
            this._isCommandArtifact = false;
            this._name = "";
        }

        public void Init (ItemTier tier, PickupIndex pickupIndex, Vector3 position, bool isCommandArtifact, String name)
        {
            this._tier = tier;
            this._pickupIndex = pickupIndex;
            this._position = position;
            this._isCommandArtifact = isCommandArtifact;
            this._name = name;
        }

        public void Reset()
        {
            this._tier = ItemTier.NoTier;
            this._pickupIndex = PickupIndex.none;
            this._position = Vector3.zero;
            this._isCommandArtifact = false;
            this._name = "";
        }

        //create to string method
        new public string ToString() => $"Tier: {_tier}, PickupIndex: {_pickupIndex}, Position: {_position}, IsCommandArtifact: {_isCommandArtifact}, Name: {_name}";
    }
}
