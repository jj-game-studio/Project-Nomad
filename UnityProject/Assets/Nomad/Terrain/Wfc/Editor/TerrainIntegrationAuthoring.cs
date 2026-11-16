using System.IO;
using System.Linq;
using Nomad.Player;
using Nomad.Props;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class TerrainIntegrationAuthoring
    {
        public const string ScenePath = "Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity";
        public static readonly string[] LegacyNames =
            { "Flat", "Stairs", "Raised", "Corner", "Tee", "Cross", "StairsCorner", "StairsTee", "StairsCross" };

        [MenuItem("Nomad/Terrain/Open Integration Test Scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Rebuild();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Nomad/Terrain/Rebuild Integration Test Scene")]
        public static void Rebuild()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            WfcCatalogAuthoring.Create();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var generator = new GameObject("Integrated Terrain Generator").AddComponent<TerrainGenerator>();
            var catalog = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(WfcCatalogAuthoring.CatalogPath);
            generator.ConfigureWfc(catalog, 6, 6, 51, new Bounds(Vector3.zero, new Vector3(400f, 100f, 400f)));

            var template = new GameObject("Integration Prop Template");
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.transform.SetParent(template.transform, false);
            crate.transform.localPosition = Vector3.up * 0.5f;
            template.SetActive(false);
            var scatter = new GameObject("Integrated Props").AddComponent<PropScatter>();
            scatter.Configure(generator, template);
            var player = new GameObject("Integration Player");
            player.SetActive(false);
            var body = player.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = Vector3.up * 0.9f;
            body.stepOffset = 0.3f;
            body.skinWidth = 0.02f;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = Vector3.up * 0.9f;
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Camera camera = Camera.main;
            camera.farClipPlane = 600f;
            Set(player.AddComponent<PlayerMovement>(), "viewCamera", camera);
            Set(camera.gameObject.AddComponent<TopDownCamera>(), "target", player.transform);

            var controls = new GameObject("Terrain Integration Controls").AddComponent<TerrainIntegrationControls>();
            Set(controls, "generator", generator);
            Set(controls, "catalog", catalog);
            Set(controls, "scatter", scatter);
            Set(controls, "propTemplate", template);
            Set(controls, "player", player);
            Set(controls, "viewCamera", camera);
            Set(controls, "status", CreateHud());
            var data = new SerializedObject(controls);
            TerrainPiece[] legacy = LoadLegacy();
            SetArray(data.FindProperty("legacySources"), legacy);
            SetArray(data.FindProperty("modularSources"), new[] { "ModularCross", "ModularTee", "WideStairs" }
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModularTileAuthoring.Output}/{name}.prefab").GetComponent<TerrainPiece>())
                .Concat(FullWidthStairAuthoring.Names.Select(name => AssetDatabase.LoadAssetAtPath<GameObject>($"{WfcCatalogAuthoring.Folder}/{name}.prefab").GetComponent<TerrainPiece>())).ToArray());
            data.ApplyModifiedPropertiesWithoutUndo();
            RenderSettings.ambientLight = Color.gray;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
        }

        // 기존 검토 씬의 객체 ID와 수동 배치를 유지하면서 신규 갤러리 원형만 갱신합니다.
        public static void RefreshTileReferences()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
            var data = new SerializedObject(controls);
            SetArray(data.FindProperty("modularSources"), new[] { "ModularCross", "ModularTee", "WideStairs" }
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModularTileAuthoring.Output}/{name}.prefab").GetComponent<TerrainPiece>())
                .Concat(FullWidthStairAuthoring.Names.Select(name => AssetDatabase.LoadAssetAtPath<GameObject>($"{WfcCatalogAuthoring.Folder}/{name}.prefab").GetComponent<TerrainPiece>())).ToArray());
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        }
        public static TerrainPiece[] LoadLegacy() => LegacyNames.Select(name =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{TerrainPieceAuthoring.Output}/{name}.prefab")?.GetComponent<TerrainPiece>()).ToArray();

        private static UnityEngine.UI.Text CreateHud()
        {
            var canvas = new GameObject("Integration HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(850f, 205f);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.9f);
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var label = new GameObject("Status", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            label.transform.SetParent(panel.transform, false);
            var textRect = (RectTransform)label.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);
            var text = label.GetComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 19;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "TERRAIN INTEGRATION\nPress Play to generate. R new seed / T replay / O overview / L legacy comparison / G tile gallery";
            return text;
        }

        private static void SetArray(SerializedProperty array, TerrainPiece[] values)
        {
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void Set(Object target, string field, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
