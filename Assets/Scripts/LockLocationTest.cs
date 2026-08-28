using UnityEngine;

public class LockLocationTest : MonoBehaviour
{

    public GameObject target;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
        Vector3 direction =  target.transform.position - transform.position;
        Quaternion rotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 10f);
        
        float horizental =  Input.GetAxis("Horizontal");
        float vertental =  Input.GetAxis("Vertical");
        
        float rotX = transform.rotation.eulerAngles.y;
        float rotZ = transform.rotation.eulerAngles.z;
        float rotY = transform.rotation.eulerAngles.y;
        
        Debug.Log(horizental);
        Debug.Log(vertental);

        if (horizental!=0)
        {
            target.transform.rotation =  new Quaternion(rotX + (horizental * Time.deltaTime), rotZ, rotY, 0);
        }

        if (vertental != 0)
        {
            target.transform.rotation =  new Quaternion(rotX, rotZ  + (vertental * Time.deltaTime), rotY, 0);
        }
        
    }
}
