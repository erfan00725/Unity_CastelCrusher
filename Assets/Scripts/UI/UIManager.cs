using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    
    [SerializeField]
    private TMP_Text bulletsCountText;
    [SerializeField]
    private TMP_Text scoreText;

    public void SetBulletCountText(int bulletsCount)
    {
        bulletsCountText.text = bulletsCount.ToString();
    }
    
    public void SetScoreText(int score)
    {
        scoreText.text = score.ToString();
    }
    
}
