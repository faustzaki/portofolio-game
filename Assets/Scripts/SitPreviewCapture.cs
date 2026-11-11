using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Preview only: puts the character in the Sit pose on the bed edge and captures the clip.
// No interaction script -- this just drives the real Animator state machine and films it.
public class SitPreviewCapture : MonoBehaviour
{
    public int totalFrames = 220;
    public int fps = 30;
    public float mattressTopY = -0.447f;          // measured from the bed's MeshCollider
    public Vector3 bedEdgeXZ = new Vector3(-1.55f, 0f, -1.00f);
    public string outputFolder = "C:/Belajar Coding/Projects/tools/sit_preview";

    private int frame = 0;
    private RenderTexture rt;
    private Texture2D tex;
    private Camera cam;
    private Animator anim;
    private Transform lf, rf, hips;
    private SkinnedMeshRenderer smr;
    private string logPath;
    private float rootY;
    private float buttMinY, hipsOffsetFromRoot, buttOffset;
    private float buttNow;
    private Mesh probeMesh;

    // lowest skinned vertex within `radius` (XZ) of a world point -- the actual seat contact
    static float LowestSkinnedY(SkinnedMeshRenderer s, Mesh tmp, Vector3 around, float radius)
    {
        return LowestSkinnedY(s, tmp, around, radius, 0.30f);
    }

    // maxDrop: jangan ikut ngukur kaki yang ngantung jauh di bawah panggul
    static float LowestSkinnedY(SkinnedMeshRenderer s, Mesh tmp, Vector3 around, float radius, float maxDrop)
    {
        if (s == null) return around.y;
        s.BakeMesh(tmp, true);
        var mat = s.transform.localToWorldMatrix;
        var verts = tmp.vertices;
        float min = float.MaxValue, r2 = radius * radius;
        for (int i = 0; i < verts.Length; i++) {
            Vector3 w = mat.MultiplyPoint3x4(verts[i]);
            float dx = w.x - around.x, dz = w.z - around.z;
            if (dx * dx + dz * dz > r2) continue;
            if (w.y > around.y || w.y < around.y - maxDrop) continue;
            if (w.y < min) min = w.y;
        }
        return min == float.MaxValue ? around.y : min;
    }

    void Start()
    {
        Time.captureFramerate = fps;
        Application.runInBackground = true;
        Directory.CreateDirectory(outputFolder);
        foreach (var f in Directory.GetFiles(outputFolder)) File.Delete(f);
        logPath = Path.Combine(outputFolder, "log.txt");

        anim = GetComponentInParent<Animator>();
        smr = GetComponentInParent<SkinnedMeshRenderer>();
        if (smr == null) smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null) smr = Object.FindObjectOfType<SkinnedMeshRenderer>();
        Debug.Log("[SitPreview] smr=" + (smr != null ? smr.name : "NULL"));
        hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        lf = anim.GetBoneTransform(HumanBodyBones.LeftToes);
        rf = anim.GetBoneTransform(HumanBodyBones.RightToes);

        foreach (var mb in GetComponentsInParent<MonoBehaviour>()) {
            var t = mb.GetType().Name;
            if (t == "PlayerController" || t == "ArmSwing") mb.enabled = false;
        }
        var cc = GetComponentInParent<CharacterController>();
        if (cc != null) cc.enabled = false;   // preview: no physics interference

        // ---- measure the SEAT CONTACT: lowest skinned vertex around the pelvis ----
        // (anchoring on the Hips bone sinks the body, because the Hips bone is the pelvis
        //  centre and the buttock flesh sits below it)
        anim.SetBool("IsSitting", true);
        probeMesh = new Mesh();
        Mesh probe = probeMesh;
        float worstButt = 0f;
        for (int k = 0; k < 6; k++) {
            float nt = 0.1f + k * 0.15f;            // sample across the seated part of the clip
            anim.Play("Sit", 0, nt);
            anim.Update(0f);
            float b = LowestSkinnedY(smr, probe, hips.position, 0.28f);
            if (b < worstButt) worstButt = b;        // the deepest the butt ever goes
        }
        buttMinY = worstButt;
        // anchor di tulang panggul (terverifikasi: bokong chibi ~1 mm di atas bidang ini).
        // buttMinY di bawah ini masih bisa kemasukan kaki yang ngantung, jadi dipakai buat log saja.
        buttOffset = hips.position.y - transform.position.y;
        hipsOffsetFromRoot = hips.position.y - transform.position.y;
        rootY = mattressTopY - buttOffset;

