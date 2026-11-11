using UnityEngine;

// Helper capture: sembunyikan renderer karakter setelah frame tertentu biar cetakan kasurnya
// kelihatan tanpa badan yang nutupin. Animasi/pose tetap jalan (BakeMesh tetap baca mesh).
public class HideAfterFrame : MonoBehaviour
{
    public int hideFrame = 30;
    public string targetRootName = "Player_Chibi";
    public string logPath = "";

    private void LateUpdate()
    {
        if (Time.frameCount < hideFrame) return;
        var root = GameObject.Find(targetRootName);
        if (root == null) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>()) if (r.enabled) r.enabled = false;
        enabled = false;
        Debug.Log("[HideAfterFrame] renderer karakter disembunyikan di frame " + Time.frameCount);
        if (!string.IsNullOrEmpty(logPath))
        {
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)); System.IO.File.WriteAllText(logPath, "hidden at frame " + Time.frameCount + "\n"); } catch { }
        }
    }
}
