using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public UIManager uiManager;
    
    private int _score;
    
    public static ScoreManager I { get; private set; }

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
        Debug.Log("Score: " + _score);

        if (uiManager)
        {
            uiManager.SetScoreText(_score);
        }
        
        return _score;
    }
    
    public float GetScore() { return _score; }
    
    public void ResetScore() { _score = 0; }
}
