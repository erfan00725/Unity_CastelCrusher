using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BulletSpawner : MonoBehaviour
{
    public SO_LevelData levelData;
    public ProjectileSelector projectileSelector;
    public UIManager uiManager;
    
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
    
    private void OnEnable()
    {
        if (projectileSelector) projectileSelector.OnSelectionChanged += HandleSelectionChanged;
    }

    private void OnDisable()
    {
        if (projectileSelector) projectileSelector.OnSelectionChanged -= HandleSelectionChanged;
    }
    
    public void InitializeAmmo(List<ProjectileLoadout> loadout)
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
        RefreshSelectedAmmoText();
    }
    
    public bool HasAmmo(SO_ProjectileType type)
    {
        return type && _ammo.TryGetValue(type, out int remaining) && remaining > 0;
    }

    public int GetRemaining(SO_ProjectileType type)
    {
        return type && _ammo.TryGetValue(type, out int remaining) ? remaining : 0;
    }
    
    public Projectile InstantiateBullet()
    {
        SO_ProjectileType type = projectileSelector.SelectedType;

        if (type && !HasAmmo(type))
        {
            if (!projectileSelector.SelectNextWithAmmo(HasAmmo))
            {
                ChangeUiBulletCountText(0);
                return null;
            }
            type = projectileSelector.SelectedType;
        }

        if (!type || !HasAmmo(type))
        {
            ChangeUiBulletCountText(0);
            return null;
        }

        _ammo[type]--;
        OnAmmoChanged?.Invoke(type, _ammo[type]);
        RefreshSelectedAmmoText();

        return Instantiate(type.prefab, transform.position, transform.rotation, transform);
    }

    private void HandleSelectionChanged(SO_ProjectileType type)
    {
        RefreshSelectedAmmoText();
    }

    private void RefreshSelectedAmmoText()
    {
        ChangeUiBulletCountText(GetRemaining(projectileSelector.SelectedType));
    }

    private void ChangeUiBulletCountText(int newValue)
    {
            if (uiManager) uiManager.SetBulletCountText(newValue);
    }
}
