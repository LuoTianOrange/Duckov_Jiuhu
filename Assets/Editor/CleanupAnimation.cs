using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class CleanupAnimation : EditorWindow
{
    private AnimationClip targetClip;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Cleanup Animation")]
    public static void ShowWindow()
    {
        GetWindow<CleanupAnimation>("Cleanup Animation");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.LabelField("动画清理工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetClip = EditorGUILayout.ObjectField("动画文件", targetClip, typeof(AnimationClip), false) as AnimationClip;

        EditorGUILayout.Space();

        if (targetClip != null)
        {
            if (GUILayout.Button("处理动画", GUILayout.Height(30)))
            {
                ProcessAnimation();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void ProcessAnimation()
    {
        if (targetClip == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择动画文件", "确定");
            return;
        }

        AnimationClip newClip = Object.Instantiate(targetClip);
        string originalPath = AssetDatabase.GetAssetPath(targetClip);
        string directory = Path.GetDirectoryName(originalPath);
        string fileName = Path.GetFileNameWithoutExtension(originalPath);
        string extension = Path.GetExtension(originalPath);
        string newPath = Path.Combine(directory, fileName + "_Cleaned" + extension).Replace('\\', '/');

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(newClip);
        int removedCount = 0;

        foreach (var binding in bindings)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(newClip, binding);
            if (curve == null || curve.length == 0)
                continue;

            if (curve.length == 1)
            {
                AnimationUtility.SetEditorCurve(newClip, binding, null);
                removedCount++;
                continue;
            }

            float firstValue = curve.keys[0].value;
            bool hasChange = false;

            for (int i = 1; i < curve.length; i++)
            {
                if (!Mathf.Approximately(curve.keys[i].value, firstValue))
                {
                    hasChange = true;
                    break;
                }
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
