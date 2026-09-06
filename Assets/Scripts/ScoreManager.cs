using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private int _score;
    
    public static ScoreManager I { get; private set; }

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    public float AddScore(int points)
    {
        _score += points;
        Debug.Log("Score: " + _score);
        return _score;
    }
    
    public float GetScore() { return _score; }
    
    public void ResetScore() { _score = 0; }
}
