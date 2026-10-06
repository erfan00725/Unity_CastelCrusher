using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileType", menuName = "Scriptable Objects/Projectile Type")]
public class SO_ProjectileType : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public int ammoCount = 5;
    public Projectile prefab;
    [Min(0f)] public float scoreMultiplier = 1f;
}