        // ---- place on the bed edge, facing out into the room (+Z) ----
        transform.position = new Vector3(bedEdgeXZ.x, rootY, bedEdgeXZ.z);
        transform.rotation = Quaternion.LookRotation(Vector3.forward);

        // ---- preview camera (own camera; the game camera would sit outside the room) ----
        var camGo = new GameObject("SitPreviewCam");
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.05f;
        camGo.transform.position = new Vector3(-0.85f, 1.05f, 0.70f);   // 3/4 dari atas: cekungan kebaca
        camGo.transform.LookAt(new Vector3(-1.55f, -0.45f, -1.02f));
        rt = new RenderTexture(960, 540, 24);
        tex = new Texture2D(960, 540, TextureFormat.RGB24, false);

        anim.Play("Sit", 0, 0f);
        anim.Update(0f);

        File.WriteAllText(logPath, string.Format(
            "SitPreview: mattressTopY={0:F4}\n" +
            "  hipsBoneOffsetFromRoot={1:F4}   buttMinOffsetFromRoot={2:F4}  (butt sits {3:F4} m BELOW the Hips bone)\n" +
            "  -> rootY={4:F4}  placed at ({5:F2},{4:F4},{6:F2}) yaw=0 (facing +Z)\n" +
            "frame|clipTime|state|nt|hipsY|buttY|toeL.y|toeR.y|buttOverMattress|toeOverFloor\n",
            mattressTopY, hipsOffsetFromRoot, buttOffset, hipsOffsetFromRoot - buttOffset,
            rootY, bedEdgeXZ.x, bedEdgeXZ.z));
        Debug.Log("[SitPreview] placing done, capturing " + totalFrames + " frames");
    }

    void LateUpdate()
    {
        if (frame >= totalFrames) return;
        if (cam == null) return;

        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(outputFolder, "f_" + frame.ToString("D4") + ".png"), tex.EncodeToPNG());

        var st = anim.GetCurrentAnimatorStateInfo(0);
        if (frame % 5 == 0) buttNow = LowestSkinnedY(smr, probeMesh, hips.position, 0.28f);

        // dump titik kontak badan (bokong+paha) buat bake cekungan kasur di edit mode
        if (frame == 30 && smr != null) {
            smr.BakeMesh(probeMesh, true);
            var mm = smr.transform.localToWorldMatrix;
            var lines = new List<string>();
            foreach (var v in probeMesh.vertices) {
                var w = mm.MultiplyPoint3x4(v);
                if (w.y > buttNow - 0.03f && w.y < buttNow + 0.12f)
                    lines.Add(w.x.ToString("F4") + "," + w.y.ToString("F4") + "," + w.z.ToString("F4"));
            }
            File.WriteAllLines(Path.Combine(outputFolder, "contact_points.txt"), lines);
            Debug.Log("[SitPreview] contact points dumped: " + lines.Count);
        }
        File.AppendAllText(logPath, string.Format("{0}|{1:F3}|{2}|{3:F3}|{4:F4}|{5:F4}|{6:F4}|{7:F4}|{8:F4}|{9:F4}\n",
            frame, st.normalizedTime * 7f,
            st.IsName("Sit") ? "Sit" : st.IsName("Idle") ? "Idle" : st.IsName("Walk") ? "Walk" : "?",
            st.normalizedTime,
            hips != null ? hips.position.y : 0f,
            buttNow,
            lf != null ? lf.position.y : 0f,
            rf != null ? rf.position.y : 0f,
            buttNow - mattressTopY,
            (lf != null ? lf.position.y : 0f) + 0.95f));

        frame++;
        if (frame >= totalFrames) {
            Time.captureFramerate = 0;
            if (rt != null) rt.Release();
            File.AppendAllText(logPath, "DONE\n");
            Debug.Log("[SitPreview] done");
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }
    }

    void OnDestroy() { if (rt != null) rt.Release(); }
}
