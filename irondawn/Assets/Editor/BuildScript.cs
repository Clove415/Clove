using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace IronDawn.EditorTools
{
    /// <summary>
    /// 分发版打包工具。菜单 IronDawn → 构建 Windows 64 位版本，
    /// 或者命令行 -executeMethod IronDawn.EditorTools.BuildScript.BuildWindows64 走批处理。
    ///
    /// 产物落在 Build/Win64/（IronDawn.exe + IronDawn_Data/），
    /// 外面那层 zip 与安装包由 tools/package.ps1 生成。
    /// </summary>
    public static class BuildScript
    {
        public const string Windows64Path = "Build/Win64/IronDawn.exe";
        public const string ProductName = "IronDawn";
        public const string Version = "1.0.1";

        [MenuItem("IronDawn/构建 Windows 64 位版本", false, 1)]
        public static void BuildWindows64()
        {
            ApplyPlayerSettings();

            string[] scenes = CollectScenes();
            if (scenes.Length == 0)
            {
                Fail("构建列表里没有启用的场景，先确认 Assets/Scenes/Arena.unity 已在 Build Settings 里");
                return;
            }

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = scenes;
            options.locationPathName = Windows64Path;
            options.target = BuildTarget.StandaloneWindows64;
            options.targetGroup = BuildTargetGroup.Standalone;
            options.options = BuildOptions.None;

            Debug.Log("[Build] 开始构建 " + ProductName + " " + Version + " → " + Windows64Path + "，场景数 " + scenes.Length);

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail("构建失败：" + summary.result + "，错误 " + summary.totalErrors + " 个");
                return;
            }

            Debug.Log("[Build] 构建完成：" + summary.outputPath
                + "，大小 " + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB"
                + "，耗时 " + summary.totalTime.TotalSeconds.ToString("0") + " 秒");
        }

        /// <summary>
        /// 局域网对战需要随时切出去看 IP、也需要同机多开测试，
        /// 所以分发包默认窗口化，而不是独占全屏。
        /// </summary>
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "IronDawn";
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;

            AssetDatabase.SaveAssets();
        }

        private static string[] CollectScenes()
        {
            List<string> scenes = new List<string>();
            EditorBuildSettingsScene[] all = EditorBuildSettings.scenes;

            for (int i = 0; i < all.Length; i++)
                if (all[i].enabled) scenes.Add(all[i].path);

            return scenes.ToArray();
        }

        private static void Fail(string message)
        {
            Debug.LogError("[Build] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
