using UnityEngine;

public class DO_ManageImpact : MonoBehaviour
{
    
    public float impactDamageMultiplier = 10f;
    
    private DO_Health _health;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _health = GetComponent<DO_Health>();
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        float currentHealth = _health.TakeDamage(collision.relativeVelocity.magnitude * impactDamageMultiplier);

        Debug.Log("Current Health: " + currentHealth);
        
        if (_health.IsDead())
        {
            _health.Die();
        }
    }
}
