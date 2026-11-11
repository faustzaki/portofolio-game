using System.Collections.Generic;
using UnityEngine;

// Kasur empuk: mesh kasur disalin, permukaan atasnya di-subdivide jadi grid rapat, lalu dicetak
// sesuai bentuk badan karakter yang sedang duduk (bukan elips generik).
//
// Cara kerja:
//  1. Ambil mesh karakter yang sedang duduk (SkinnedMeshRenderer.BakeMesh) -> titik-titik badan
//     yang menyentuh kasur (pita kontak di atas titik terendah badan).
//  2. Proyeksikan ke bidang XZ jadi medan kontak, di-blur -> cetakan halus berbentuk badan.
//  3. Vertex kasur digeser turun sebesar SinkDepth * medan, plus bibir kain naik (bulge) + kerutan di tepi cetakan.
//
// Keempukan lewat `softness` (0..1): SinkDepth = softness * maxSinkDepth. Script lain (mis. script duduk)
// baca SinkDepth buat nurunin karakter supaya bokongnya duduk DI DASAR cetakan.
//
// Kenapa harus bentuk mesh: permukaan atas kasur cuma 1-2 segitiga raksasa (vertex terdekat >30 cm dari
// titik duduk) dan patch tempelan di atas permukaan rata selalu ketutup depth test di bagian yang cekung.
//
// Butuh ModelImporter.isReadable = true pada model kasur (data mesh dibaca saat Play).
public class MattressSitDent : MonoBehaviour
{
    [Header("Keempukan")]
    [Range(0f, 1f)] public float softness = 0.6f;    // 0 = keras (nggak ngecetak), 1 = paling empuk
    public float maxSinkDepth = 0.05f;               // turun terdalam saat softness = 1 (meter)

    [Header("Bentuk cetakan")]
    public float bulge = 0.006f;                     // bibir kain naik di tepi cekungan
    public float foldStrength = 0.003f;              // kerutan halus di bibir cetakan
    public float foldCount = 10f;                    // jumlah kerutan keliling
    public float fieldCell = 0.02f;                  // resolusi medan kontak (meter)
    public float fieldHalf = 0.70f;                  // setengah ukuran medan di sekitar titik duduk
    public float contactBandUp = 0.10f;              // vertex badan maksimal sekian meter di atas titik terendah = kontak
    public float contactBandDown = 0.03f;
    public int blurPasses = 7;                       // makin banyak = cetakan makin lembut/nyebar
    public int subdivisions = 120;                   // grid permukaan atas (~1.1 cm antar vertex)

    [Header("Sumber")]
    public string bedObjectName = "PP_Bed";
    public string characterRootName = "Player_Chibi";
    public Vector2 seatCentreXZ = new Vector2(-1.55f, -1.02f);
    public int startFrameDelay = 3;                  // tunggu karakter diposisikan dulu
    public string logPath = "C:/Belajar Coding/Projects/tools/sit_soft/dent_log.txt";

    public float SinkDepth { get; private set; }
    public bool Built { get; private set; }

    private int buildFrame = -1;
    private string lastDiag = "";

    private void LateUpdate()
    {
        if (Built) return;
        if (buildFrame < 0) buildFrame = Time.frameCount + Mathf.Max(0, startFrameDelay);
        if (Time.frameCount < buildFrame) return;
        Build();
        if (!Built)
        {
            // karakter belum kebaca (pose/animator belum jalan) -> coba lagi beberapa frame
            buildFrame = Time.frameCount + 5;
            if (Time.frameCount % 20 == 0) Log("retry frame " + Time.frameCount + " | " + lastDiag);
        }
    }

    // Panggil ulang kalau pose duduk / keempukan berubah
    public void Rebake()
    {
        Built = true;
        Build();
    }

