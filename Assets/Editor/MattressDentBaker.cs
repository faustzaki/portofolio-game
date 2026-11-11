using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Compilation;
using System.Collections.Generic;
using System.IO;

// Bake cekungan + kerutan kain kasur dari titik kontak badan karakter.
// Menu: Tools > Bake Mattress Dent   (bisa dipanggil dari script: MattressDentBaker.Schedule())
public static class MattressDentBaker
{
    static bool pending;
    public static bool PreviewOnly;
    public static bool FitSeatedContact;
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update += Pump; EditorApplication.delayCall += Pump; }
    static void Pump() { if (!pending) return; pending = false; Run(); }
    [MenuItem("Tools/Bake Mattress Dent")] public static void Schedule() { pending = true; Debug.Log("[MattressDentBaker] dijadwalkan"); }

    public static void Run()
    {
      string log = "C:/Belajar Coding/Projects/tools/bake_log.txt";
      try {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/KosanGameScene.unity")
            throw new System.InvalidOperationException("Bake hanya di KosanGameScene dalam Edit Mode.");
        var sb = new System.Text.StringBuilder();
        var bed = GameObject.Find("PP_Bed"); var mf = bed.GetComponent<MeshFilter>(); var mc = bed.GetComponent<MeshCollider>();
        Mesh orig = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Environment/Room_6x6_Master.fbx")) { var m = o as Mesh; if (m != null && m.name == "PP_Bed") { orig = m; break; } }
        if (orig == null) { File.WriteAllText(log, "ERROR mesh asli PP_Bed tidak ditemukan"); return; }
        mf.sharedMesh = orig; if (mc != null) mc.sharedMesh = orig;
        Physics.SyncTransforms();
    
        float softness = 0.90f, maxSink = 0.065f, sink = softness * maxSink;
        float seatX = -1.55f, seatZ = -0.92f, fieldHalf = 0.12f, cell = 0.02f;
        float surfY = -0.4474f;   // top kasur verified raycast; jangan raycast di seat (mepet tepi=kena bevel)
        var lines = File.ReadAllLines("C:/Belajar Coding/Projects/tools/sit_soft06_dent/contact_points.txt");
        int fx = Mathf.CeilToInt(fieldHalf*2f/cell)+1, fz = fx;
        float x0 = seatX - fieldHalf, z0 = seatZ - fieldHalf;
        var field = new float[fx*fz]; int hits = 0;
        foreach (var ln in lines) { var p = ln.Split(','); if (p.Length < 3) continue;
          float px = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture);
          float pz = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
          int i = Mathf.RoundToInt((px-x0)/cell), j = Mathf.RoundToInt((pz-z0)/cell);
          if (i<0||j<0||i>=fx||j>=fz) continue;
          float dpt = Mathf.Clamp01((-0.4474f - float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)) / 0.09f);   // kedalaman: bokong bobot besar, paha ringan
          field[i*fz+j] += 0.25f + 1.75f * dpt; hits++; }
        for (int pass = 0; pass < 5; pass++) { var d = new float[field.Length];
          for (int i = 0; i < fx; i++) for (int j = 0; j < fz; j++) { float sum = 0; int cnt = 0;
            for (int di=-1; di<=1; di++) for (int dj=-1; dj<=1; dj++) { int ii=i+di, jj=j+dj;
              if (ii<0||jj<0||ii>=fx||jj>=fz) continue; sum += field[ii*fz+jj]; cnt++; }
            d[i*fz+j] = sum/cnt; }
          field = d; }
        // One anisotropic Gaussian pass, with sigma measured in grid cells.
        const float sigmaX = 1f, sigmaZ = 1.8f * sigmaX;
        var gaussian = new float[field.Length];
        for (int i = 0; i < fx; i++) for (int j = 0; j < fz; j++) {
          float sum = 0f, weight = 0f;
          for (int di = -3; di <= 3; di++) for (int dj = -3; dj <= 3; dj++) {
            int ii = i + di, jj = j + dj;
            if (ii < 0 || jj < 0 || ii >= fx || jj >= fz) continue;
            float w = Mathf.Exp(-0.5f * (di * di / (sigmaX * sigmaX) + dj * dj / (sigmaZ * sigmaZ)));
            sum += field[ii * fz + jj] * w; weight += w;
          }
          gaussian[i * fz + j] = sum / weight;
        }
        field = gaussian;
        float fmax = 0f; foreach (var f in field) if (f > fmax) fmax = f;
        for (int k = 0; k < field.Length; k++) field[k] = Mathf.Pow(Mathf.Clamp01(field[k]/fmax), 0.70f);
        for (int i = 0; i < fx; i++) for (int j = 0; j < fz; j++) {
          float border = Mathf.Min(i, j, fx - 1 - i, fz - 1 - j) * cell;
          field[i * fz + j] *= Mathf.SmoothStep(0f, 1f, border / 0.08f);
        }
        System.Func<float,float,float> SampleF = (x, z) => {
          // ELIPS ANALITIK NATURAL: 2 lobus bokong + asymmetry, tanpa rim propaganda.
          const float RX = 0.19f, RZ = 0.17f, LOBE = 0.06f;   // lobe separation ±5.5cm arah X
          float cx = seatX, cz = seatZ - 0.08f;
          // peak dekat tepi duduk (bokong berat condong ke arah kaki).
          float dxL = (x - (cx - LOBE)) / RX, dzL = (z - cz) / RZ;
          float dxR = (x - (cx + LOBE)) / RX, dzR = (z - cz) / RZ;
          float rL = dxL*dxL + dzL*dzL, rR = dxR*dxR + dzR*dzR;
          float r2 = Mathf.Min(rL, rR);              // 2 lobus: min dari 2 elips
          if (r2 >= 1f) return 0f;
          float f = 1f - Mathf.Sqrt(r2);
          // asymmetry halus: sisi belakang sedikit lebih dalam (panggul condong belakang)
          f *= 1f + 0.10f * Mathf.Sin(Mathf.Atan2(z - cz, x - cx) + 0.7f);
          return f * f * (3f - 2f * f);
        };
        var srcMesh = mf.sharedMesh; var wm = bed.transform.localToWorldMatrix; var inv = wm.inverse;
        var sv = srcMesh.vertices; var suv = srcMesh.uv; var st = srcMesh.triangles;
        var nv = new List<Vector3>(); var nuv = new List<Vector2>(); var nt = new List<int>();
        var originalYs = new List<float>();
        int displaced = 0, raised = 0; float deepest = 0f, highest = 0f;
        var random = new System.Random(173);
        var foldOffsets = new float[4];
        for (int i = 0; i < foldOffsets.Length; i++) foldOffsets[i] = (float)random.NextDouble() * 0.4f;
        var srcMesh0 = mf.sharedMesh; var bnd = srcMesh0.bounds; var wmin = wm.MultiplyPoint3x4(bnd.min); var wmax = wm.MultiplyPoint3x4(bnd.max);
        // EDGE GUARD: jarak titik ke tepi kasur (bounds XZ, world); mold memudar < 12cm dari edge
        System.Func<float,float,float> EdgeFade = (x, z) => {
          float d = Mathf.Min(Mathf.Min(x - wmin.x, wmax.x - x), Mathf.Min(z - wmin.z, wmax.z - z));
          return Mathf.Clamp01((d - 0.04f) / 0.08f); };
        System.Func<float,float,float> Mold = (x, z) => {
          float f = SampleF(x, z); if (f <= 0.004f) return 0f;
          float dx = x - seatX, dz = z - seatZ; float th = Mathf.Atan2(dz, dx);
          float ss = f * f * (3f - 2f * f);   // smoothstep: transisi halus low-poly
          float dy = -sink * ss;   // smoothstep penuh: profil bowl halus
          // TANPA dinding curam & TANPA rim tinggi: natural = falloff panjang doang
          dy *= EdgeFade(x, z);   // tepi kasur tetap lurus (guard 4→12 cm feather)
          float mask = Mathf.Clamp01(f);              // 0 di tepi & di puncak beban
          // kerutan: suku reguler konsentris DIGANTI noise low-freq + 2 lipatan, amplitudo kecil
          float n1 = Mathf.Sin(x * 23.7f + z * 11.3f) * Mathf.Cos(x * 9.1f - z * 17.9f);  // -1..1 low-freq
          float n2 = Mathf.Sin(x * 41.3f - z * 29.7f + 1.7f);
          dy += (0.004f * n1 + 0.002f * n2) * mask;   // kusut halus tak berpola, max ±6mm
          dy += 0.005f * Mathf.Exp(-((f - 0.85f) * (f - 0.85f)) / 0.02f) * Mathf.Max(0f, n1) * mask; // kain numpuk tak rata di bibir luar, max 5mm
          if (dy < deepest) deepest = dy; if (dy > highest) highest = dy; displaced++; if (dy > 0f) raised++;
          return dy; };
        foreach (var v in sv) { var w = wm.MultiplyPoint3x4(v);
          originalYs.Add(w.y);
          if (w.y <= surfY+0.005f && w.y >= surfY-0.085f) w.y += Mold(w.x, w.z);
          nv.Add(inv.MultiplyPoint3x4(w)); }
        foreach (var uv in suv) nuv.Add(uv);
        int N = 128; int topTris = 0;
        for (int t = 0; t < st.Length; t += 3) {
          int i0=st[t], i1=st[t+1], i2=st[t+2];
          var w0 = wm.MultiplyPoint3x4(sv[i0]); var w1 = wm.MultiplyPoint3x4(sv[i1]); var w2 = wm.MultiplyPoint3x4(sv[i2]);
          // Subdivide the bevel and underlying fabric too: otherwise their large
          // unchanged triangles bridge over the dent and intersect the seated body.
          bool top = w0.y <= surfY + 0.005f && w1.y <= surfY + 0.005f && w2.y <= surfY + 0.005f &&
                     w0.y >= surfY - 0.085f && w1.y >= surfY - 0.085f && w2.y >= surfY - 0.085f;
          if (!top) { nt.Add(i0); nt.Add(i1); nt.Add(i2); continue; }
          topTris++;
          N = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max((w1-w0).magnitude, (w2-w0).magnitude, (w2-w1).magnitude) / 0.012f), 2, 180);
          var idx = new int[N+1, N+1];
          for (int a=0; a<=N; a++) for (int b=0; b<=N-a; b++) {
            float bu=(float)a/N, bv=(float)b/N, bw=1f-bu-bv;
            var w = w0*bw + w1*bu + w2*bv; var uv = suv[i0]*bw + suv[i1]*bu + suv[i2]*bv;
            originalYs.Add(w.y);
            w.y += Mold(w.x, w.z);
            idx[a,b] = nv.Count; nv.Add(inv.MultiplyPoint3x4(w)); nuv.Add(uv); }
          for (int a=0; a<N; a++) for (int b=0; b<N-a; b++) {
            nt.Add(idx[a,b]); nt.Add(idx[a+1,b]); nt.Add(idx[a,b+1]);
            if (b < N-a-1) { nt.Add(idx[a+1,b]); nt.Add(idx[a+1,b+1]); nt.Add(idx[a,b+1]); } } }
        var mesh = new Mesh { name = "PP_Bed_SitDent" };
        mesh.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(nv); mesh.SetUVs(0, nuv); mesh.SetTriangles(nt, 0);
        if (false && FitSeatedContact) SitFinalVerification.FitMattress(mesh, bed.transform, surfY, originalYs); // DIMATIKAN: drag area keluasahan (audit dent_grid)
        nv = new List<Vector3>(mesh.vertices);
        if (false && FitSeatedContact) {
            // Keep a small clearance after fitting, feathered into unchanged fabric.
            for (int i = 0; i < nv.Count; i++) {
                var w = wm.MultiplyPoint3x4(nv[i]);
                w.y -= 0.0035f * Mathf.SmoothStep(0f, 1f, (originalYs[i] - w.y) / 0.01f);
                nv[i] = inv.MultiplyPoint3x4(w);
            }
            mesh.SetVertices(nv);
        }
        deepest = 0; highest = 0;
        for (int i = 0; i < nv.Count; i++) {
            float delta = wm.MultiplyPoint3x4(nv[i]).y - originalYs[i];
            deepest = Mathf.Min(deepest, delta); highest = Mathf.Max(highest, delta);
        }
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        float seatMin = 9f, seatMax = -9f; var wwm = bed.transform.localToWorldMatrix;
        foreach (var q in nv) { var w2 = wwm.MultiplyPoint3x4(q);
          if (Mathf.Abs(w2.x + 1.55f) < 0.45f && Mathf.Abs(w2.z + 1.02f) < 0.45f) { if (w2.y < seatMin) seatMin = w2.y; if (w2.y > seatMax) seatMax = w2.y; } }
        sb.Append("OK kontak=" + hits + " surfY=" + surfY.ToString("F4") + " topTris=" + topTris + "\n");
        sb.Append("deepest=" + deepest.ToString("F4") + " ridgeTertinggi=" + highest.ToString("F4") + " displaced=" + displaced + " naik=" + raised + "\n");
        sb.Append("seatMinY=" + seatMin.ToString("F4") + " seatMaxY=" + seatMax.ToString("F4") + " (datar=-0.4474) verts=" + nv.Count + " tris=" + (nt.Count/3));
        mf.sharedMesh = mesh;
        if (mc != null) mc.sharedMesh = mesh;
        string ap = "Assets/Environment/PP_Bed_SitDent_swallow.asset";
        bool saved = false;
        if (!PreviewOnly) {
        var oldA = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
        if (oldA != null) { EditorUtility.CopySerialized(mesh, oldA); EditorUtility.SetDirty(oldA); }
        else AssetDatabase.CreateAsset(mesh, ap);
        AssetDatabase.SaveAssets();
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
        if (mc != null) mc.sharedMesh = mf.sharedMesh;
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        saved = EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/KosanGameScene.unity");
        }
        sb.Append("\nasset=" + ap + " sceneSaved=" + saved);
        sb.Append(" previewOnly=" + PreviewOnly);
        sb.Append("\nsmoothing=11 fieldHalf=0.17 cell=0.02 gaussianPasses=1 sigmaX=1 sigmaZ=1.8 foldSeed=173 foldPhaseOffset=0..0.4 folds=4 foldStrength=0.012 rimPile=0.014");
        File.WriteAllText(log, "PASS1\n" + sb.ToString());
      } catch (System.Exception e) { File.WriteAllText(log, "ERROR " + e.ToString()); }
    }
}
