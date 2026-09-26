using System;
using UnityEngine;

[RequireComponent(typeof(DOHealth))]
public class ExplosiveObject : MonoBehaviour
{

    [Header("Blast")]
    [SerializeField] private float radius = 2f;
    [SerializeField] private float maxForce = 100f;
    [SerializeField] private float explosionDamage = 100f;
    [SerializeField] private float upwardBias = 0.3f;
    [SerializeField] private LayerMask affectedLayers = ~0;

    [Header("Presentation")]
    [SerializeField] private ParticleSystem explosionParticle;
    [SerializeField] private float shakeForce = 2f;
    [SerializeField] private float particleLifetime = 3f;

    private DOHealth _health;
    private bool _hasExploded;

    private void Awake() => _health = GetComponent<DOHealth>();
    private void OnEnable() => _health.OnDeath += Explode;
    private void OnDisable() => _health.OnDeath -= Explode;

    private void Update()
    {
        Debug.DrawLine(transform.position, transform.position + (Vector3.forward * radius), Color.red, 1);
    }

    private void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, radius, affectedLayers);
        Vector3 origin = transform.position;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            Vector3 toTarget = hit.bounds.center - origin;
            float distance = toTarget.magnitude;
            float falloff = Mathf.Clamp01(1f - distance / radius);
            if (falloff <= 0f) continue;

            // Damage first, and independently of whether the target can be pushed,
            // so static scenery still takes blast damage without moving.
            DOHealth targetHealth = hit.GetComponentInParent<DOHealth>();
            if (targetHealth)
            {
                targetHealth.TakeDamage(explosionDamage * falloff);
                if (targetHealth.IsDead()) targetHealth.Die();
            }

            Rigidbody body = hit.attachedRigidbody;
            if (!body || body.isKinematic) continue;

            Vector3 direction = distance > 0.001f ? toTarget / distance : Vector3.up;
            direction = (direction + Vector3.up * upwardBias).normalized;

            body.AddForceAtPosition(
                direction * (maxForce * falloff), hit.bounds.center, ForceMode.Impulse);
        }

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

        ExplosiveSoundManager sound = GetComponent<ExplosiveSoundManager>();
        if (sound) sound.PlayExplosionSound(1f);
    }

    
    
}
