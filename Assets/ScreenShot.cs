using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ScreenShot : MonoBehaviour
{
    public string savePath = "ScreenShot.png";
    public int width = 1920;
    public int height = 1080;
    public bool transparentBackground = true;

    private bool _keyIsDown;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            if (_keyIsDown) return;
            _keyIsDown = true;
            Capture();
        }
        else if (Input.GetKeyUp(KeyCode.F12))
        {
            _keyIsDown = false;
        }
    }

    public void Capture()
    {
        // ReSharper disable once LocalVariableHidesMember
        var camera = GetComponent<Camera>();
        if (camera == null)
        {
            Debug.LogWarning("ScreenShot: Camera component not found");
            return;
        }

        var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);

        var originalTarget = camera.targetTexture;
        var originalClearFlags = camera.clearFlags;
        var originalBackgroundColor = camera.backgroundColor;

        if (transparentBackground)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
        }

        camera.targetTexture = renderTexture;
        camera.Render();
        camera.targetTexture = originalTarget;

        if (transparentBackground)
        {
            camera.clearFlags = originalClearFlags;
            camera.backgroundColor = originalBackgroundColor;
        }

        RenderTexture.active = renderTexture;
        var texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = null;
        var bytes = texture2D.EncodeToPNG();
#if UNITY_EDITOR
        if (Application.isPlaying)
            Destroy(texture2D);
        else
            DestroyImmediate(texture2D);
#else
        Destroy(texture2D);
#endif

        renderTexture.Release();
#if UNITY_EDITOR
        if (Application.isPlaying)
            Destroy(renderTexture);
        else
            DestroyImmediate(renderTexture);
#else
        Destroy(renderTexture);
#endif

        var path = Path.IsPathRooted(savePath) ? savePath : Path.Combine(Application.dataPath, savePath);
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, bytes);
        Debug.Log($"ScreenShot saved to {path}");
#if UNITY_EDITOR
        if (!Application.isPlaying) AssetDatabase.Refresh();
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ScreenShot))]
public class ScreenShotEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (!GUILayout.Button("Capture", GUILayout.Height(30))) return;
        var screenShot = (ScreenShot)target;
        screenShot.Capture();
    }
}
#endif