using UnityEngine;

public class IsometricCameraFollow : MonoBehaviour
{
    [Header("Target to Follow")]
    public Transform target;

    [Header("Isometric Camera Offset")]
    public Vector3 offset = new Vector3(-6f, 8f, -6f);
    public float smoothSpeed = 5f;

    void Start()
    {
        // Align initial rotation towards the target
        if (target != null)
        {
            transform.position = target.position + offset;
            transform.LookAt(target.position + Vector3.up * 1f);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // Maintain isometric viewing angle
        Vector3 lookTarget = target.position + Vector3.up * 1f;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookTarget - transform.position), smoothSpeed * Time.deltaTime);
    }
}
