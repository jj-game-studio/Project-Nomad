using Nomad.Monsters.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class MonsterDebugSetup
    {
        public const string Path = "Assets/Nomad/Monsters/MonsterDebug.unity";

        public static void Apply()
        {
            EditorSceneManager.OpenScene("Assets/Nomad/Monsters/MonsterElite.unity");
            GameObject controls = Object.FindFirstObjectByType<MonsterTestPanel>().gameObject;
            if (controls.GetComponent<MonsterDebugArena>() == null) controls.AddComponent<MonsterDebugArena>();
            var data = new SerializedObject(controls.GetComponent<MonsterTestPanel>());
            data.FindProperty("appearances").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Nomad.Monsters.MonsterAppearanceSet>(
                "Assets/ThirdParty/Nomad/Monsters/Skeletons/SkeletonAppearances.asset");
            data.ApplyModifiedPropertiesWithoutUndo();
            GameObject.Find("Floor").transform.localScale = new Vector3(38f, 0.5f, 42f);
            GameObject.Find("Floor").transform.position = new Vector3(0f, -0.25f, 8f);
            GameObject.Find("Player").transform.position = new Vector3(0f, 0.05f, -5f);
            Object.DestroyImmediate(Camera.main.GetComponent<Nomad.Player.TopDownCamera>());
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 21f;
            Camera.main.transform.SetPositionAndRotation(new Vector3(0f, 30f, -14f), Quaternion.LookRotation(new Vector3(0f, -30f, 22f)));
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), Path);
            AssetDatabase.SaveAssets();
            Debug.Log("MONSTER_DEBUG_SETUP_PASS");
        }

        [MenuItem("Nomad/Monsters/Open Monster Debug")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(Path);
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.EnterPlaymode();
            };
        }
    }
}
