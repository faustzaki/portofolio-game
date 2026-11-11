using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SitFinalVerification
{
    const string Output = "C:/Belajar Coding/Projects/tools/";

    public static void FitMattress(Mesh mattress, Transform bed, float flatY, System.Collections.Generic.List<float> originalYs)
    {
        var player = GameObject.Find("Player_Chibi");
        var renderer = player.GetComponentInChildren<SkinnedMeshRenderer>();
        var hips = player.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).position;
        var skin = new Mesh();
        var probe = new GameObject("TemporaryMattressFit");
        probe.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            renderer.BakeMesh(skin, true);
            var sv = skin.vertices;
            for (int i = 0; i < sv.Length; i++) sv[i] = renderer.transform.TransformPoint(sv[i]);
            skin.vertices = sv; skin.RecalculateBounds();
            var collider = probe.AddComponent<MeshCollider>(); collider.sharedMesh = skin;
            Physics.SyncTransforms();
            var vertices = mattress.vertices;
            var originalCollider = bed.GetComponent<MeshCollider>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = bed.TransformPoint(vertices[i]);
                if (originalYs[i] < flatY - 0.085f || originalYs[i] > flatY + 0.005f || Mathf.Abs(w.x - hips.x) > 0.35f || Mathf.Abs(w.z - hips.z) > 0.24f) continue;
                RaycastHit hit;
                if (!collider.Raycast(new Ray(new Vector3(w.x, -2f, w.z), Vector3.up), out hit, 4f)) continue;
                float target = Mathf.Min(flatY, hit.point.y - 0.003f);
                if (target < flatY - 0.080f || hit.point.y > flatY + 0.025f) continue;
                RaycastHit originalSurface;
                if (!originalCollider.Raycast(new Ray(new Vector3(w.x, 1f, w.z), Vector3.down), out originalSurface, 3f)) continue;
                // Raise/lower the fabric to the seated underside, feathering outside
                // the loaded patch; the original broad folds remain around it.
                float blend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, hit.point.y - flatY) / 0.025f);
                w.y = Mathf.Lerp(w.y, target - (originalSurface.point.y - originalYs[i]), blend);
                vertices[i] = bed.InverseTransformPoint(w);
            }
            mattress.vertices = vertices;
        }
        finally { UnityEngine.Object.DestroyImmediate(probe); UnityEngine.Object.DestroyImmediate(skin); }
    }

    public static string Check(int attempt)
    {
        var player = GameObject.Find("Player_Chibi");
        var animator = player.GetComponent<Animator>();
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
        var renderer = player.GetComponentInChildren<SkinnedMeshRenderer>();
        var bed = GameObject.Find("PP_Bed").GetComponent<MeshCollider>();
        var mesh = new Mesh();
        var probe = new GameObject("TemporaryContactProbe");
        probe.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            // This imported rig requires useScale=true; transform once to world space.
            renderer.BakeMesh(mesh, true);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = renderer.transform.TransformPoint(vertices[i]);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            var body = probe.AddComponent<MeshCollider>();
            body.sharedMesh = mesh;
            Physics.SyncTransforms();
            float min = float.PositiveInfinity, max = float.NegativeInfinity, sum = 0;
            int penetrating = 0, count = 0;
            var rows = new StringBuilder();
            rows.AppendLine("id | x_m | z_m | bodyY_m | mattressY_m | gap_mm");
            for (int z = 0; z < 5; z++)
            for (int x = 0; x < 8; x++)
            {
                // Four columns per buttock/upper thigh; exclude the open crotch.
                float px = hips.x + (x < 4 ? Mathf.Lerp(-0.14f, -0.07f, x / 3f) : Mathf.Lerp(0.07f, 0.14f, (x - 4) / 3f));
                float pz = hips.z + Mathf.Lerp(-0.05f, 0.01f, z / 4f);
                RaycastHit skin, mattress;
                if (!body.Raycast(new Ray(new Vector3(px, -2f, pz), Vector3.up), out skin, 4f) ||
                    !bed.Raycast(new Ray(new Vector3(px, 1f, pz), Vector3.down), out mattress, 3f))
                    throw new InvalidOperationException("Contact ray missed at " + x + "," + z);
                float gap = (skin.point.y - mattress.point.y) * 1000f;
                min = Mathf.Min(min, gap); max = Mathf.Max(max, gap); sum += gap; count++;
                if (gap < 0f) penetrating++;
                rows.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:D2} | {1:F5} | {2:F5} | {3:F6} | {4:F6} | {5:F3}", count, px, pz, skin.point.y, mattress.point.y, gap));
            }
            float avg = sum / count;
            int auditHits = 0, auditPenetrations = 0;
            float auditMin = float.PositiveInfinity;
            foreach (var vertex in vertices)
            {
                if (vertex.y > -0.40f) continue;
                RaycastHit hit;
                if (!bed.Raycast(new Ray(new Vector3(vertex.x, 1f, vertex.z), Vector3.down), out hit, 3f)) continue;
                float gap = (vertex.y - hit.point.y) * 1000f;
                auditHits++; auditMin = Mathf.Min(auditMin, gap);
                if (gap < 0f) auditPenetrations++;
            }
            bool pass = count == 40 && penetrating == 0 && auditPenetrations == 0 && avg >= 0 && avg <= 8 && max <= 15;
            string summary = string.Format(CultureInfo.InvariantCulture,
                "attempt={0}/5 result={1}\nscene=Assets/Scenes/KosanGameScene.unity\nmethod=40 fixed XZ samples (8x5), mesh underside raycast UP, mattress raycast DOWN from above; signed gap detects penetration\nrootY={2:F6} hipsY={3:F6}\npoints={4} min_mm={5:F3} max_mm={6:F3} mean_mm={7:F3} penetrating={8}\n",
                attempt, pass ? "PASS" : "FAIL", player.transform.position.y, hips.y, count, min, max, avg, penetrating);
            summary += string.Format(CultureInfo.InvariantCulture, "sample_area=two load-bearing buttock/upper-thigh patches; 4x5 per side, crotch excluded\nextra_mesh_audit=all body vertices below y=-0.40m with mattress hit; hits={0} min_mm={1:F3} penetrating={2}\n", auditHits, auditMin, auditPenetrations);
            File.WriteAllText(Output + "contact_check.txt", summary + rows);
            File.WriteAllText(Output + "sit_final/contact_attempt_" + attempt + ".txt", summary + rows);
            return summary;
        }
        finally { UnityEngine.Object.DestroyImmediate(probe); UnityEngine.Object.DestroyImmediate(mesh); }
    }

    public static string Capture(string filename)
    {
        var camera = GameObject.Find("SitPreviewCam").GetComponent<Camera>();
        camera.transform.position = new Vector3(0.65f, 0.5f, 0.35f);
        camera.transform.LookAt(new Vector3(-1.55f, -0.05f, -1.08f));
        camera.fieldOfView = 45;
        var target = new RenderTexture(1440, 1080, 24);
        var texture = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.aspect = 4f / 3f;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0);
            texture.Apply();
            string path = Output + "sit_final/" + filename;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            return path;
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
