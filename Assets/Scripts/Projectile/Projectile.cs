using System;
using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float baseForcePower = 1500;
    public float delayBeforeDestroy = 0.5f;
    public float shakeIntensity = 1f;
    public float shakeDuration = 0.2f;
    
    private Rigidbody _rb;

    private ProjectileSoundManager _soundManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;

        _soundManager = GetComponent<ProjectileSoundManager>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Start"))
        {
            if (_soundManager)
            {
                _soundManager.PlayHitSound(collision.relativeVelocity.magnitude);
            }
            StartCoroutine(DelayDestroy(delayBeforeDestroy));
        }
    }

    IEnumerator DelayDestroy(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        Destroy(gameObject);
    }

    public void Shoot(float force = 1)
    {
        _rb.isKinematic = false;
        transform.SetParent(null);
        _rb.AddRelativeForce(new Vector3(-1,1,0) * (baseForcePower * force * Time.deltaTime), ForceMode.Impulse);
        
        CameraShakeManager cameraShakeManager = FindAnyObjectByType<CameraShakeManager>();
        
        if (cameraShakeManager)
        {
            cameraShakeManager.Shake(force * shakeIntensity, shakeDuration);
        }
    }
}
