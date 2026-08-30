using System;
using System.Collections;
using UnityEngine;

public class DO_Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public float delayBeforeDestroy = 1f;
    
    private float _currentHealth;

    private void Start()
    {
        _currentHealth = maxHealth;
    }
    
    public float TakeDamage(float damage)
    {
        _currentHealth -= damage;
        return _currentHealth;
    }
    
    public float GetCurrentHealth()
    {
        return _currentHealth;
    }
    
    public bool IsDead()
    {
        return _currentHealth <= 0;
    }
    
    public void Die()
    {
        StartCoroutine(DieRoutine());
    }
    
    IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(delayBeforeDestroy);
        Destroy(gameObject);
    }
}
