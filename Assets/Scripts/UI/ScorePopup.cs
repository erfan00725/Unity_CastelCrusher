using System.Collections;
using TMPro;
using UnityEngine;

public class ScorePopup : MonoBehaviour
{
    [Header("Text")]
    public string format = "+{0}";

    [Header("Motion")]
    public float floatHeight = 1.5f;
    public float duration = 1f;

    [Header("Fade")]
    [Range(0f, 1f)]
    public float fadeStartFraction = 0.5f;

    private TMP_Text _text;
    private Color _startColor;
    
    static private Camera _sMainCamera;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    public void Show(int points)
    {
        _text.text = string.Format(format, points);
        _startColor = _text.color;

        if (!_sMainCamera) _sMainCamera = Camera.main;
        
        if (_sMainCamera)
        {
            transform.rotation = _sMainCamera.transform.rotation;
        }
        
        StartCoroutine(PopupRoutine());
    }

    IEnumerator PopupRoutine()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * floatHeight;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.position = Vector3.Lerp(startPos, endPos, t);

            if (t >= fadeStartFraction)
            {
                float fadeT = (t - fadeStartFraction) / (1f - fadeStartFraction);
                _text.color = Color.Lerp(_startColor, Color.clear, fadeT);
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}