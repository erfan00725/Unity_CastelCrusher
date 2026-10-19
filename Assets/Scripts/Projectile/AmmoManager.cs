using System;
using UnityEngine;
using System.Collections.Generic;

public class AmmoManager : MonoBehaviour
{
    public ProjectileSelector projectileSelector;
    public SO_LevelData levelData;

    public event Action<SO_ProjectileType, int> OnAmmoChanged;
    
    private readonly Dictionary<SO_ProjectileType, int> _ammo = new Dictionary<SO_ProjectileType, int>();
    
    private void Awake()
    {
        List<ProjectileLoadout> loadout;
        if (levelData && levelData.projectileLoadouts.Count > 0)
        {
            loadout = levelData.projectileLoadouts;
        }
        else
        {
            loadout = new List<ProjectileLoadout>();
            foreach (SO_ProjectileType type in projectileSelector.availableTypes)
            {
                loadout.Add(new ProjectileLoadout { projectileType = type, ammoCount = type.ammoCount });
            }
        }
        
        InitializeAmmo(loadout);
    }
    
    private void InitializeAmmo(List<ProjectileLoadout> loadout)
    {
        _ammo.Clear();

        List<SO_ProjectileType> types = new List<SO_ProjectileType>();
        foreach (ProjectileLoadout entry in loadout)
        {
            if (!entry.projectileType) continue;
            types.Add(entry.projectileType);
            _ammo[entry.projectileType] = Mathf.Max(0, entry.ammoCount);
        }

        projectileSelector.Initialize(types);
    }

    public int DecreaseAmmo(SO_ProjectileType type)
    {
        int newAmmo = --_ammo[type];
        OnAmmoChanged?.Invoke(type, newAmmo);
        return _ammo[type];
    }
    
    public int DecreaseCurrentAmmo()
    {
        SO_ProjectileType type = projectileSelector.SelectedType;
        return DecreaseAmmo(type);
    }
    
    public int GetCurrentAmmoCount()
    {
        return _ammo[projectileSelector.SelectedType];
    }
    
    public bool HasAmmo(SO_ProjectileType type)
    {
        return type && _ammo.TryGetValue(type, out int remaining) && remaining > 0;
    }
    
    public int GetRemaining(SO_ProjectileType type)
    {
        return type && _ammo.TryGetValue(type, out int remaining) ? remaining : 0;
    }
}
