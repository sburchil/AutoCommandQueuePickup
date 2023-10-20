using System.Collections.Generic;
using RoR2;
using RoR2.Artifacts;
using UnityEngine;
using UnityEngine.Networking;

public class CharacterMasterManager : MonoBehaviour
{
    public static Dictionary<uint, CharacterMaster> playerCharacterMasters = new ();
}