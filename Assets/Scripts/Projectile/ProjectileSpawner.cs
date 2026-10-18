using UnityEngine;

public class ProjectileSpawner : MonoBehaviour
{
    public ProjectileSelector projectileSelector;
    public AmmoManager ammoManager;

    private Projectile _currentProjectile;
    private bool _isSwitching;
    
    private Projectile InstantiateBullet(SO_ProjectileType pType = null)
    {
        SO_ProjectileType type = pType ?? projectileSelector.SelectedType;
        
        bool hasAmmo = ammoManager.HasAmmo(type);
        
        if (type && !hasAmmo)
        {
            if (!projectileSelector.SelectNextWithAmmo(ammoManager.HasAmmo))
            {
                return null;
            }
            type = projectileSelector.SelectedType;
        }

        if (!type || !type.prefab)
        {
            return null;
        }

        Projectile currentProjectile = Instantiate(type.prefab, transform.position, transform.rotation, transform);
        currentProjectile.ammoManager = ammoManager;

        return currentProjectile;
    }
    
    public Projectile InstantiateBulletSafe(SO_ProjectileType pType = null)
    {
        // Guard against re-entrant calls: InstantiateBullet's ammo fallback can
        // re-select a type and re-raise OnSelectionChanged synchronously.
        if (_isSwitching)
        {
            return _currentProjectile;
        }

        _isSwitching = true;

        // Replace the loaded projectile only if it hasn't been launched yet.
        // Projectile.Shoot() unparents the instance, so launched bullets are no
        // longer children of this spawner and must not be destroyed here.
        if (_currentProjectile && _currentProjectile.transform.parent == transform)
        {
            Destroy(_currentProjectile.gameObject);
        }

        _currentProjectile = InstantiateBullet();

        _isSwitching = false;
        
        return _currentProjectile;
    }
}
