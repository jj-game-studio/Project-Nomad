using System.IO;
using System.Linq;
using Nomad.Inventory;
using Nomad.Inventory.Editor;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomad.Equipment.Editor
{
    internal static class EquipmentAuthoring
    {
        internal const string ScenePath = "Assets/Nomad/Equipment/EquipmentDemo.unity";
        internal const string CsvPath = "Assets/Nomad/Equipment/equipment.csv";

        [MenuItem("Nomad/Equipment/Create Equipment Demo")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            ItemCsvImporter.Import(File.ReadAllText(CsvPath), "Assets/Nomad/Equipment/Items");
            // 씬을 여는 동안 미사용 에셋이 언로드되므로 아이템 참조는 그 뒤에 읽습니다.
            EditorSceneManager.OpenScene("Assets/Nomad/Player/A_PlayerCombat.unity");
            var definitions = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Nomad/Equipment/Items" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();

            // 원본 씬을 저장하지 않고 새 장비 전용 사본에만 컴포넌트를 추가합니다.
            var old = Object.FindAnyObjectByType<PlayerCombat>();
            var equipment = old.gameObject.AddComponent<PlayerEquipment>();
            var inventory = equipment.GetComponent<PlayerInventory>();
            var serialized = new SerializedObject(inventory);
            var items = serialized.FindProperty("items");
            items.arraySize = definitions.Length;
            for (int index = 0; index < definitions.Length; index++)
                items.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var combat = old.gameObject.AddComponent<PlayerEquipmentCombat>();
            var source = new SerializedObject(old);
            foreach (string field in new[] { "viewCamera", "muzzle", "projectile" })
                Set(combat, field, source.FindProperty(field).objectReferenceValue);
            Set(combat, "unarmedDefinition", definitions.Single(item => item.ItemId == "demo-unarmed"));
            old.gameObject.AddComponent<EquipmentCombatVisuals>();
            old.gameObject.AddComponent<EquipmentPanel>();

            var demo = new SerializedObject(old.gameObject.AddComponent<EquipmentDemo>());
            var demoItems = demo.FindProperty("items");
            demoItems.arraySize = definitions.Length;
            for (int index = 0; index < definitions.Length; index++)
                demoItems.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            demo.ApplyModifiedPropertiesWithoutUndo();

            foreach (float x in new[] { -0.65f, 0.65f })
            {
                var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                target.name = "Melee Target " + x;
                target.transform.position = new Vector3(x, 1f, 1.5f);
                var targetHealth = target.AddComponent<Nomad.Combat.Health>();
                Set(target.AddComponent<CombatDemoTarget>(), "health", targetHealth);
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        }

        [MenuItem("Nomad/Equipment/Open Equipment Demo")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
