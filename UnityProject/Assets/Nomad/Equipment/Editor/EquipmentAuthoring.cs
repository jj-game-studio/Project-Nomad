using System.Linq;
using Nomad.Combat;
using Nomad.Inventory;
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

            WeaponContentAuthoring.Import();
            // 씬을 여는 동안 미사용 에셋이 언로드되므로 아이템 참조는 그 뒤에 읽습니다.
            EditorSceneManager.OpenScene("Assets/Nomad/Player/A_PlayerCombat.unity");
            // 원본 씬을 저장하지 않고 새 장비 전용 사본에만 컴포넌트를 추가합니다.
            var old = Object.FindAnyObjectByType<PlayerCombat>();
            old.gameObject.AddComponent<EquipmentDemo>();
            ConfigurePlayer(old);

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

        internal static void UpdateWeaponReferences()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigurePlayer(Object.FindAnyObjectByType<PlayerCombat>());
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigurePlayer(PlayerCombat legacy)
        {
            var source = new SerializedObject(legacy);
            WeaponContentAuthoring.ConfigureCombat(legacy.gameObject, (Camera)source.FindProperty("viewCamera").objectReferenceValue,
                (Transform)source.FindProperty("muzzle").objectReferenceValue,
                (Projectile)source.FindProperty("projectile").objectReferenceValue);

            ItemDefinition[] definitions = WeaponContentAuthoring.Definitions().Where(item =>
                item.ItemId.StartsWith("demo-", System.StringComparison.Ordinal) || item.ItemId == PlayerEquipmentCombat.ArrowItemId).ToArray();
            WeaponContentAuthoring.SetItems(new SerializedObject(legacy.GetComponent<PlayerInventory>()), "items", definitions);
            WeaponContentAuthoring.SetItems(new SerializedObject(legacy.GetComponent<EquipmentDemo>()), "items", definitions);
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
