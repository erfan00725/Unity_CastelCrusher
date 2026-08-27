using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class BuletLuncher : MonoBehaviour
{
    public BuletSpawner bs;
    
    public float cooldown = 0.5f;

    public GameObject catapultHead;

    private Projectile activeBulet;

    private Quaternion _initateCatapultHeadRotation = Quaternion.identity;
    
    private Quaternion _initateCatapultBodyRotation = Quaternion.identity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //activeBulet = bs.InstantiateBullet();

        if (catapultHead)
        {
            _initateCatapultHeadRotation = catapultHead.transform.localRotation;
            _initateCatapultBodyRotation = transform.localRotation;
            
            Debug.Log(_initateCatapultHeadRotation);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (activeBulet)
        {
            activeBulet.Shoot();
            StartCoroutine(DelaySpawn(cooldown));
        }
    }

    public void RotateCatapultHead(float zAngle)
    {
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
        catapultHead.transform.localRotation = _initateCatapultHeadRotation;
    }

    public void ResetCatapultBodyRotation()
    {
        transform.localRotation = _initateCatapultBodyRotation;
    }
    
    IEnumerator DelaySpawn(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        activeBulet = bs.InstantiateBullet();
    }
}
