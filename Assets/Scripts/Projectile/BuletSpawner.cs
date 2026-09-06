using UnityEngine;
using UnityEngine.Serialization;

public class BuletSpawner : MonoBehaviour
{
    public Projectile projectilePrefab;

    public Projectile InstantiateBullet()
    {
        return Instantiate(projectilePrefab, transform.position, transform.rotation, gameObject.transform);
    }
}
