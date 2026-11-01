using UnityEngine;
using System.IO;

public class GameRecorder : MonoBehaviour
{
    public bool isRecording = false; // Default off so normal play doesn't dump screenshots
    public int recordDurationFrames = 270;
    public int targetFrameRate = 30;
    public string outputFolder = "C:/Belajar Coding/Projects/tools/record_frames";

    private int frameCount = 0;
    private RenderTexture rt;
    private Texture2D tex;

    void Start()
    {
        frameCount = 0;
        if (isRecording)
        {
            Time.captureFramerate = targetFrameRate;
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            rt = new RenderTexture(960, 540, 24);
            tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        }
    }

    void LateUpdate()
    {
        if (!isRecording) return;

        if (frameCount < recordDurationFrames)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                tex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;

                string filename = Path.Combine(outputFolder, $"frame_{frameCount:D4}.png");
                File.WriteAllBytes(filename, tex.EncodeToPNG());
                frameCount++;
            }
        }
        else
        {
            isRecording = false;
            Time.captureFramerate = 0;
            if (rt != null) rt.Release();
            Debug.Log($"[GameRecorder] Recording finished! Captured {frameCount} frames in {outputFolder}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    void OnDestroy()
    {
        if (rt != null) rt.Release();
    }
}
