using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraShakeManager : MonoBehaviour
{
    [Header("Impulse")]
    [Tooltip("Kick direction in screen space, normalized automatically. (0,-1) = recoil downward.")]
    public Vector2 impulseDirection = new Vector2(-1f, -2f);

    [Tooltip("Multiplier on the incoming force. Higher = harder kick per shot.")]
    public float impulseStrength = 15f;

    [Header("Spring")]
    [Tooltip("How snappy the return to rest is. Higher = faster, tighter spring.")]
    public float stiffness = 120f;

    [Tooltip("How quickly the wobble calms. Slightly under-damped gives one soft overshoot.")]
    public float damping = 14f;

    [Tooltip("Camera roll tilt (Z rotation, degrees) applied per unit of force. 0 = off.")]
    public float rollStrength = 0.5f;

    [Header("Safety")]
    [Tooltip("Max distance from rest the camera can be pushed. Prevents extreme forces flinging the view.")]
    public float maxOffset = 0.6f;

    private Camera _cam;
    private Vector3 _restLocalPosition;
    private Quaternion _restLocalRotation;

    private Vector2 _offset;
    private Vector2 _velocity;
    private float _roll;
    private float _rollVelocity;

    private bool _settled = true;

    void Awake() => _cam = GetComponent<Camera>();

    void Start()
    {
        RecordRest();
    }

    // Call from anywhere: CameraShakeManager.Shake(force);
    public void Shake(float force)
    {
        if (force <= 0f) return;

        Vector2 dir = impulseDirection.sqrMagnitude > 0.0001f
            ? impulseDirection.normalized
            : Vector2.down;

        _velocity += dir * (force * impulseStrength);
        _rollVelocity += force * rollStrength;
        _settled = false;
    }

    void LateUpdate()
    {
        if (!_cam) return;

        // While calm, continuously note where "home" is so external camera moves are respected.
        if (_settled)
        {
            RecordRest();
            return;
        }

        float dt = Time.deltaTime;

        // Semi-implicit Euler integration of two damped springs (position + roll).
        Vector2 accel = -stiffness * _offset - damping * _velocity;
        _velocity += accel * dt;
        _offset += _velocity * dt;
        _offset = Vector2.ClampMagnitude(_offset, maxOffset);

        float rollAccel = -stiffness * _roll - damping * _rollVelocity;
        _rollVelocity += rollAccel * dt;
        _roll += _rollVelocity * dt;

        if (IsSleeping())
        {
            _offset = Vector2.zero;
            _velocity = Vector2.zero;
            _roll = 0f;
            _rollVelocity = 0f;
            _settled = true;
            Apply(Vector2.zero, 0f);
            return;
        }

        Apply(_offset, _roll);
    }

    private void Apply(Vector2 offset, float roll)
    {
        _cam.transform.localPosition = _restLocalPosition + new Vector3(offset.x, offset.y, 0f);
        _cam.transform.localRotation = _restLocalRotation * Quaternion.Euler(0f, 0f, roll);
    }

    private void RecordRest()
    {
        _restLocalPosition = _cam.transform.localPosition;
        _restLocalRotation = _cam.transform.localRotation;
    }

    private bool IsSleeping()
    {
        return _offset.sqrMagnitude < 0.0001f
            && _velocity.sqrMagnitude < 0.0001f
            && Mathf.Abs(_roll) < 0.01f
            && Mathf.Abs(_rollVelocity) < 0.01f;
    }
}
