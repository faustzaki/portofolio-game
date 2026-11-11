using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayCamCapture : MonoBehaviour
{
    public int totalFrames = 200;
    public int fps = 30;
    public string outputFolder = "C:/Belajar Coding/Projects/tools/play_frames";

    private int frame = 0;
    private RenderTexture rt;
    private Texture2D tex;
    private Animator anim;
    private CharacterController cc;
    private Transform lf, rf;
    private string logPath;

    void Start()
    {
        Time.captureFramerate = fps;
        Application.runInBackground = true;
        Directory.CreateDirectory(outputFolder);
        foreach (var f in Directory.GetFiles(outputFolder)) File.Delete(f);
        logPath = Path.Combine(outputFolder, "log.txt");
        File.WriteAllText(logPath, "frame|pos|state|nt|speedParam|grounded|ccVelY|footL.y|footR.y|clip|meshMinY\n");

        anim = GetComponentInParent<Animator>();
        cc   = GetComponentInParent<CharacterController>();
        lf = anim.GetBoneTransform(HumanBodyBones.LeftToes);
        rf = anim.GetBoneTransform(HumanBodyBones.RightToes);
        rt = new RenderTexture(960, 540, 24);
        tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        Debug.Log("[PlayCamCapture] start, folder=" + outputFolder);
    }

    void LateUpdate()
    {
        if (frame >= totalFrames) return;
        var cam = Camera.main;
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
        var ci = anim.GetCurrentAnimatorClipInfo(0);
        Vector3 p = cc.transform.position;
        File.AppendAllText(logPath, string.Format("{0}|{1:F4},{2:F4},{3:F4}|{4}|{5:F3}|{6:F3}|{7}|{8:F3}|{9:F4}|{10:F4}|{11}\n",
            frame, p.x, p.y, p.z,
            st.IsName("Walk") ? "Walk" : st.IsName("Idle") ? "Idle" : "?",
            st.normalizedTime, anim.GetFloat("Speed"), cc.isGrounded, cc.velocity.y,
            lf != null ? lf.position.y : 0f, rf != null ? rf.position.y : 0f,
            ci.Length > 0 ? ci[0].clip.name : "-"));

        frame++;
        if (frame >= totalFrames)
        {
            Time.captureFramerate = 0;
            if (rt != null) rt.Release();
            File.AppendAllText(logPath, "DONE\n");
            Debug.Log("[PlayCamCapture] done");
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }
    }

    void OnDestroy() { if (rt != null) rt.Release(); }
}
