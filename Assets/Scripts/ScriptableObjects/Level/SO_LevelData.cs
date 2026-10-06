using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/Level Data")]
public class SO_LevelData : ScriptableObject
{
    public List<ProjectileLoadout> projectileLoadouts = new List<ProjectileLoadout>();
}

[Serializable]
public class ProjectileLoadout
{
    public SO_ProjectileType projectileType;
    public int ammoCount;
}