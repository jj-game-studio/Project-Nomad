using System.IO;
using Nomad.Combat;
using Nomad.Loot;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Player;
using Nomad.Terrain;
using Nomad.Terrain.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class GameplayIntegrationSetup
    {
        private const string PreviewKey = "Nomad.Gameplay.Preview";

        [MenuItem("Nomad/Gameplay/Connect Existing Integration Scene")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            ConfigureScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("GAMEPLAY_INTEGRATION_SETUP_PASS");
        }

        public static void ConfigureScene()
        {
            var controls = Object.FindFirstObjectByType<TerrainIntegrationControls>();
            GameObject player = controls.Player;
            if (player.GetComponent<Health>() == null) player.AddComponent<Health>();
            if (player.GetComponent<SegmentedHealthBar>() == null) player.AddComponent<SegmentedHealthBar>();
            var shooting = player.GetComponent<PlayerCombat>();
            if (shooting == null) shooting = player.AddComponent<PlayerCombat>();
            Transform muzzle = player.transform.Find("Muzzle");
            if (muzzle == null)
            {
                muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(player.transform, false);
            }
            muzzle.localPosition = new Vector3(0f, 1.2f, 0.45f);
            var combat = new SerializedObject(shooting);
            combat.FindProperty("viewCamera").objectReferenceValue = Camera.main;
            combat.FindProperty("muzzle").objectReferenceValue = muzzle;
            combat.FindProperty("projectile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Nomad/Combat/PlayerProjectile.prefab").GetComponent<Projectile>();
            combat.ApplyModifiedPropertiesWithoutUndo();
            Renderer body = player.GetComponentInChildren<Renderer>(true);
            body.sharedMaterial = Material("Player");

            var factory = controls.GetComponent<MonsterActorFactory>();
            if (factory == null) factory = controls.gameObject.AddComponent<MonsterActorFactory>();
            factory.ConfigureMaterials(Material("Monster"), Material("Elite"), Material("HeavyWarning"), Material("WarningFill"));
            EditorUtility.SetDirty(factory);
            var integration = controls.GetComponent<GameplayIntegration>();
            if (integration == null) integration = controls.gameObject.AddComponent<GameplayIntegration>();
            var settings = new SerializedObject(integration);
            SerializedProperty panels = settings.FindProperty("diagnosticPanels");
            panels.arraySize = 2;
            panels.GetArrayElementAtIndex(0).objectReferenceValue = new SerializedObject(controls)
                .FindProperty("status").objectReferenceValue is Component terrainLabel ? terrainLabel.transform.parent.gameObject : null;
            panels.GetArrayElementAtIndex(1).objectReferenceValue = new SerializedObject(controls.GetComponent<TerrainLootIntegration>())
                .FindProperty("status").objectReferenceValue is Component lootLabel ? lootLabel.transform.parent.gameObject : null;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(integration);
        }

        private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(
            $"Assets/Nomad/Monsters/Demo/CombatMaterials/{name}.mat");

        [MenuItem("Nomad/Gameplay/Open Integrated Gameplay")]
        public static void OpenForPlay()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            SessionState.SetBool(PreviewKey, true);
            ResumePreview();
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.EnterPlaymode();
            };
        }

        [InitializeOnLoadMethod]
        private static void ResumePreview()
        {
            EditorApplication.update -= Capture;
            if (SessionState.GetBool(PreviewKey, false)) EditorApplication.update += Capture;
        }

        private static void Capture()
        {
            if (!EditorApplication.isPlaying || Time.time < 1f) return;
            var gameplay = Object.FindFirstObjectByType<GameplayIntegration>();
            if (gameplay == null || !gameplay.Ready) return;
            SessionState.SetBool(PreviewKey, false);
            EditorApplication.update -= Capture;
            Directory.CreateDirectory("Artifacts");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/integrated-gameplay-player.png"));
            Debug.Log("INTEGRATED_GAMEPLAY_READY");
        }
    }
}
