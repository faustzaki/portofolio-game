using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;

// Rebuild the mattress sit-dent mesh from the ORIGINAL low-poly PP_Bed source
// with a bounded subdivision cap, so the dent shape survives but the mesh stops
// being a 367k-tri picking hazard. Old asset is left untouched; new mesh is saved
// as PP_Bed_SitDent_opt.asset and wired into the scene.
public static class BedOptimizer
{
    const float surfY = -0.4474f;
    const float seatX = -1.55f, seatZ = -0.92f;
    const float sink = 0.90f * 0.065f;

    static float SampleF(float x, float z)
    {
        const float RX = 0.19f, RZ = 0.17f, LOBE = 0.06f;
        float cx = seatX, cz = seatZ - 0.08f;
        float dxL = (x - (cx - LOBE)) / RX, dzL = (z - cz) / RZ;
        float dxR = (x - (cx + LOBE)) / RX, dzR = (z - cz) / RZ;
        float rL = dxL * dxL + dzL * dzL, rR = dxR * dxR + dzR * dzR;
        float r2 = Mathf.Min(rL, rR);
        if (r2 >= 1f) return 0f;
        float f = 1f - Mathf.Sqrt(r2);
        f *= 1f + 0.10f * Mathf.Sin(Mathf.Atan2(z - cz, x - cx) + 0.7f);
        return f * f * (3f - 2f * f);
    }

    static float Mold(float x, float z, float edgeFade)
    {
        float f = SampleF(x, z);
        if (f <= 0.004f) return 0f;
        float ss = f * f * (3f - 2f * f);
        float dy = -sink * ss * edgeFade;
        float mask = Mathf.Clamp01(f);
        float n1 = Mathf.Sin(x * 23.7f + z * 11.3f) * Mathf.Cos(x * 9.1f - z * 17.9f);
        float n2 = Mathf.Sin(x * 41.3f - z * 29.7f + 1.7f);
        dy += (0.004f * n1 + 0.002f * n2) * mask;
        dy += 0.005f * Mathf.Exp(-((f - 0.85f) * (f - 0.85f)) / 0.02f) * Mathf.Max(0f, n1) * mask;
        return dy;
    }

