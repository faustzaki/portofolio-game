using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Captures one frame per sit placement, from the real game loop (Play mode).
public class SitVariantsCapture : MonoBehaviour
{
    public string outputFolder = "C:/Belajar Coding/Projects/tools/sit_variants";
    private RenderTexture rt;
    private Texture2D tex;
    private Animator anim;
    private int phase = 0;
    private int timer = 0;
    private string logPath;
    private readonly (string tag, float y, float z)[] vars = {
        ("A_now",        -0.3055f, -1.00f),
        ("B_up3cm",      -0.2755f, -1.00f),
        ("C_up6cm",      -0.2455f, -1.00f),
        ("D_fwd_z-0.90", -0.3055f, -0.90f),
    };

    void Start()
    {
        Time.captureFramerate = 30;
        Application.runInBackground = true;
        Directory.CreateDirectory(outputFolder);
        foreach (var f in Directory.GetFiles(outputFolder)) File.Delete(f);
        logPath = Path.Combine(outputFolder, "log.txt");
        anim = GetComponentInParent<Animator>();
        foreach (var mb in GetComponentsInParent<MonoBehaviour>()) {
            var t = mb.GetType().Name;
            if (t == "PlayerController" || t == "ArmSwing") mb.enabled = false;
        }
        var cc = GetComponentInParent<CharacterController>();
        if (cc != null) cc.enabled = false;
        anim.SetBool("IsSitting", true);
        rt = new RenderTexture(960, 540, 24);
        tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        var camGo = new GameObject("VarCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f; cam.nearClipPlane = 0.05f;
        camGo.transform.position = new Vector3(-0.60f, 0.60f, 0.85f);
        camGo.transform.LookAt(new Vector3(-1.55f, -0.20f, -1.00f));
        camera_ = cam;
        File.WriteAllText(logPath, "variant|y|z|rtLumMin|rtLumMax\n");
        Apply();
    }
    private Camera camera_;

    void Apply()
    {
        var v = vars[phase];
        transform.position = new Vector3(-1.55f, v.y, v.z);
        transform.rotation = Quaternion.identity;
    }

    void LateUpdate()
    {
        if (phase >= vars.Length) return;
        timer++;
        if (timer < 20) return;                 // let the AnyState->Sit blend settle, then shoot
        var cam = camera_;
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
        cam.targetTexture = null; RenderTexture.active = null;
        var v = vars[phase];
        File.WriteAllBytes(Path.Combine(outputFolder, v.tag + ".png"), tex.EncodeToPNG());
        var all = tex.GetPixels32();
        int mn = 9999, mx = -1;
        for (int i = 0; i < all.Length; i += 13) { int l = all[i].r + all[i].g + all[i].b; if (l < mn) mn = l; if (l > mx) mx = l; }
        File.AppendAllText(logPath, $"{v.tag}|{v.y:F4}|{v.z:F2}|{mn}|{mx}\n");
        phase++; timer = 0;
        if (phase < vars.Length) Apply();
        else {
            Time.captureFramerate = 0;
            rt.Release();
            File.AppendAllText(logPath, "DONE\n");
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }
    }

    void OnDestroy() { if (rt != null) rt.Release(); }
}
