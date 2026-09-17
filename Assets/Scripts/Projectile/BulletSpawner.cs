using System;
using UnityEngine;
using UnityEngine.Serialization;

public class BulletSpawner : MonoBehaviour
{
    public Projectile projectilePrefab;
    
    public UIManager uiManager;
    
    public int maxBulletsCount = 10;
    
    private int _currentBulletsCount = 0;

    private void Awake()
    {
        _currentBulletsCount = maxBulletsCount;
        
        if (uiManager)
        {
            uiManager.SetBulletCountText(_currentBulletsCount);
        }
    }

    public Projectile InstantiateBullet()
    {
        if (_currentBulletsCount > 0)
        {
            if (uiManager)
            {
                uiManager.SetBulletCountText(_currentBulletsCount);
            }
            _currentBulletsCount--;
            
            
            return Instantiate(projectilePrefab, transform.position, transform.rotation, gameObject.transform);
        }
        Debug.LogWarning("No bullets available");
        if (uiManager)
        {
            uiManager.SetBulletCountText(0);
        }
        return null;
    }
}
