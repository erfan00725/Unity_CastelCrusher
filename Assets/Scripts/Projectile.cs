using System;
using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float baseForcePower = 1500;
    public float delayBeforeDestroy = 0.5f;

    private Rigidbody _rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Start"))
        {
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
        _rb.AddForce(new Vector3(-1,1,0) * (baseForcePower * force * Time.deltaTime), ForceMode.Impulse);
    }
}