    static Mesh Build(Mesh orig, Transform bedT, int nMax)
    {
        var wm = bedT.localToWorldMatrix;
        var inv = wm.inverse;
        var sv = orig.vertices; var suv = orig.uv; var st = orig.triangles;
        var wmin = wm.MultiplyPoint3x4(orig.bounds.min);
        var wmax = wm.MultiplyPoint3x4(orig.bounds.max);
        var nv = new List<Vector3>(); var nuv = new List<Vector2>(); var nt = new List<int>();
        for (int i = 0; i < sv.Length; i++)
        {
            nv.Add(sv[i]); nuv.Add(i < suv.Length ? suv[i] : Vector2.zero);
            var w = wm.MultiplyPoint3x4(sv[i]);
            if (w.y <= surfY + 0.005f && w.y >= surfY - 0.085f)
            {
                float d = Mathf.Min(Mathf.Min(w.x - wmin.x, wmax.x - w.x), Mathf.Min(w.z - wmin.z, wmax.z - w.z));
                float ef = Mathf.Clamp01((d - 0.04f) / 0.08f);
                w.y += Mold(w.x, w.z, ef);
                nv[i] = inv.MultiplyPoint3x4(w);
            }
        }
        for (int t = 0; t < st.Length; t += 3)
        {
            int i0 = st[t], i1 = st[t + 1], i2 = st[t + 2];
            var w0 = wm.MultiplyPoint3x4(sv[i0]); var w1 = wm.MultiplyPoint3x4(sv[i1]); var w2 = wm.MultiplyPoint3x4(sv[i2]);
            bool top = w0.y <= surfY + 0.005f && w1.y <= surfY + 0.005f && w2.y <= surfY + 0.005f &&
                       w0.y >= surfY - 0.085f && w1.y >= surfY - 0.085f && w2.y >= surfY - 0.085f;
            if (!top) { nt.Add(i0); nt.Add(i1); nt.Add(i2); continue; }
            int N = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max((w1 - w0).magnitude, (w2 - w0).magnitude, (w2 - w1).magnitude) / 0.012f), 2, nMax);
            var idx = new int[N + 1, N + 1];
            for (int a = 0; a <= N; a++) for (int b = 0; b <= N - a; b++)
            {
                float bu = (float)a / N, bv = (float)b / N, bw = 1f - bu - bv;
                var w = w0 * bw + w1 * bu + w2 * bv;
                var uv = suv[i0] * bw + suv[i1] * bu + suv[i2] * bv;
                float d = Mathf.Min(Mathf.Min(w.x - wmin.x, wmax.x - w.x), Mathf.Min(w.z - wmin.z, wmax.z - w.z));
                float ef = Mathf.Clamp01((d - 0.04f) / 0.08f);
                w.y += Mold(w.x, w.z, ef);
                idx[a, b] = nv.Count; nv.Add(inv.MultiplyPoint3x4(w)); nuv.Add(uv);
            }
            for (int a = 0; a < N; a++) for (int b = 0; b < N - a; b++)
            {
                nt.Add(idx[a, b]); nt.Add(idx[a + 1, b]); nt.Add(idx[a, b + 1]);
                if (b < N - a - 1) { nt.Add(idx[a + 1, b]); nt.Add(idx[a + 1, b + 1]); nt.Add(idx[a, b + 1]); }
            }
        }
        var mesh = new Mesh { name = "PP_Bed_SitDent_opt" };
        mesh.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(nv); mesh.SetUVs(0, nuv); mesh.SetTriangles(nt, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    public static void Run()
    {
        var S = new StringBuilder("BED OPTIMIZE\n");
        var bed = GameObject.Find("PP_Bed");
        var mf = bed.GetComponent<MeshFilter>(); var mc = bed.GetComponent<MeshCollider>();
        var cur = mf.sharedMesh;
        S.AppendLine("OLD " + cur.name + " verts=" + cur.vertexCount + " tris=" + (cur.triangles.Length / 3) + " path=" + AssetDatabase.GetAssetPath(cur));

        Mesh orig = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Environment/Room_6x6_Master.fbx"))
        { var m = o as Mesh; if (m != null && m.name == "PP_Bed") { orig = m; break; } }
        if (orig == null) { S.AppendLine("ERROR: PP_Bed source mesh not found"); File.WriteAllText("C:/tmp/bedopt.txt", S.ToString()); return; }
        S.AppendLine("SRC " + orig.name + " verts=" + orig.vertexCount + " tris=" + (orig.triangles.Length / 3));

        // reference heightmap sampled from the CURRENT (heavy) collider
        var xs = new List<float>(); var zs = new List<float>();
        for (float x = -1.95f; x <= -1.1501f; x += 0.02f) xs.Add(x);
        for (float z = -1.55f; z <= -0.7501f; z += 0.02f) zs.Add(z);
        int nPts = xs.Count * zs.Count;
        var refH = new float[nPts]; int refHit = 0;
        var refGo = new GameObject("__bed_ref_probe");
        var refCol = refGo.AddComponent<MeshCollider>();
        refCol.sharedMesh = cur;
        refGo.transform.SetPositionAndRotation(bed.transform.position, bed.transform.rotation);
        refGo.transform.localScale = bed.transform.lossyScale;
        Physics.SyncTransforms();
        for (int i = 0; i < xs.Count; i++) for (int j = 0; j < zs.Count; j++)
        {
            RaycastHit h;
            if (refCol.Raycast(new Ray(new Vector3(xs[i], 0.6f, zs[j]), Vector3.down), out h, 3f)) { refH[i * zs.Count + j] = h.point.y; refHit++; }
            else refH[i * zs.Count + j] = float.NaN;
        }
        S.AppendLine("REF heightmap hits=" + refHit + "/" + nPts);

        int[] cands = new int[] { 8, 12, 16, 24, 32 };
        var meshes = new Dictionary<int, Mesh>();
        var tris = new Dictionary<int, int>();
        var maxDev = new Dictionary<int, float>();
        var meanDev = new Dictionary<int, float>();
        foreach (int nMax in cands)
        {
            var mesh = Build(orig, bed.transform, nMax);
            meshes[nMax] = mesh; tris[nMax] = mesh.triangles.Length / 3;
            var go = new GameObject("__bed_probe_" + nMax);
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            go.transform.SetPositionAndRotation(bed.transform.position, bed.transform.rotation);
            go.transform.localScale = bed.transform.lossyScale;
            Physics.SyncTransforms();
            float mx = 0f; double sum = 0; int n = 0;
            for (int i = 0; i < xs.Count; i++) for (int j = 0; j < zs.Count; j++)
            {
                if (float.IsNaN(refH[i * zs.Count + j])) continue;
                RaycastHit h;
                if (!col.Raycast(new Ray(new Vector3(xs[i], 0.6f, zs[j]), Vector3.down), out h, 3f)) { mx = 99f; continue; }
                float dv = Mathf.Abs(h.point.y - refH[i * zs.Count + j]);
                if (dv > mx) mx = dv; sum += dv; n++;
            }
            maxDev[nMax] = mx; meanDev[nMax] = n > 0 ? (float)(sum / n) : 99f;
            S.AppendLine("CAND nMax=" + nMax + " verts=" + mesh.vertexCount + " tris=" + tris[nMax] +
                         " maxDev=" + (mx * 1000f).ToString("F2") + "mm meanDev=" + (meanDev[nMax] * 1000f).ToString("F3") + "mm");
            UnityEngine.Object.DestroyImmediate(go);
        }

        // pick: keep the coarsest candidate whose worst sample is within 3 mm
        // (the dent's own fine details are ~2-4 mm, so 3 mm is inside its own noise floor)
        int chosen = -1;
        foreach (int nMax in cands) if (maxDev[nMax] <= 0.003f) { chosen = nMax; break; }
        if (chosen < 0) { chosen = cands[cands.Length - 1]; }
        S.AppendLine("CHOSEN nMax=" + chosen + " tris=" + tris[chosen] + " maxDev=" + (maxDev[chosen] * 1000f).ToString("F2") + "mm");

        var final = meshes[chosen];
        final.name = "PP_Bed_SitDent_opt";
        // local dims sanity for the selected mesh
        float mn = 9f, mx2 = -9f;
        var wm = bed.transform.localToWorldMatrix;
        foreach (var v in final.vertices)
        {
            var w = wm.MultiplyPoint3x4(v);
            if (Mathf.Abs(w.x - seatX) < 0.30f && Mathf.Abs(w.z - (seatZ - 0.08f)) < 0.30f) { if (w.y < mn) mn = w.y; if (w.y > mx2) mx2 = w.y; }
        }
        S.AppendLine("SEAT area minY=" + mn.ToString("F4") + " maxY=" + mx2.ToString("F4") + " (flat mattress top ref " + surfY.ToString("F4") + ")");

        string ap = "Assets/Environment/PP_Bed_SitDent_opt.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
        if (old != null) { EditorUtility.CopySerialized(final, old); EditorUtility.SetDirty(old); final = old; }
        else { AssetDatabase.CreateAsset(final, ap); }
        AssetDatabase.SaveAssets();
        mf.sharedMesh = final;
        if (mc != null) { mc.sharedMesh = final; }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        bool saved = EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/KosanGameScene.unity");
        S.AppendLine("saved asset=" + ap + " sceneSaved=" + saved);

        foreach (int nMax in cands) if (nMax != chosen) UnityEngine.Object.DestroyImmediate(meshes[nMax]);

        File.WriteAllText("C:/tmp/bedopt.txt", S.ToString());

        // perf: render + pick cost with the new mesh
        Physics.SyncTransforms();
        var sv = SceneView.lastActiveSceneView;
        try {
        if (sv != null)
        {
            var pos = bed.GetComponent<Renderer>().bounds.center;
            var rot = sv.camera.transform.rotation;
            sv.LookAt(pos, rot, 0.15f);
            sv.camera.Render(); sv.camera.Render();
            var sw = Stopwatch.StartNew(); for (int i = 0; i < 10; i++) sv.camera.Render(); sw.Stop();
            S.AppendLine("NEW render close 0.15m = " + (sw.Elapsed.TotalMilliseconds / 10.0).ToString("F2") + " ms  (was 2.09)");
            float w = sv.camera.pixelWidth, h2 = sv.camera.pixelHeight;
            var m2 = typeof(HandleUtility).GetMethod("PickGameObject", new System.Type[] { typeof(Vector2), typeof(bool) });
            if (m2 != null)
            {
                object[] args = new object[] { new Vector2(w * 0.5f, h2 * 0.5f), false };
                sw.Restart(); int N = 60; for (int i = 0; i < N; i++) m2.Invoke(null, args); sw.Stop();
                S.AppendLine("NEW pick center = " + (sw.Elapsed.TotalMilliseconds / N).ToString("F3") + " ms/call  (was 7.785)");
                var p2 = typeof(HandleUtility).GetMethod("PickGameObject", new System.Type[] { typeof(Vector2), typeof(bool), typeof(GameObject[]) });
                if (p2 != null) { object[] a2 = new object[] { new Vector2(w * 0.5f, h2 * 0.5f), false, new GameObject[0] };
                    sw.Restart(); for (int i = 0; i < N; i++) p2.Invoke(null, a2); sw.Stop();
                    S.AppendLine("NEW pick center (ignore) = " + (sw.Elapsed.TotalMilliseconds / N).ToString("F3") + " ms/call"); }
            }
            sv.LookAt(pos, rot, 1.2f);
        }
        } catch (System.Exception pe) { S.AppendLine("perf-probe skipped: " + pe.GetType().Name); }
        UnityEngine.Object.DestroyImmediate(refGo);

        File.WriteAllText("C:/tmp/bedopt.txt", S.ToString());
        UnityEngine.Debug.Log(S.ToString());
    }
}
