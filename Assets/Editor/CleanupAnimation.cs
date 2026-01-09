using System.IO;
using UnityEditor;
using UnityEngine;

public class CleanupAnimation : EditorWindow
{
    private Vector2 scrollPosition;
    private AnimationClip targetClip;

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("动画清理工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetClip = EditorGUILayout.ObjectField("动画文件", targetClip, typeof(AnimationClip), false) as AnimationClip;

        EditorGUILayout.Space();

        if (targetClip != null)
            if (GUILayout.Button("处理动画", GUILayout.Height(30)))
                ProcessAnimation();

        EditorGUILayout.EndScrollView();
    }

    [MenuItem("Tools/Cleanup Animation")]
    public static void ShowWindow()
    {
        GetWindow<CleanupAnimation>("Cleanup Animation");
    }

    private void ProcessAnimation()
    {
        if (targetClip == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择动画文件", "确定");
            return;
        }

        var newClip = Instantiate(targetClip);
        var originalPath = AssetDatabase.GetAssetPath(targetClip);
        var directory = Path.GetDirectoryName(originalPath);
        var fileName = Path.GetFileNameWithoutExtension(originalPath);
        var extension = Path.GetExtension(originalPath);
        var newPath = Path.Combine(directory, fileName + "_Cleaned" + extension).Replace('\\', '/');

        var bindings = AnimationUtility.GetCurveBindings(newClip);
        var removedCount = 0;

        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(newClip, binding);
            if (curve == null || curve.length == 0)
                continue;

            if (curve.length == 1)
            {
                AnimationUtility.SetEditorCurve(newClip, binding, null);
                removedCount++;
                continue;
            }

            var firstValue = curve.keys[0].value;
            var hasChange = false;

            for (var i = 1; i < curve.length; i++)
                if (!Mathf.Approximately(curve.keys[i].value, firstValue))
                {
                    hasChange = true;
                    break;
                }

            if (!hasChange)
            {
                AnimationUtility.SetEditorCurve(newClip, binding, null);
                removedCount++;
            }
        }

        AssetDatabase.CreateAsset(newClip, newPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("处理完成",
            $"已处理动画：{targetClip.name}\n" +
            $"删除了 {removedCount} 个无变化的曲线\n" +
            $"保存为：{newPath}",
            "确定");

        targetClip = null;
    }
}