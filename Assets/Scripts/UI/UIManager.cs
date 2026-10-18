using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField]
    private TMP_Text scoreText;
    
    public void SetScoreText(int score)
    {
        scoreText.text = score.ToString();
    }
    
}
