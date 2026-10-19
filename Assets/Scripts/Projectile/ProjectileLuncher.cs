using System.Collections;
using UnityEngine;

public class ProjectileLuncher : MonoBehaviour
{
    public ProjectileSpawner projectileSpawner;
    
    public float cooldown = 0.5f;

    // Seconds the catapult head takes to return to its rest rotation
    public float headResetDuration = 0.3f;
    
    public SO_AudioConfigBase audioConfig;
    
    public AudioSource luncherReleaseAudio;

    public GameObject catapultHead;

    public ProjectileSelector projectileSelector;

    private Projectile _activeProjectile;

    private Quaternion _initiateCatapultHeadRotation = Quaternion.identity;
    
    private Quaternion _initiateCatapultBodyRotation = Quaternion.identity;

    private Coroutine _headResetRoutine;

    private bool _isOnDelay = false;
    private bool _isCatapultMoving = false;
    
    
    private void OnEnable()
    {
        if (projectileSelector) projectileSelector.OnSelectionChanged += HandleSelectionChanged;
    }

    private void OnDisable()
    {
        if (projectileSelector) projectileSelector.OnSelectionChanged -= HandleSelectionChanged;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (catapultHead)
        {
            _initiateCatapultHeadRotation = catapultHead.transform.localRotation;
            _initiateCatapultBodyRotation = transform.localRotation;
        }
    }
    
    public void RotateCatapultHead(float zAngle)
    {
        // A new drag takes over: cancel the return-to-rest animation so it doesn't fight the input
        if (_headResetRoutine != null)
        {
            StopCoroutine(_headResetRoutine);
            _headResetRoutine = null;
        }

        _isCatapultMoving = true;
        float xAngle = catapultHead.transform.localRotation.x;
        float  yAngle = catapultHead.transform.localRotation.y;
        catapultHead.transform.localRotation = Quaternion.Euler(xAngle, yAngle, zAngle);
    }

    public void RotateCatapultBody(float yAngle)
    {
        float yRot = Mathf.Clamp(yAngle, -60, 60);
        
        float xRot = transform.rotation.eulerAngles.x;
        float zRot = transform.rotation.eulerAngles.z;
        
        transform.rotation = Quaternion.Euler(xRot, yRot, zRot);
    }

    public void ResetCatapultHeadRotation()
    {
        _isCatapultMoving = false;
        if (_activeProjectile)
        {
            float mag = 1 - (Mathf.Clamp(catapultHead.transform.localRotation.eulerAngles.magnitude, 0, 60) / 60);
            if (mag > 0.1f)
            {
                if (luncherReleaseAudio && audioConfig)
                {
                    audioConfig.Play(luncherReleaseAudio, mag);
                }
                
                _activeProjectile.Shoot(mag);
                StartCoroutine(DelaySpawn(cooldown));
            }
        }

        if (_headResetRoutine != null)
        {
            StopCoroutine(_headResetRoutine);
        }
        _headResetRoutine = StartCoroutine(ResetHeadRotationRoutine());
    }

    // Linearly rotates the head from its current rotation back to the rest rotation
    private IEnumerator ResetHeadRotationRoutine()
    {
        Quaternion startRotation = catapultHead.transform.localRotation;
        float elapsed = 0f;

        while (elapsed < headResetDuration)
        {
            elapsed += Time.deltaTime;
            // Linear t in [0, 1]: the same amount of rotation every second
            float t = Mathf.Clamp01(elapsed / headResetDuration);
            catapultHead.transform.localRotation = Quaternion.Slerp(startRotation, _initiateCatapultHeadRotation, t);
            yield return null;
        }

        // Snap to the exact target so no rounding residue is left behind
        catapultHead.transform.localRotation = _initiateCatapultHeadRotation;
        _headResetRoutine = null;
    }

    public void ResetCatapultBodyRotation()
    {
        transform.localRotation = _initiateCatapultBodyRotation;
    }
    
    IEnumerator DelaySpawn(float delayTime)
    {
        _isOnDelay = true;
        yield return new WaitForSeconds(delayTime);
        _isOnDelay = false;
        _activeProjectile = projectileSpawner.InstantiateBulletSafe();
    }
    
    private void HandleSelectionChanged(SO_ProjectileType type)
    {
        // also called in start of the script
        if (_isOnDelay || _isCatapultMoving) return;
        _activeProjectile = projectileSpawner.InstantiateBulletSafe(type);
    }
}
