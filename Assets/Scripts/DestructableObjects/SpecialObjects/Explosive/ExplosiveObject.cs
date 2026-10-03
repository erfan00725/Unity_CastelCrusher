using UnityEngine;

[RequireComponent(typeof(DOHealth))]
public class ExplosiveObject : MonoBehaviour
{
    [SerializeField]
    private BlastData blastData = new BlastData
    {
        radius = 2f,
        maxForce = 100f,
        explosionDamage = 100f,
        upwardBias = 0.3f,
        affectedLayers = ~0
    };

    [Header("Presentation")]
    [SerializeField] private ParticleSystem explosionParticle;
    [SerializeField] private float shakeForce = 2f;
    [SerializeField] private float particleLifetime = 3f;

    private DOHealth _health;
    private bool _hasExploded;

    private void Awake() => _health = GetComponent<DOHealth>();
    private void OnEnable() => _health.OnDeath += Explode;
    private void OnDisable() => _health.OnDeath -= Explode;

    private void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;
        
        Vector3 origin = transform.position;
        
        Explosion.TriggerBlast(origin, blastData, transform);

        PlayEffects(origin);
    }
    
    private void PlayEffects(Vector3 origin)
    {
        if (explosionParticle)
        {
            ParticleSystem effect = Instantiate(explosionParticle, origin, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, particleLifetime);
        }

        CameraShakeManager shake = FindAnyObjectByType<CameraShakeManager>();
        if (shake) shake.Shake(shakeForce);

        ExplosionSoundManager sound = GetComponent<ExplosionSoundManager>();
        if (sound) sound.PlayExplosionSound(1f);
    }

    
    
}
