using UnityEngine;
using UnityEngine.Serialization;

public class BuletSpawner : MonoBehaviour
{
    public Projectile projectilePrefab;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public Projectile InstantiateBullet()
    {
        return Instantiate(projectilePrefab, transform.position, transform.rotation, gameObject.transform);
    }
}
