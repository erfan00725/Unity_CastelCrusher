using System;
using System.Collections;
using UnityEngine;

public class DOHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float delayBeforeDestroy = 1f;
    
    public int deathScoreValue = 100;
    public ScorePopup scorePopupPrefab;
    
    private float _currentHealth;
    
    private bool _isDead = false;

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
        if (_isDead) return;
        StartCoroutine(DieRoutine());
    }
    
    IEnumerator DieRoutine()
    {
        if (_isDead) yield return null;
        
        _isDead = true;
        
        ScoreManager.I.AddScore(deathScoreValue);
        
        if (scorePopupPrefab)
        {
            ScorePopup popup = Instantiate(scorePopupPrefab, transform.position, Quaternion.identity);
            popup.Show(deathScoreValue);
        }
        
        yield return new WaitForSeconds(delayBeforeDestroy);
        Destroy(gameObject);
    }
}
