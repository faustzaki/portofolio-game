using UnityEngine;

// Kamera sudut ruangan yang mengikuti pemain.
// - offset tetap relatif ke pemain (arah pandang isometrik dari satu sudut)
// - posisi diklem di dalam ruangan supaya tidak nembus dinding
public class IsometricCameraFollow : MonoBehaviour
{
    [Header("Target to Follow")]
    public Transform target;

    [Header("Isometric Camera Offset (relatif ke pemain)")]
    public Vector3 offset = new Vector3(-2.45f, 1.90f, 2.45f);
    public float smoothSpeed = 4f;

    [Header("Look Height")]
    public float lookHeight = 0.95f;

    [Header("Room Clamp (biar kamera gak keluar kamar)")]
    public bool clampToRoom = true;
    public Vector3 minBounds = new Vector3(-2.80f, 1.30f, -2.80f);
    public Vector3 maxBounds = new Vector3(2.80f, 1.80f, 2.80f);

    private void Start()
    {
        Apply(true);
    }

    private void LateUpdate()
    {
        Apply(false);
    }

    private void Apply(bool instant)
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        if (clampToRoom)
        {
            desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
            desired.y = Mathf.Clamp(desired.y, minBounds.y, maxBounds.y);
            desired.z = Mathf.Clamp(desired.z, minBounds.z, maxBounds.z);
        }

        if (instant)
        {
            transform.position = desired;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        }

        Vector3 lookTarget = target.position + Vector3.up * lookHeight;
        Quaternion want = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = instant ? want : Quaternion.Slerp(transform.rotation, want, smoothSpeed * Time.deltaTime);
    }
}
