using UnityEngine;

public class DO_ManageImpact : MonoBehaviour
{
    
    public float impactDamageMultiplier = 10f;
    
    private DO_Health _health;
    
    private DO_LowHealthRedTint _lowHealthRedTint;
    
    void Awake()
    {
        _health = GetComponent<DO_Health>();
        _lowHealthRedTint = GetComponent<DO_LowHealthRedTint>();
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        float currentHealth = _health.TakeDamage(collision.relativeVelocity.magnitude * impactDamageMultiplier);

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
