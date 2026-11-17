using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronDawn.EditorTools
{
    /// <summary>
    /// 生成一个可以直接打开的入口场景。
    /// 场景本身是空的——所有内容由 GameBootstrap 在运行时用代码搭出来，
    /// 这里只负责让工程里有一个"能按 Play"的入口场景，并塞进构建列表。
    /// </summary>
    public static class SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Arena.unity";

        public static void CreateScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject hint = new GameObject("IronDawn Arena (Press Play)");
            hint.transform.position = Vector3.zero;

            string scenesDir = Path.Combine(Application.dataPath, "Scenes");
            Directory.CreateDirectory(scenesDir);

            UnityEngine.SceneManagement.Scene scene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError("[IronDawn] 场景保存失败: " + ScenePath);
                return;
            }

            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IronDawn] 场景已生成: " + ScenePath);
        }
    }
}
