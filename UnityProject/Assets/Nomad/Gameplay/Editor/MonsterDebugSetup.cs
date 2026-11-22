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

        [MenuItem("Nomad/Monsters/Rebuild Map Debug From Integration")]
        public static void Rebuild()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Apply();
        }

        public static void Apply()
        {
            const string lab = "Assets/Nomad/Monsters/MonsterAnimationLab.unity";
            if (!System.IO.File.Exists(lab)) AssetDatabase.CopyAsset(Path, lab);
            EditorSceneManager.OpenScene("Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity");
            var controls = Object.FindFirstObjectByType<Nomad.Terrain.TerrainIntegrationControls>();
            var integration = controls.GetComponent<GameplayIntegration>();
            var old = new SerializedObject(integration).FindProperty("diagnosticPanels");
            for (int i = 0; i < old.arraySize; i++)
                if (old.GetArrayElementAtIndex(i).objectReferenceValue is GameObject panelRoot) panelRoot.SetActive(false);
            Object.DestroyImmediate(integration);
            var panel = controls.gameObject.AddComponent<MonsterTestPanel>();
            var data = new SerializedObject(panel);
            data.FindProperty("player").objectReferenceValue = controls.Player.GetComponent<Nomad.Combat.Health>();
            data.FindProperty("compactUi").boolValue = true;
            data.FindProperty("appearances").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Nomad.Monsters.MonsterAppearanceSet>(
                "Assets/ThirdParty/Nomad/Monsters/Skeletons/SkeletonAppearances.asset");
            string folder = "Assets/Nomad/Monsters/Demo/CombatMaterials/";
            data.FindProperty("normalMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(folder + "Monster.mat");
            data.FindProperty("eliteMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(folder + "Elite.mat");
            data.FindProperty("warningMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(folder + "HeavyWarning.mat");
            data.FindProperty("fillMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(folder + "WarningFill.mat");
            data.ApplyModifiedPropertiesWithoutUndo();
            controls.gameObject.AddComponent<MonsterMapDebug>();
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
