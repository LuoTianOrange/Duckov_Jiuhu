using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DuckovCustomModelTools
{
    [Flags]
    public enum PlatformFlags
    {
        None = 0,
        Windows = 1,
        Linux = 2,
        Mac = 4,
    }

    /// <summary>
    ///     AssetBundle 打包工具
    /// </summary>
    public class BuildAssetBundle : EditorWindow
    {
        private readonly List<GameObject> _modelPrefabs = new();
        private string _bundleName = "models";

        private Vector2 _scrollPosition;
        private PlatformFlags _selectedPlatforms = PlatformFlags.Windows;

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("AssetBundle 打包工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _bundleName = EditorGUILayout.TextField("Bundle 名称", _bundleName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("选择构建目标平台:", EditorStyles.label);
            _selectedPlatforms = (PlatformFlags)EditorGUILayout.EnumFlagsField(_selectedPlatforms);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("包含的模型预制件:", EditorStyles.label);

            for (var i = 0; i < _modelPrefabs.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _modelPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(_modelPrefabs[i], typeof(GameObject), false);
                if (GUILayout.Button("移除", GUILayout.MaxWidth(60)))
                {
                    _modelPrefabs.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            var uniquePrefabs = _modelPrefabs.Distinct().ToArray();
            if (uniquePrefabs.Length != _modelPrefabs.Count)
            {
                _modelPrefabs.Clear();
                _modelPrefabs.AddRange(uniquePrefabs);
            }

            if (_modelPrefabs.Count == 0 || _modelPrefabs[^1] != null) _modelPrefabs.Add(null);

            EditorGUILayout.Space();
            if (GUILayout.Button("导出模型 Bundle")) ExportModelBundle();

            EditorGUILayout.EndScrollView();
        }

        private void ExportModelBundle()
        {
            var validPrefabs = _modelPrefabs.Where(prefab => prefab != null).ToArray();
            if (validPrefabs.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请至少添加一个有效的模型预制件。", "确定");
                return;
            }

            if (_selectedPlatforms == PlatformFlags.None)
            {
                EditorUtility.DisplayDialog("错误", "请至少选择一个目标平台。", "确定");
                return;
            }

            BuildTarget buildTarget = 0;
            if ((_selectedPlatforms & PlatformFlags.Windows) != 0) buildTarget |= BuildTarget.StandaloneWindows64;
            if ((_selectedPlatforms & PlatformFlags.Linux) != 0) buildTarget |= BuildTarget.StandaloneLinux64;
            if ((_selectedPlatforms & PlatformFlags.Mac) != 0) buildTarget |= BuildTarget.StandaloneOSX;

            var path = EditorUtility.SaveFilePanel("保存模型 Bundle", "",
                string.IsNullOrEmpty(_bundleName) ? "duckov_model_bundle" : _bundleName, "unity3d");
            if (string.IsNullOrEmpty(path)) return;

            var bundleFileName = Path.GetFileName(path);
            var outputDir = Path.Combine(Application.temporaryCachePath, "AssetBundleBuild");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var assetNames = validPrefabs.Select(AssetDatabase.GetAssetPath).ToArray();
            const BuildAssetBundleOptions buildOptions = BuildAssetBundleOptions.ForceRebuildAssetBundle
                                                         | BuildAssetBundleOptions.RecurseDependencies
                                                         | BuildAssetBundleOptions.StrictMode;

            var build = new AssetBundleBuild
            {
                assetBundleName = bundleFileName,
                assetNames = assetNames,
            };

            var bundles = BuildPipeline.BuildAssetBundles(outputDir, new[] { build },
                buildOptions, buildTarget);

            if (bundles == null || bundles.GetAllAssetBundles().Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "模型 Bundle 导出失败。", "确定");
                return;
            }

            var builtBundlePath = Path.Combine(outputDir, bundleFileName);
            if (File.Exists(builtBundlePath)) File.Copy(builtBundlePath, path, true);

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("成功", "模型 Bundle 导出成功！", "确定");
        }

        [MenuItem("Duckov Custom Model/AssetBundle 打包工具")]
        public static void ShowWindow()
        {
            GetWindow<BuildAssetBundle>("AssetBundle 打包工具");
        }
    }
}