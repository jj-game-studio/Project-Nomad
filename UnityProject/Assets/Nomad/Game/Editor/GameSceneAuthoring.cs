using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Game.Editor
{
    // GameManager 하나만 있는 메인 게임 씬과 구성 에셋을 만들고 Build Settings에 등록합니다.
    internal static class GameSceneAuthoring
    {
        internal const string ScenePath = "Assets/Nomad/Game/Game.unity";
        internal const string ConfigPath = "Assets/Nomad/Game/Resources/GameConfig.asset";
        internal const string VillagePath = "Assets/Nomad/Village/Village.unity";
        internal const string FieldPath = "Assets/Nomad/Game/Field.unity";

        // 지역 씬 목록입니다. Field는 Build Game Content가 만든 뒤에만 등록합니다.
        internal static readonly string[] Regions = { VillagePath, FieldPath };

        [MenuItem("Nomad/Game/Create Game Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureConfig();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameManager").AddComponent<GameManager>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"GAME_SCENE_CREATED {ScenePath}");
        }

        // Game이 0번이어야 빌드가 메인 게임 씬에서 시작합니다.
        internal static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { ScenePath }.Concat(Regions)
                .Where(File.Exists)
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
        }

        [MenuItem("Nomad/Game/Open Game Scene")]
        public static void Open()
        {
            if (!File.Exists(ScenePath))
            {
                Create();
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        // 이미 있는 구성은 다른 작업이 등록한 모듈을 보존하기 위해 덮어쓰지 않습니다.
        private static void EnsureConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), ConfigPath);
        }
    }
}