    private void Build()
    {
        GameObject bed = GameObject.Find(bedObjectName);
        if (bed == null) { Log("bed tidak ditemukan"); return; }
        MeshFilter mf = bed.GetComponent<MeshFilter>();
        Mesh src = mf != null ? mf.sharedMesh : null;
        if (src == null) { Log("mesh kasur tidak ada"); return; }

        Vector3[] srcVerts;
        Vector2[] srcUv;
        int[] srcTris;
        try
        {
            srcVerts = src.vertices;
            srcUv = src.uv;
            srcTris = src.triangles;
        }
        catch (System.Exception e)
        {
            Log("mesh kasur TIDAK READABLE di runtime: " + e.Message);
            return;
        }

        Matrix4x4 wm = bed.transform.localToWorldMatrix;
        Matrix4x4 inv = wm.inverse;

        float surfaceY = SurfaceY(srcVerts, wm);
        SinkDepth = Mathf.Clamp01(softness) * maxSinkDepth;

        float[] field = BuildContactField(out int fx, out int fz);
        if (field == null)
        {
            lastDiag = "medan kontak kosong: " + lastDiag;
            Built = false;   // biar dicoba lagi
            return;
        }

        float bandTop = surfaceY + 0.25f;
        float bandBottom = surfaceY - 0.30f;

        var verts = new List<Vector3>(srcVerts.Length + 8192);
        var uvs = new List<Vector2>(srcVerts.Length + 8192);
        var tris = new List<int>(srcTris.Length + 8192);

        int displaced = 0;
        float deepest = 0f;

        // vertex asli (dipakai segitiga non-permukaan: sisi kasur, rangka) - tetap digeser biar nggak ada lompatan
        foreach (var v in srcVerts)
        {
            Vector3 w = wm.MultiplyPoint3x4(v);
            if (w.y <= bandTop && w.y >= bandBottom)
                w.y += Mold(field, fx, fz, w.x, w.z, ref displaced, ref deepest);
            verts.Add(inv.MultiplyPoint3x4(w));
        }
        foreach (var uv in srcUv) uvs.Add(uv);

        int n = Mathf.Max(2, subdivisions);
        int topTris = 0;

        for (int t = 0; t < srcTris.Length; t += 3)
        {
            int i0 = srcTris[t], i1 = srcTris[t + 1], i2 = srcTris[t + 2];
            Vector3 w0 = wm.MultiplyPoint3x4(srcVerts[i0]);
            Vector3 w1 = wm.MultiplyPoint3x4(srcVerts[i1]);
            Vector3 w2 = wm.MultiplyPoint3x4(srcVerts[i2]);
            bool isTopPlane = Mathf.Abs(w0.y - surfaceY) < 0.005f
                           && Mathf.Abs(w1.y - surfaceY) < 0.005f
                           && Mathf.Abs(w2.y - surfaceY) < 0.005f;

            if (!isTopPlane)
            {
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
                continue;
            }

            // segitiga permukaan atas -> subdivide jadi grid rapat, tiap vertex dicetak sesuai medan kontak
            topTris++;
            var idx = new int[n + 1, n + 1];
            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= n - i; j++)
                {
                    float bu = (float)i / n;
                    float bv = (float)j / n;
                    float bw = 1f - bu - bv;
                    Vector3 w = w0 * bw + w1 * bu + w2 * bv;
                    Vector2 uv = srcUv[i0] * bw + srcUv[i1] * bu + srcUv[i2] * bv;
                    w.y += Mold(field, fx, fz, w.x, w.z, ref displaced, ref deepest);
                    idx[i, j] = verts.Count;
                    verts.Add(inv.MultiplyPoint3x4(w));
                    uvs.Add(uv);
                }
            }
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n - i; j++)
                {
                    tris.Add(idx[i, j]); tris.Add(idx[i + 1, j]); tris.Add(idx[i, j + 1]);
                    if (j < n - i - 1)
                        tris.Add(idx[i + 1, j]); tris.Add(idx[i + 1, j + 1]); tris.Add(idx[i, j + 1]);
                }
            }
        }

        Mesh mesh = new Mesh { name = src.name + "_SitDent" };
        mesh.indexFormat = verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;   // mesh BARU; asset aslinya nggak diubah
        Built = true;

        Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "OK softness={0:F2} sinkDepth={1:F4} surfaceY={2:F4} topTris={3} displacedVerts={4} deepestDy={5:F4} field={6}x{7} verts={8} tris={9}",
            softness, SinkDepth, surfaceY, topTris, displaced, deepest, fx, fz, verts.Count, tris.Count / 3));
    }

    // Geser satu vertex: turun sebesar medan kontak (dibentuk badan), plus bibir naik + kerutan di tepinya
    private float Mold(float[] field, int fx, int fz, float x, float z, ref int displaced, ref float deepest)
    {
        float f = SampleField(field, fx, fz, x, z);
        if (f <= 0.002f) return 0f;
        float dy = -SinkDepth * f;
        float ring = 4f * f * (1f - f);   // maksimum di tepi cetakan, nol di dalam & di luar
        float theta = Mathf.Atan2(z - seatCentreXZ.y, x - seatCentreXZ.x);
        dy += (bulge + foldStrength * Mathf.Sin(foldCount * theta)) * ring;
        if (dy < deepest) deepest = dy;
        displaced++;
        return dy;
    }

    // Level permukaan atas kasur di sekitar titik duduk
    private float SurfaceY(Vector3[] verts, Matrix4x4 wm)
    {
        float best = float.MinValue;
        foreach (var v in verts)
        {
            Vector3 w = wm.MultiplyPoint3x4(v);
            if (Mathf.Abs(w.x - seatCentreXZ.x) > 0.45f || Mathf.Abs(w.z - seatCentreXZ.y) > 0.45f) continue;
            if (w.y > best) best = w.y;
        }
        if (best == float.MinValue) best = -0.4474f;
        return best;
    }

    // Medan kontak 0..1 dari mesh karakter yang sedang duduk
    private float[] BuildContactField(out int fx, out int fz)
    {
        fx = Mathf.CeilToInt(fieldHalf * 2f / fieldCell) + 1;
        fz = fx;

        GameObject player = GameObject.Find(characterRootName);
        if (player == null) return null;
        var smrs = player.GetComponentsInChildren<SkinnedMeshRenderer>();
        lastDiag = "player=" + player.name + " smr=" + (smrs == null ? 0 : smrs.Length);
        if (smrs == null || smrs.Length == 0) return null;

        var pts = new List<Vector3>();
        float minY = float.MaxValue;
        foreach (var smr in smrs)
        {
            if (smr.sharedMesh == null) continue;
            var baked = new Mesh();
            smr.BakeMesh(baked);
            Matrix4x4 m = smr.transform.localToWorldMatrix;
            foreach (var v in baked.vertices)
            {
                Vector3 w = m.MultiplyPoint3x4(v);
                pts.Add(w);
                if (w.y < minY) minY = w.y;
            }
            lastDiag += " mesh=" + (smr.sharedMesh == null ? "null" : smr.sharedMesh.name) + " srcVerts=" + (smr.sharedMesh == null ? 0 : smr.sharedMesh.vertexCount) + " bakedVerts=" + baked.vertexCount;
            Object.Destroy(baked);
        }
        if (pts.Count == 0) return null;

        float bandTop = minY + contactBandUp;
        float bandBottom = minY - contactBandDown;
        float x0 = seatCentreXZ.x - fieldHalf;
        float z0 = seatCentreXZ.y - fieldHalf;

        float[] field = new float[fx * fz];
        int hits = 0;
        foreach (var w in pts)
        {
            if (w.y > bandTop || w.y < bandBottom) continue;
            int i = Mathf.RoundToInt((w.x - x0) / fieldCell);
            int j = Mathf.RoundToInt((w.z - z0) / fieldCell);
            if (i < 0 || j < 0 || i >= fx || j >= fz) continue;
            field[i * fz + j] += 1f;
            hits++;
        }
        lastDiag += " minY=" + minY.ToString("F4") + " band=" + bandBottom.ToString("F4") + ".." + bandTop.ToString("F4") + " hits=" + hits + " pts=" + pts.Count;
        if (hits == 0) return null;

        for (int p = 0; p < blurPasses; p++) field = Blur(field, fx, fz);

        float max = 0f;
        for (int k = 0; k < field.Length; k++) if (field[k] > max) max = field[k];
        if (max <= 0f) return null;
        for (int k = 0; k < field.Length; k++) field[k] = Mathf.Clamp01(field[k] / max);
        return field;
    }

    private float[] Blur(float[] src, int fx, int fz)
    {
        float[] dst = new float[src.Length];
        for (int i = 0; i < fx; i++)
        {
            for (int j = 0; j < fz; j++)
            {
                float sum = 0f; int cnt = 0;
                for (int di = -1; di <= 1; di++)
                {
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        int ii = i + di, jj = j + dj;
                        if (ii < 0 || jj < 0 || ii >= fx || jj >= fz) continue;
                        sum += src[ii * fz + jj]; cnt++;
                    }
                }
                dst[i * fz + j] = sum / cnt;
            }
        }
        return dst;
    }

    private float SampleField(float[] field, int fx, int fz, float x, float z)
    {
        float x0 = seatCentreXZ.x - fieldHalf;
        float z0 = seatCentreXZ.y - fieldHalf;
        float fi = (x - x0) / fieldCell;
        float fj = (z - z0) / fieldCell;
        int i = Mathf.FloorToInt(fi), j = Mathf.FloorToInt(fj);
        if (i < 0 || j < 0 || i >= fx - 1 || j >= fz - 1) return 0f;
        float tx = fi - i, tz = fj - j;
        float a = field[i * fz + j], b = field[(i + 1) * fz + j];
        float c = field[i * fz + j + 1], d = field[(i + 1) * fz + j + 1];
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private void Log(string msg)
    {
        Debug.Log("[MattressSitDent] " + msg);
        try
        {
            var dir = System.IO.Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(logPath, msg + "\n");
        }
        catch { }
    }
}
