using System;
using UnityEngine;

public class ProjectileParticleManager : MonoBehaviour
{
    public ParticleSystem impactParticle;
    
    public float impactThreshold = 5f;

    private void OnCollisionEnter(Collision collision)
    {
        if (impactParticle && collision.relativeVelocity.magnitude > impactThreshold)
        {
            ContactPoint contact = collision.GetContact(0);

            Quaternion rotation = Quaternion.FromToRotation(
                Vector3.up,
                contact.normal
            );

            ParticleSystem effect = Instantiate(
                impactParticle,
                contact.point,
                rotation
            );

            effect.Play();
            
        }

    }
}
