using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform camTransform;

    void Start()
    {
        camTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        // Option A: Perfect Camera Alignment (Best for Top-Down)
        transform.rotation = camTransform.rotation;        
        // Option B: Look at Camera (If your camera is very close/perspective)
        // transform.LookAt(transform.position + camTransform.rotation * Vector3.forward, camTransform.rotation * Vector3.up);
    }
}