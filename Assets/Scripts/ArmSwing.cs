using UnityEngine;

// Procedural arm swing via OnAnimatorIK
// Hand IK targets & elbow hints oscillate in anti-phase with the Walk cycle,
// tuned specifically for chibi proportions (wide torso, short limbs) so arms clear hips.
public class ArmSwing : MonoBehaviour
{
    [Header("Swing amplitudes (meters)")]
    public float swingDepth = 0.20f;   // forward/back reach
    public float handHeight = 0.52f;   // hands at thigh/hip height
    public float swingWidth = 0.25f;   // lateral clearance (torso half-width is ~0.19m)
    public float elbowSpread = 0.32f;  // keep elbows flared outward
    [Range(0f, 1f)] public float ikWeight = 0.85f;

    private Animator anim;
    private int walkHash;
    private Transform root;
    private float currentWeight = 0f;

    void Awake()
    {
        anim = GetComponent<Animator>();
        walkHash = Animator.StringToHash("Walk");
        root = transform;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (anim == null || root == null) return;

        float targetSpeed = anim.GetFloat("Speed");
        float targetWeight = (targetSpeed > 0.05f) ? ikWeight : 0f;
        // Smooth damp weight for clean Idle <-> Walk transitions
        currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * 6f);

        if (currentWeight <= 0.001f)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            return;
        }

        var st = anim.GetCurrentAnimatorStateInfo(layerIndex);
        bool inWalk = st.shortNameHash == walkHash;
        float phase = (inWalk ? st.normalizedTime : 0f) * Mathf.PI * 2f;
        float s = Mathf.Sin(phase);

        Vector3 fwd = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(root.right, Vector3.up).normalized;
        Vector3 basePos = root.position + Vector3.up * handHeight;

        // Left & Right hand targets (contra-phase)
        Vector3 lHand = basePos + fwd * (s * swingDepth) - right * swingWidth;
        Vector3 rHand = basePos + fwd * (-s * swingDepth) + right * swingWidth;

        // Elbow hints (flared slightly outward and back to avoid straight-arm stiffness)
        Vector3 elbowBase = root.position + Vector3.up * (handHeight + 0.22f);
        Vector3 lElbow = elbowBase - right * elbowSpread - fwd * (s * 0.06f);
        Vector3 rElbow = elbowBase + right * elbowSpread + fwd * (s * 0.06f);

        // Apply IK
        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, currentWeight);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, currentWeight);
        anim.SetIKPosition(AvatarIKGoal.LeftHand, lHand);
        anim.SetIKPosition(AvatarIKGoal.RightHand, rHand);

        anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, currentWeight * 0.75f);
        anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, currentWeight * 0.75f);
        anim.SetIKHintPosition(AvatarIKHint.LeftElbow, lElbow);
        anim.SetIKHintPosition(AvatarIKHint.RightElbow, rElbow);
    }
}
