using UnityEngine;

[System.Serializable]
public struct BlastData
{
    public float radius;
    public float maxForce;
    public float explosionDamage;
    public float upwardBias;
    public LayerMask affectedLayers;
}

public static class Explosion
{
    public static void TriggerBlast(Vector3 origin, BlastData data, Transform ignoreRoot = null)
    {
        Collider[] hits = Physics.OverlapSphere(origin, data.radius, data.affectedLayers);

        foreach (Collider hit in hits)
        {
            if (ignoreRoot && (hit.transform == ignoreRoot || hit.transform.IsChildOf(ignoreRoot))) continue;

            Vector3 toTarget = hit.bounds.center - origin;
            float distance = toTarget.magnitude;
            float falloff = Mathf.Clamp01(1f - distance / data.radius);
            if (falloff <= 0f) continue;

            DOHealth targetHealth = hit.GetComponentInParent<DOHealth>();
            if (targetHealth)
            {
                targetHealth.TakeDamage(data.explosionDamage * falloff);
                if (targetHealth.IsDead()) targetHealth.Die();
            }

            Rigidbody body = hit.attachedRigidbody;
            if (!body || body.isKinematic) continue;

            Vector3 direction = distance > 0.001f ? toTarget / distance : Vector3.up;
            direction = (direction + Vector3.up * data.upwardBias).normalized;

            body.AddForceAtPosition(
                direction * (data.maxForce * falloff), hit.bounds.center, ForceMode.Impulse);
        }
    }
}