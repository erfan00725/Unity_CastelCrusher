using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BulletSpawner : MonoBehaviour
{
    public ProjectileSelector projectileSelector;
    public AmmoManager ammoManager;

    
    private void OnEnable()
    {
        if (projectileSelector) projectileSelector.OnSelectionChanged += HandleSelectionChanged;
    }

    private void OnDisable()
    {
        if (projectileSelector) projectileSelector.OnSelectionChanged -= HandleSelectionChanged;
    }
    
    public Projectile InstantiateBullet()
    {
        SO_ProjectileType type = projectileSelector.SelectedType;

        bool hasAmmo = ammoManager.HasAmmo(type);
        
        if (type && !hasAmmo)
        {
            if (!projectileSelector.SelectNextWithAmmo(ammoManager.HasAmmo))
            {
                ammoManager.ChangeUiBulletCountText(0);
                return null;
            }
            type = projectileSelector.SelectedType;
        }

        if (!type || !hasAmmo)
        {
            ammoManager.ChangeUiBulletCountText(0);
            return null;
        }

        ammoManager.DecreaseAmmo(type);
        ammoManager.RefreshSelectedAmmoText();

        return Instantiate(type.prefab, transform.position, transform.rotation, transform);
    }

    private void HandleSelectionChanged(SO_ProjectileType type)
    {
        ammoManager.RefreshSelectedAmmoText();
    }
}
