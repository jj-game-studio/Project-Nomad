using System;
using System.Linq;
using Nomad.Game;
using UnityEditor;
using UnityEngine;

namespace Nomad.Visibility.Editor
{
    public static class VisibilityGameRegistration
    {
        // 등록 전 모듈의 순서·참조를 보존하고 재실행 중복을 검사합니다.
        public static void RegisterAndVerify()
        {
            GameConfig config = GameConfig.Load();
            GameObject[] before = config.Modules.ToArray();
            Register();

            GameObject[] registered = config.Modules.ToArray();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Nomad/Visibility/PlayerVisibilityModule.prefab");
            if (!registered.Take(before.Length).SequenceEqual(before) ||
                registered.Count(module => module == prefab) != 1)
                throw new InvalidOperationException("Registration changed existing modules or duplicated visibility.");

            string saved = System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(config));
            Register();
            if (!config.Modules.SequenceEqual(registered) ||
                saved != System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(config)))
                throw new InvalidOperationException("Repeated registration changed the config.");

            Debug.Log("VISIBILITY_REGISTRATION_PASS modules=" + registered.Length);
        }

        [MenuItem("Nomad/Visibility/Register Main Game Module")]
        public static void Register()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before registration.");

            GameConfig config = GameConfig.Load();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Nomad/Visibility/PlayerVisibilityModule.prefab");
            if (config == null || prefab == null)
                throw new InvalidOperationException("GameConfig or visibility prefab is missing.");
            if (config.Modules.Contains(prefab)) return;

            // 기존 영속 모듈 순서를 보존하고 시야 프리팹만 한 번 추가합니다.
            var data = new SerializedObject(config);
            SerializedProperty modules = data.FindProperty("modules");
            int index = modules.arraySize;
            modules.InsertArrayElementAtIndex(index);
            modules.GetArrayElementAtIndex(index).objectReferenceValue = prefab;
            data.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("VISIBILITY_GAME_REGISTERED");
        }
    }
}
