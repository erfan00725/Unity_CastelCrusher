using System;
using System.Collections.Generic;
using UnityEngine;

public enum AmmoMode { Shared, PerCatapult }

[CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/Level Data")]
public class SO_LevelData : ScriptableObject
{
    public List<ProjectileLoadout> projectileLoadouts = new List<ProjectileLoadout>();
    
    [Tooltip("Shared = all catapults fire from one pool. PerCatapult = each catapult gets a full copy of the loadout.")]
    public AmmoMode ammoMode = AmmoMode.Shared;

}

[Serializable]
public class ProjectileLoadout
{
    public SO_ProjectileType projectileType;
    public int ammoCount;
}