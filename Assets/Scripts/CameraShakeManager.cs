using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraShakeManager : MonoBehaviour
{
    [Tooltip("Max displacement in world units (0.5 ≈ half a unit, tune per scene scale).")]
    public float intensity = 0.5f;

    [Tooltip("How long one shake lasts once triggered.")]
    public float duration = 0.4f;

    [Tooltip("Higher = more jittery. 20 is Perlin-like, 50+ is raw noise.")]
    public float frequency = 20f;

    private Camera _cam;
    private float _shakeAmount;
    private float _shakeTime;
    
    private Vector3 _originalPosition;

    void Awake() => _cam = GetComponent<Camera>();

    // Call from anywhere: FindObjectOfType<CameraShake>().Shake(1.2f);
    public void Shake(float force, float durationOverride = 0f)
    {
        if (force <= 0f) return;
        _shakeAmount = Mathf.Max(_shakeAmount, force);
        _shakeTime   = Mathf.Max(_shakeTime, durationOverride > 0f ? durationOverride : duration);
    }
    
    private void Start()
    {
        _originalPosition = _cam.transform.localPosition;
    }

    void LateUpdate()
    {
        if (_shakeAmount <= 0f || !_cam) return;

        _shakeTime -= Time.deltaTime;
        if (_shakeTime <= 0f)
        {
            _cam.transform.localPosition = _originalPosition;
            _shakeAmount = 0f;
            return;
        }

        float t = 1f - _shakeTime / Mathf.Max(duration, 0.0001f);   // 0→1 over the window
        float decay = Mathf.Lerp(_shakeAmount, 0f, t);               // linear decay; swap to AnimationCurve later if you want eased

        // 2D screen-plane shake only (no Z — cameras rarely want that)
        Vector3 offset = new Vector3(
            0f,
            (Mathf.PerlinNoise(0f, Time.time * frequency) - 0.5f),
            (Mathf.PerlinNoise(Time.time * frequency, 0f) - 0.5f)
        ) * decay;

        _cam.transform.localPosition += offset;   // additive: composes with any existing camera motion
    }
}