using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class SitLockVerify : MonoBehaviour
{
    private void Start()
    {
        Time.captureFramerate = 30;
        Application.runInBackground = true;

        foreach (MonoBehaviour component in GetComponentsInParent<MonoBehaviour>())
        {
            string typeName = component.GetType().Name;
            if (typeName == "PlayerController" || typeName == "ArmSwing")
                component.enabled = false;
        }

        CharacterController controller = GetComponentInParent<CharacterController>();
        if (controller != null) controller.enabled = false;

        Animator anim = GetComponentInParent<Animator>();
        SkinnedMeshRenderer smr = GetComponentInParent<SkinnedMeshRenderer>();
        if (smr == null) smr = GetComponentInChildren<SkinnedMeshRenderer>();
        Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        Mesh probe = new Mesh();
        anim.SetBool("IsSitting", true);
        float deepest = 0f;
        for (int k = 0; k < 6; k++)
        {
            anim.Play("Sit", 0, 0.1f + k * 0.15f);
            anim.Update(0f);
            float b = LowestSkinnedY(smr, probe, hips.position, 0.28f);
            if (b < deepest) deepest = b;
        }

        float buttOffset = deepest - transform.position.y;
        float rootY = -0.447f - buttOffset;
        transform.position = new Vector3(-1.55f, rootY, -1.00f);
        transform.rotation = Quaternion.LookRotation(Vector3.forward);
        anim.Play("Sit", 0, 0f);
        anim.Update(0f);
        Physics.SyncTransforms();

        GameObject bed = GameObject.Find("PP_Bed");
        Collider bedCollider = bed.GetComponent<Collider>();
        smr.BakeMesh(probe, true);
        Vector3[] vertices = probe.vertices;
        Matrix4x4 matrix = smr.transform.localToWorldMatrix;
        int vertsChecked = 0, vertsBelowSurface = 0, vertsNearSurface = 0;
        float maxVerticalPen = 0f, minDist = float.PositiveInfinity;
        foreach (Vector3 vertex in vertices)
        {
            Vector3 w = matrix.MultiplyPoint3x4(vertex);
            if (w.y >= -0.30f) continue;
            vertsChecked++;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(w.x, -0.30f, w.z), Vector3.down, out hit, 1.0f) && hit.collider.gameObject.name == "PP_Bed")
            {
                float verticalPen = hit.point.y - w.y;
                if (verticalPen > 0.0005f) vertsBelowSurface++;
                if (verticalPen > maxVerticalPen) maxVerticalPen = verticalPen;
            }
            Vector3 cp = bedCollider.ClosestPoint(w);
            float dist = Vector3.Distance(w, cp);
            if (dist < 0.002f) vertsNearSurface++;
            if (dist < minDist) minDist = dist;
        }
        if (vertsChecked == 0) minDist = 0f;

        string directory = "C:/Belajar Coding/Projects/tools/sit_lock2/";
        Directory.CreateDirectory(directory);
        string sidePath = directory + "side_ortho.png";
        string closeupPath = directory + "closeup.png";
        RenderView(sidePath, 900, 600, new Vector3(-0.15f, -0.42f, -1.00f), Quaternion.LookRotation(new Vector3(-1f, 0f, 0f), Vector3.up), true);
        Vector3 closePosition = new Vector3(-0.60f, 0.60f, 0.85f);
        RenderView(closeupPath, 960, 540, closePosition, Quaternion.LookRotation(new Vector3(-1.55f, -0.447f + 0.25f, -1.00f) - closePosition, Vector3.up), false);

        CultureInfo culture = CultureInfo.InvariantCulture;
        string log = string.Format(culture, "buttOffset={0:F4}\nrootY={1:F4}\nfinal position=({2:F4}, {3:F4}, {4:F4})\nVERIFY rootY={1:F4} vertsChecked={5} vertsBelowSurface={6} maxVerticalPen={7:F4} vertsNearSurface={8} minDist={9:F4} sidePng={10} closeupPng={11}\n", buttOffset, rootY, transform.position.x, transform.position.y, transform.position.z, vertsChecked, vertsBelowSurface, maxVerticalPen, vertsNearSurface, minDist, sidePath, closeupPath);
        File.WriteAllText(directory + "log.txt", log);
        Destroy(probe);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private static float LowestSkinnedY(SkinnedMeshRenderer smr, Mesh probe, Vector3 point, float radius)
    {
        smr.BakeMesh(probe, true);
        Matrix4x4 matrix = smr.transform.localToWorldMatrix;
        float lowest = float.PositiveInfinity;
        float radiusSquared = radius * radius;
        foreach (Vector3 vertex in probe.vertices)
        {
            Vector3 w = matrix.MultiplyPoint3x4(vertex);
            float dx = w.x - point.x;
            float dz = w.z - point.z;
            if (dx * dx + dz * dz <= radiusSquared && w.y < lowest) lowest = w.y;
        }
        return float.IsPositiveInfinity(lowest) ? point.y : lowest;
    }

    private static void RenderView(string path, int width, int height, Vector3 position, Quaternion rotation, bool orthographic)
    {
        GameObject cameraObject = new GameObject("SitLockVerifyCamera");
        Camera cam = cameraObject.AddComponent<Camera>();
        cam.orthographic = orthographic;
        if (orthographic)
        {
            cam.orthographicSize = 0.45f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
        }
        else cam.fieldOfView = 40f;
        cam.transform.position = position;
        cam.transform.rotation = rotation;
        RenderTexture rt = new RenderTexture(width, height, 24);
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        rt.Release();
        Destroy(tex);
        Destroy(rt);
        Destroy(cameraObject);
    }
}
