using System.Collections;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PhysicsProbe : MonoBehaviour
{
    public string outputFolder = "C:/Belajar Coding/Projects/tools/phys_probe";
    private CharacterController cc;
    private Animator anim;
    private string logPath;

    void Start()
    {
        Directory.CreateDirectory(outputFolder);
        foreach (var f in Directory.GetFiles(outputFolder)) File.Delete(f);
        logPath = Path.Combine(outputFolder, "log.txt");
        cc = GetComponentInParent<CharacterController>();
        anim = GetComponentInParent<Animator>();
        foreach (var pb in GetComponentsInParent<MonoBehaviour>()) { var t = pb.GetType().Name; if (t == "PlayerController" || t == "ArmSwing") pb.enabled = false; }
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        var sb = new System.Text.StringBuilder();
        var floorCol = GameObject.Find("Floor_Collider");
        if (floorCol != null) {
            var fc = floorCol.GetComponent<Collider>();
            sb.AppendLine($"Floor_Collider world span Y: {fc.bounds.min.y:F4} .. {fc.bounds.max.y:F4}  (visual Indo_Floor top = -0.9500)");
        }
        var wR = GameObject.Find("Boundary_Wall_Right");
        if (wR != null) sb.AppendLine($"Boundary_Wall_Right inner face X = {wR.GetComponent<Collider>().bounds.min.x:F4}");

        Renderer bedR = null, fanR = null, tableR = null;
        foreach (var r in GetComponentsInParent<Renderer>()) { }
        foreach (var r in Object.FindObjectsOfType<Renderer>()) {
            if (r.name == "PP_Bed") bedR = r;
            if (r.name == "PP_FloorFan") fanR = r;
            if (r.name == "PP_RoundTable") tableR = r;
        }
        Vector3 bedC = bedR != null ? new Vector3(bedR.bounds.center.x, 0, bedR.bounds.center.z) : new Vector3(-1.55f, 0, -1.85f);
        sb.AppendLine($"PP_Bed XZ centre = ({bedC.x:F3},{bedC.z:F3}) halfsize=({(bedR!=null?bedR.bounds.extents.x:0):F3},{(bedR!=null?bedR.bounds.extents.z:0):F3})");

        // ---------- phase 1: walk straight into the bed ----------
        transform.position = new Vector3(0f, -0.9453f, -1.85f);
        yield return null;
        sb.AppendLine("\n--- PHASE 1: drive into PP_Bed (no collider on it?) ---");
        yield return MoveToward(bedC, 4f, sb);

        // ---------- phase 2: walk straight into the right wall ----------
        transform.position = new Vector3(0f, -0.9453f, 0f);
        yield return null;
        sb.AppendLine("\n--- PHASE 2: drive into Boundary_Wall_Right ---");
        yield return MoveToward(new Vector3(6f, 0f, 0f), 4f, sb);

        File.WriteAllText(logPath, sb.ToString());
        File.AppendAllText(logPath, "DONE\n");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    IEnumerator MoveToward(Vector3 target, float seconds, System.Text.StringBuilder sb)
    {
        anim.Play("Walk", 0, 0f);
        float t = 0f;
        while (t < seconds) {
            Vector3 d = target - transform.position; d.y = 0f;
            Vector3 dir = d.normalized;
            if (dir.sqrMagnitude > 0.001f) {
                transform.rotation = Quaternion.LookRotation(dir);
                cc.Move(dir * 1.0f * Time.deltaTime);
            }
            if (Mathf.Repeat(t, 0.5f) < Time.deltaTime) {
                float dist = Vector3.Distance(new Vector3(transform.position.x,0,transform.position.z), new Vector3(target.x,0,target.z));
                sb.AppendLine($"   t={t:F1}s pos=({transform.position.x:F3},{transform.position.y:F4},{transform.position.z:F3}) distToTarget={dist:F3}m grounded={cc.isGrounded}");
            }
            yield return null; t += Time.deltaTime;
        }
    }
}
