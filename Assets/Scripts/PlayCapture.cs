using System.Collections;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayCapture : MonoBehaviour
{
    public float duration = 12f;
    public float interval = 0.25f;

    void Start() { StartCoroutine(Run()); }

    IEnumerator Run()
    {
        string proj = Application.dataPath;
        if (proj.EndsWith("/Assets")) proj = proj.Substring(0, proj.Length - 7);
        string dir = Path.Combine(proj, "PlayCaptureOut");
        Directory.CreateDirectory(dir);
        string log = Path.Combine(dir, "log.txt");
        File.WriteAllText(log, "t|pos|state|nt|speedParam|grounded|ccVelY|footL|footR|clip\n");

        var anim = GetComponentInParent<Animator>();
        var cc   = GetComponentInParent<CharacterController>();
        var lf = anim.GetBoneTransform(HumanBodyBones.LeftToes);
        var rf = anim.GetBoneTransform(HumanBodyBones.RightToes);

        float t = 0f; int i = 0;
        while (t < duration)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            var ci = anim.GetCurrentAnimatorClipInfo(0);
            string clip = ci.Length > 0 ? ci[0].clip.name : "-";
            Vector3 p = cc != null ? cc.transform.position : transform.position;
            string line = string.Format(
                "{0:F2}|{1:F3},{2:F3},{3:F3}|{4}|{5:F3}|{6:F3}|{7}|{8:F3}|{9:F4}|{10:F4}|{11}\n",
                t, p.x, p.y, p.z,
                st.IsName("Walk") ? "Walk" : st.IsName("Idle") ? "Idle" : ("?" + st.shortNameHash),
                st.normalizedTime, anim.GetFloat("Speed"), cc != null ? cc.isGrounded.ToString() : "n/a",
                cc != null ? cc.velocity.y : 0f,
                lf != null ? lf.position.y : 0f, rf != null ? rf.position.y : 0f, clip);
            File.AppendAllText(log, line);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "cap_" + i.ToString("D3") + ".png"));
            i++;
            yield return new WaitForSeconds(interval);
            t += interval;
        }
        File.AppendAllText(log, "DONE\n");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }
}
