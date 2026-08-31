using UnityEngine;

public class DO_ManageImpact : MonoBehaviour
{
    
    public float impactDamageMultiplier = 10f;
    
    private DO_Health _health;

    private ParticleSystem _impactParticle;

    private DO_LowHealthRedTint _lowHealthRedTint;
    
    void Awake()
    {
        _health = GetComponent<DO_Health>();
        _impactParticle = GetComponent<ParticleSystem>();
        _lowHealthRedTint = GetComponent<DO_LowHealthRedTint>();
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        float damageToTake = collision.relativeVelocity.magnitude * impactDamageMultiplier;
        
        
        if (_impactParticle && damageToTake > impactDamageMultiplier)
        {
            ParticleSystem.ShapeModule shape = _impactParticle.shape;
            
            shape.position = transform.InverseTransformPoint(collision.GetContact(0).point);
            
            _impactParticle.Play();
            
        }
        
        
        float currentHealth = _health.TakeDamage(collision.relativeVelocity.magnitude * impactDamageMultiplier);

        if (_lowHealthRedTint)
        {
            _lowHealthRedTint.UpdateHealthColor(currentHealth, _health.maxHealth);
        }

        if (_health.IsDead())
        {
            if (_impactParticle)
            {
                _impactParticle.Play();
            }
            _health.Die();
        }
    }
}
