using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DuckovCustomModelTools
{
    public static class BuildModDll
    {
        public static string GenerateMod(string name, string displayName, string description, string outputPath)
        {
            if (string.IsNullOrEmpty(name))
            {
                EditorUtility.DisplayDialog("错误", "DLL 名称不能为空。", "确定");
                return null;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                EditorUtility.DisplayDialog("错误", "输出路径不能为空。", "确定");
                return null;
            }

            var gamePath = GamePathSettings.Instance.GameInstallPath;
            if (string.IsNullOrEmpty(gamePath))
            {
                EditorUtility.DisplayDialog("错误", "请先设置游戏安装路径。", "确定");
                return null;
            }

            var tempProjectPath = Path.Combine(Application.temporaryCachePath, $"ModBuild_{name}");
            if (Directory.Exists(tempProjectPath)) Directory.Delete(tempProjectPath, true);

            Directory.CreateDirectory(tempProjectPath);

            try
            {
                GenerateModBehaviour(name, tempProjectPath);
                GenerateCsproj(name, gamePath, tempProjectPath);
                GenerateInfoIni(name, displayName, description, tempProjectPath);

                var logPath = CompileProject(tempProjectPath, outputPath, name);
                if (!string.IsNullOrEmpty(logPath)) return logPath;

                CopyInfoIni(tempProjectPath, outputPath, name);

                Directory.Delete(tempProjectPath, true);

                PreventUnityImport(outputPath, name);
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("成功", $"Mod DLL 生成成功！\n输出路径: {outputPath}", "确定");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"生成 Mod DLL 时出错: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("错误", $"生成 Mod DLL 时出错: {ex.Message}", "确定");
                return null;
            }
        }

        private static void GenerateModBehaviour(string name, string projectPath)
        {
            var code = $@"using System.IO;
using UnityEngine;

namespace {name}
{{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {{
        public static string ModDirectory => Path.GetDirectoryName(typeof(ModBehaviour).Assembly.Location)!;
        public static string ModelDirectory => $""{{Application.dataPath}}/../ModConfigs/DuckovCustomModel/Models"";

        private void OnEnable()
        {{
            CreateModelDirectoryIfNeeded();
            CopyModels();
        }}

        private static void CreateModelDirectoryIfNeeded()
        {{
            if (Directory.Exists(ModelDirectory)) return;
            Directory.CreateDirectory(ModelDirectory);
        }}

        private static void CopyModels()
        {{
            var sourceDir = Path.Combine(ModDirectory, ""Models"");
            CopyFolder(sourceDir, ModelDirectory);
        }}

        private static void CopyFolder(string sourceDir, string destDir)
        {{
            if (!Directory.Exists(sourceDir)) return;
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            foreach (var filePath in Directory.GetFiles(sourceDir))
            {{
                var fileName = Path.GetFileName(filePath);
                var destFilePath = Path.Combine(destDir, fileName);
                File.Copy(filePath, destFilePath, true);
            }}

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {{
                var dirName = Path.GetFileName(directory);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyFolder(directory, destSubDir);
            }}
        }}
    }}
}}";

            var filePath = Path.Combine(projectPath, "ModBehaviour.cs");
            File.WriteAllText(filePath, code, Encoding.UTF8);
        }

        private static void GenerateCsproj(string name, string gamePath, string projectPath)
        {
            var duckovPath = gamePath.Replace('/', '\\');
            var csproj = $@"<Project Sdk=""Microsoft.NET.Sdk"">

    <PropertyGroup>
        <TargetFramework>netstandard2.1</TargetFramework>
        <Nullable>enable</Nullable>
        <LangVersion>latest</LangVersion>
    </PropertyGroup>

    <PropertyGroup>
        <DuckovPath>{duckovPath}</DuckovPath>
    </PropertyGroup>

    <ItemGroup>
        <Reference Include=""$(DuckovPath)\Duckov_Data\Managed\TeamSoda.*"">
            <Private>False</Private>
        </Reference>
        <Reference Include=""$(DuckovPath)\Duckov_Data\Managed\Unity*"">
            <Private>False</Private>
        </Reference>
        <Reference Include=""$(DuckovPath)\Duckov_Data\Managed\Newtonsoft.Json.dll"">
            <Private>False</Private>
        </Reference>
    </ItemGroup>

    <ItemGroup>
        <None Update=""info.ini"">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
    </ItemGroup>

</Project>";

            var filePath = Path.Combine(projectPath, $"{name}.csproj");
            File.WriteAllText(filePath, csproj, Encoding.UTF8);
        }

        private static string EscapeIniValue(string value)
        {
            return string.IsNullOrEmpty(value)
                ? value
                : value.Replace("\\", @"\\").Replace(" ", "\\ ").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static void GenerateInfoIni(string name, string displayName, string description, string projectPath)
        {
            var escapedName = EscapeIniValue(name);
            var info = $"name = {escapedName}\n";
            if (!string.IsNullOrEmpty(displayName))
            {
                var escapedDisplayName = EscapeIniValue(displayName);
                info += $"displayName = {escapedDisplayName}\n";
            }

            if (!string.IsNullOrEmpty(description))
            {
                var escapedDescription = EscapeIniValue(description);
                info += $"description = {escapedDescription}\n";
            }

            var filePath = Path.Combine(projectPath, "info.ini");
            File.WriteAllText(filePath, info, Encoding.UTF8);
        }

        private static string CompileProject(string projectPath, string outputPath, string name)
        {
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

            var gamePath = GamePathSettings.Instance.GameInstallPath;
            var duckovPath = gamePath.Replace('/', '\\');
            var managedPath = Path.Combine(duckovPath, "Duckov_Data", "Managed");

            var sourceFile = Path.Combine(projectPath, "ModBehaviour.cs");
            var dllPath = Path.Combine(outputPath, $"{name}.dll");
            var logPath = Path.Combine(Application.temporaryCachePath, $"ModBuild_{name}_compile.log");

            var compilerInfo = GetUnityCompilerPath();
            if (compilerInfo == null)
            {
                const string errorMsg = "无法找到 Unity 编译器。";
                File.WriteAllText(logPath, errorMsg, Encoding.UTF8);
                Debug.LogError(errorMsg);
                return logPath;
            }

            var references = GetReferences(managedPath);
            var referenceArgs = string.Join(" ", references.Select(r => $"/reference:\"{r}\""));

            var args =
                $"/target:library /out:\"{dllPath}\" /debug+ /debug:full /optimize- /nologo {referenceArgs} \"{sourceFile}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = compilerInfo.FileName,
                Arguments = compilerInfo.UseDotnet
                    ? $"exec \"{compilerInfo.CompilerPath}\" {args}"
                    : args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            };

            var logContent = new StringBuilder();
            logContent.AppendLine($"编译命令: {compilerInfo.FileName} {processStartInfo.Arguments}");
            logContent.AppendLine(new('=', 80));

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                const string errorMsg = "无法启动 Unity 编译器进程。";
                logContent.AppendLine(errorMsg);
                File.WriteAllText(logPath, logContent.ToString(), Encoding.UTF8);
                Debug.LogError(errorMsg);
                return logPath;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            logContent.AppendLine("标准输出:");
            logContent.AppendLine(output);
            logContent.AppendLine();
            logContent.AppendLine("标准错误:");
            logContent.AppendLine(error);
            logContent.AppendLine();
            logContent.AppendLine($"退出代码: {process.ExitCode}");

            File.WriteAllText(logPath, logContent.ToString(), Encoding.UTF8);

            if (process.ExitCode != 0) return logPath;

            if (File.Exists(dllPath)) return null;

            logContent.AppendLine();
            logContent.AppendLine($"错误: DLL 文件未生成: {dllPath}");
            File.WriteAllText(logPath, logContent.ToString(), Encoding.UTF8);
            Debug.LogError($"DLL 文件未生成: {dllPath}");
            return logPath;
        }

        private static CompilerInfo GetUnityCompilerPath()
        {
            var editorPath = EditorApplication.applicationContentsPath;

            var cscDllPath = Path.Combine(editorPath, "DotNetSdkRoslyn", "csc.dll");
            var dotnetExePath = Path.Combine(editorPath, "NetCoreRuntime", "dotnet.exe");
            if (File.Exists(cscDllPath) && File.Exists(dotnetExePath))
                return new()
                {
                    FileName = dotnetExePath,
                    CompilerPath = cscDllPath,
                    UseDotnet = true,
                };

            var mcsPaths = new[]
            {
                Path.Combine(editorPath, "MonoBleedingEdge", "bin", "mcs.exe"),
                Path.Combine(editorPath, "MonoBleedingEdge", "bin", "mcs.bat"),
                Path.Combine(editorPath, "MonoBleedingEdge", "bin", "mcs"),
            };

            var mcsPath = mcsPaths.FirstOrDefault(File.Exists);
            if (mcsPath != null)
                return new()
                {
                    FileName = mcsPath,
                    CompilerPath = mcsPath,
                    UseDotnet = false,
                };

            return null;
        }

        private static List<string> GetReferences(string managedPath)
        {
            var references = new List<string>();

            if (!Directory.Exists(managedPath))
            {
                Debug.LogError($"游戏 Managed 目录不存在: {managedPath}");
                return references;
            }

            var allDlls = Directory.GetFiles(managedPath, "*.dll", SearchOption.TopDirectoryOnly);
            references.AddRange(allDlls);

            return references.Where(File.Exists).ToList();
        }

        private static void CopyInfoIni(string projectPath, string outputPath, string name)
        {
            var sourceInfoPath = Path.Combine(projectPath, "info.ini");
            var destInfoPath = Path.Combine(outputPath, "info.ini");

            if (File.Exists(sourceInfoPath)) File.Copy(sourceInfoPath, destInfoPath, true);
        }

        private static void PreventUnityImport(string outputPath, string name)
        {
            var dataPath = Application.dataPath.Replace('\\', '/');
            var outputPathNormalized = outputPath.Replace('\\', '/');

            if (!outputPathNormalized.StartsWith(dataPath)) return;

            var dllPath = Path.Combine(outputPath, $"{name}.dll");
            var pdbPath = Path.Combine(outputPath, $"{name}.pdb");

            CreateMetaFile(dllPath);
            CreateMetaFile(pdbPath);
        }

        private static void CreateMetaFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var metaPath = $"{filePath}.meta";
            var metaContent = @"fileFormatVersion: 2
guid: 0000000000000000d000000000000000
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any: 
    second:
      enabled: 0
      settings: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";
            File.WriteAllText(metaPath, metaContent, Encoding.UTF8);
        }

        private class CompilerInfo
        {
            public string CompilerPath;
            public string FileName;
            public bool UseDotnet;
        }
    }
}