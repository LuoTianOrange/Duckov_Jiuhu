using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DuckovCustomModelTools
{
    public class BuildModDllWindow : EditorWindow
    {
        private bool _autoCopyToGame;
        private string _description = "";
        private string _displayName = "";
        private string _lastLogPath = "";
        private string _name = "";
        private string _previewImagePath = "";

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Mod DLL 生成工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _name = EditorGUILayout.TextField("DLL 名称 (Namespace)", _name);
            EditorGUILayout.HelpBox("DLL 名称将同时用作命名空间名称", MessageType.Info);

            EditorGUILayout.Space();

            _displayName = EditorGUILayout.TextField("Mod 显示名称", _displayName);

            EditorGUILayout.Space();

            _description = EditorGUILayout.TextField("Mod 描述", _description);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("预览图 (可选)", EditorStyles.label);
            if (GUILayout.Button("选择文件...", GUILayout.MaxWidth(100)))
            {
                var path = EditorUtility.OpenFilePanel("选择预览图", "", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path)) _previewImagePath = path;
            }

            if (!string.IsNullOrEmpty(_previewImagePath) && GUILayout.Button("清除", GUILayout.MaxWidth(50)))
                _previewImagePath = "";

            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_previewImagePath))
                EditorGUILayout.LabelField(_previewImagePath, EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            EditorGUILayout.Space();

            _autoCopyToGame = EditorGUILayout.Toggle("自动复制到游戏文件夹", _autoCopyToGame);
            if (_autoCopyToGame)
            {
                var modDirectory = GamePathSettings.GetModDirectory();
                if (string.IsNullOrEmpty(modDirectory))
                    EditorGUILayout.HelpBox("请先设置游戏安装路径", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox($"将复制到: {modDirectory}", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_name));
            if (GUILayout.Button("生成 Mod DLL", GUILayout.Height(30))) GenerateMod();
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_lastLogPath) && File.Exists(_lastLogPath))
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("编译失败，请查看日志文件。", MessageType.Error);
                if (GUILayout.Button("打开日志", GUILayout.Width(80))) EditorUtility.RevealInFinder(_lastLogPath);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            var fileList = "生成的文件将包含:\n- DLL 文件\n- PDB 文件\n- info.ini 文件";
            if (!string.IsNullOrEmpty(_previewImagePath)) fileList += "\n- preview.png 文件";
            EditorGUILayout.HelpBox(fileList, MessageType.Info);
        }

        private void GenerateMod()
        {
            var path = EditorUtility.OpenFolderPanel("选择输出目录", "", "");
            if (string.IsNullOrEmpty(path)) return;
            var outputPath = Path.Combine(path, _name).Replace('\\', '/');
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

            _lastLogPath = BuildModDll.GenerateMod(_name, _displayName, _description, outputPath);
            if (!string.IsNullOrEmpty(_lastLogPath)) return;

            if (!string.IsNullOrEmpty(_previewImagePath))
            {
                var destPreviewPath = Path.Combine(outputPath, "preview.png");
                if (!SavePreviewAsPng(destPreviewPath)) EditorUtility.DisplayDialog("警告", "保存预览图时出错。", "确定");
            }

            AssetDatabase.Refresh();

            if (_autoCopyToGame)
            {
                var modDirectory = GamePathSettings.GetModDirectory();
                if (string.IsNullOrEmpty(modDirectory))
                {
                    EditorUtility.DisplayDialog("警告", "请先设置游戏安装路径，无法自动复制到游戏文件夹。", "确定");
                    return;
                }

                var gameModPath = Path.Combine(modDirectory, _name);
                if (!Directory.Exists(gameModPath)) Directory.CreateDirectory(gameModPath);

                var dllPath = Path.Combine(outputPath, $"{_name}.dll");
                var pdbPath = Path.Combine(outputPath, $"{_name}.pdb");
                var infoPath = Path.Combine(outputPath, "info.ini");
                var previewPath = Path.Combine(outputPath, "preview.png");

                if (File.Exists(dllPath))
                    File.Copy(dllPath, Path.Combine(gameModPath, $"{_name}.dll"), true);
                if (File.Exists(pdbPath))
                    File.Copy(pdbPath, Path.Combine(gameModPath, $"{_name}.pdb"), true);
                if (File.Exists(infoPath))
                    File.Copy(infoPath, Path.Combine(gameModPath, "info.ini"), true);
                if (File.Exists(previewPath))
                    File.Copy(previewPath, Path.Combine(gameModPath, "preview.png"), true);

                EditorUtility.DisplayDialog("成功",
                    $"Mod DLL 生成成功！\n已复制到游戏文件夹:\n{gameModPath}", "确定");
            }
        }

        private bool SavePreviewAsPng(string outputPath)
        {
            if (string.IsNullOrEmpty(_previewImagePath) || !File.Exists(_previewImagePath)) return false;

            Texture2D texture = null;
            try
            {
                var fileData = File.ReadAllBytes(_previewImagePath);
                texture = new(2, 2);
                if (!texture.LoadImage(fileData))
                {
                    DestroyImmediate(texture);
                    return false;
                }

                var pngData = texture.EncodeToPNG();
                if (pngData == null || pngData.Length == 0)
                {
                    DestroyImmediate(texture);
                    return false;
                }

                File.WriteAllBytes(outputPath, pngData);
                DestroyImmediate(texture);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"保存预览图为PNG时出错: {ex.Message}");
                if (texture != null) DestroyImmediate(texture);

                return false;
            }
        }

        [MenuItem("Duckov Custom Model/生成 Mod DLL")]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildModDllWindow>("生成 Mod DLL");
            window.Show();
        }
    }
}