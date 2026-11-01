using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 1.25f; // Tuned for zero foot-sliding with 1.03s walk cycle
    public float rotationSpeed = 14f;
    public float gravity = -9.81f;

    [Header("Auto Demo Mode (Toggle with Tab)")]
    public bool autoDemoMode = true;
    public Transform[] demoWaypoints;
    private int currentWaypointIndex = 0;
    private float demoPauseTimer = 0f;

    private CharacterController controller;
    private Animator animator;
    private Transform mainCameraTransform;
    private Vector3 verticalVelocity = Vector3.zero;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        var smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            smr.updateWhenOffscreen = true;
        }
    }

    void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        // Toggle auto demo with Tab
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            autoDemoMode = !autoDemoMode;
        }

        // Ground check and gravity
        if (controller.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        Vector3 moveInput = Vector3.zero;

        // Check manual input first
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (h != 0 || v != 0)
        {
            // Any manual input immediately gives control to the player
            autoDemoMode = false;

            if (mainCameraTransform != null)
            {
                Vector3 camFwd = mainCameraTransform.forward;
                camFwd.y = 0;
                camFwd.Normalize();

                Vector3 camRight = mainCameraTransform.right;
                camRight.y = 0;
                camRight.Normalize();

                moveInput = (camFwd * v + camRight * h).normalized;
            }
            else
            {
                moveInput = new Vector3(h, 0, v).normalized;
            }
        }
        else if (autoDemoMode && demoWaypoints != null && demoWaypoints.Length > 0)
        {
            // Automated patrol between waypoints
            if (demoPauseTimer > 0f)
            {
                demoPauseTimer -= Time.deltaTime;
                moveInput = Vector3.zero;
            }
            else
            {
                Transform targetWp = demoWaypoints[currentWaypointIndex];
                Vector3 toTarget = targetWp.position - transform.position;
                toTarget.y = 0f;

                if (toTarget.magnitude < 0.25f)
                {
                    currentWaypointIndex = (currentWaypointIndex + 1) % demoWaypoints.Length;
                    demoPauseTimer = 0.35f;
                    moveInput = Vector3.zero;
                }
                else
                {
                    moveInput = toTarget.normalized;
                }
            }
        }

        // Apply Horizontal Movement
        float targetSpeed = moveInput.magnitude * moveSpeed;
        if (moveInput.magnitude > 0.05f)
        {
            controller.Move(moveInput * moveSpeed * Time.deltaTime);

            // Rotate towards movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveInput);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Apply Gravity
        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);

        // Update Animator
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, targetSpeed);
        }
    }
}
