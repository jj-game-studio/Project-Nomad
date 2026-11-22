using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Terrain;
using Nomad.Terrain.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Loot.Editor
{
    public static class TerrainLootIntegrationSetup
    {
        public static void Apply()
        {
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            ConfigureScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureScene()
        {
            var controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
            GameObject player = controls.Player;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null) inventory = player.AddComponent<PlayerInventory>();
            var data = new SerializedObject(inventory);
            SerializedProperty items = data.FindProperty("items");
            // 상자 임시 내용물(scrap·field-knife·field-vest)을 모두 받을 수 있게 등록합니다.
            items.arraySize = 3;
            items.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Nomad/Inventory/Demo/scrap.asset");
            items.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Nomad/Inventory/Demo/field-knife.asset");
            items.GetArrayElementAtIndex(2).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Nomad/Inventory/Demo/field-vest.asset");
            data.ApplyModifiedPropertiesWithoutUndo();
            if (player.GetComponent<PlayerInteraction>() == null) player.AddComponent<PlayerInteraction>();

            var loot = controls.GetComponent<TerrainLootIntegration>();
            if (loot == null) loot = controls.gameObject.AddComponent<TerrainLootIntegration>();
            if (controls.GetComponent<ChestSpawner>() == null) controls.gameObject.AddComponent<ChestSpawner>();
            Transform canvas = Object.FindAnyObjectByType<Canvas>().transform;
            Transform panel = canvas.Find("Loot Panel");
            if (panel == null)
            {
                panel = new GameObject("Loot Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image)).transform;
                panel.SetParent(canvas, false);
                var rect = (RectTransform)panel;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(16, 16);
                rect.sizeDelta = new Vector2(850, 70);
                panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.9f);
                panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
                var label = new GameObject("Status", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                label.transform.SetParent(panel, false);
                var textRect = (RectTransform)label.transform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(12, 8);
                textRect.offsetMax = new Vector2(-12, -8);
                var text = label.GetComponent<UnityEngine.UI.Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 19;
                text.color = Color.white;
                text.raycastTarget = false;
            }
            var settings = new SerializedObject(loot);
            settings.FindProperty("inventory").objectReferenceValue = inventory;
            settings.FindProperty("status").objectReferenceValue = panel.GetComponentInChildren<UnityEngine.UI.Text>();
            settings.FindProperty("useScatteredCrateSlots").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
