using System;
using UnityEngine;
using UnityEngine.Serialization;

public class BuletSpawner : MonoBehaviour
{
    public Projectile projectilePrefab;
    
    public int maxBulletsCount = 10;
    
    private int _currentBulletsCount = 0;

    private void Awake()
    {
        _currentBulletsCount = maxBulletsCount;
    }

    public Projectile InstantiateBullet()
    {
        if (_currentBulletsCount > 0)
        {
            _currentBulletsCount--;
            return Instantiate(projectilePrefab, transform.position, transform.rotation, gameObject.transform);
        }
        Debug.LogWarning("No bullets available");
        return null;
    }
}
