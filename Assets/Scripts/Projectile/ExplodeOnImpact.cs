using UnityEngine;

[RequireComponent(typeof(Projectile))]
public class ExplodeOnImpact : MonoBehaviour
{
    [Header("Blast")]
    [SerializeField] private BlastData blast = new BlastData
    {
        radius = 2.5f,
        maxForce = 150f,
        explosionDamage = 120f,
        upwardBias = 0.3f,
        affectedLayers = ~0
    };

    [Header("Presentation")]
    [SerializeField] private ParticleSystem explosionParticle;
    [SerializeField] private float shakeForce = 2f;
    [SerializeField] private float particleLifetime = 3f;
    [SerializeField] private float delayBeforeDestroy = 0.1f;

    private bool _hasExploded;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Start")) return;
        if (_hasExploded) return;
        _hasExploded = true;

        Explosion.TriggerBlast(transform.position, blast, transform);
        PlayEffects();

        Destroy(gameObject, delayBeforeDestroy);
    }

    private void PlayEffects()
    {
        if (explosionParticle)
        {
            ParticleSystem effect = Instantiate(explosionParticle, transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, particleLifetime);
        }

        CameraShakeManager shake = FindAnyObjectByType<CameraShakeManager>();
        if (shake) shake.Shake(shakeForce);

        ExplosionSoundManager sound = GetComponent<ExplosionSoundManager>();
        if (sound) sound.PlayExplosionSound(1f);
    }
}