using UnityEngine;

public class DOManageImpact : MonoBehaviour
{
    
    public float impactDamageMultiplier = 10f;
    
    private DOHealth _health;
    
    private DOLowHealthRedTint _lowHealthRedTint;
    
    private DOSoundManager _soundManager;
    
    void Awake()
    {
        _health = GetComponent<DOHealth>();
        _lowHealthRedTint = GetComponent<DOLowHealthRedTint>();
        _soundManager = GetComponent<DOSoundManager>();
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        float collisionVelocityMagnitude = collision.relativeVelocity.magnitude;
        
        float currentHealth = _health.TakeDamage(collisionVelocityMagnitude * impactDamageMultiplier);
        
        if (_soundManager)
        {
            _soundManager.PlayHitSound(collisionVelocityMagnitude);
        }

        if (_lowHealthRedTint)
        {
            _lowHealthRedTint.UpdateHealthColor(currentHealth, _health.maxHealth);
        }

        if (_health.IsDead())
        {
            _health.Die();
        }
    }
}
