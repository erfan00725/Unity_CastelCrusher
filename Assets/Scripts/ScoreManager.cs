using System.Collections.Generic;
using Core;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public UIManager uiManager;
    
    private int _score;
    
    public static ScoreManager I { get; private set; }
    
    [Header("Leftover shots")]
    public float scorePerUnusedShot = 100f;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        
        if (uiManager)
        {
            uiManager.SetScoreText(_score);
        }
    }

    public float AddScore(int points)
    {
        _score += points;

        if (uiManager)
        {
            uiManager.SetScoreText(_score);
        }
        
        return _score;
    }
    
    public void AddLeftoverShotBonus(IReadOnlyList<(int remaining, float multiplier)> entries)
    {
        int bonus = Mathf.RoundToInt(LeftoverScoreCalculator.Compute(entries, scorePerUnusedShot));
        if (bonus > 0) AddScore(bonus);
    }
    
    public float GetScore() { return _score; }
    
    public void ResetScore() { _score = 0; }
}
