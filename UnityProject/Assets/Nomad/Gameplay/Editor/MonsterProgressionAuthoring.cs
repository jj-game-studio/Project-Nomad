using System;
using System.Linq;
using Nomad.Inventory;
using Nomad.Monsters.Demo;
using UnityEditor;
using UnityEngine;

namespace Nomad.Gameplay.Editor
{
    public static class MonsterProgressionAuthoring
    {
        public const string TablePath = "Assets/Nomad/Gameplay/Resources/MonsterEquipmentDrops.asset";

        [MenuItem("Nomad/Monsters/Refresh Equipment Drop Table")]
        public static void Build()
        {
            ItemDefinition[] items = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Nomad/Equipment/Items" })
                .Select(id => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(id)))
                .Where(item => item.EquipmentType != EquipmentType.None && !item.ItemId.StartsWith("demo-") && item.MaxStack == 1)
                .OrderBy(item => item.AttackPower + item.Defense).ThenBy(item => item.ItemId).ToArray();
            if (items.Length == 0) throw new InvalidOperationException("드롭할 실제 장비가 없습니다.");
            if (!AssetDatabase.IsValidFolder("Assets/Nomad/Gameplay/Resources"))
                AssetDatabase.CreateFolder("Assets/Nomad/Gameplay", "Resources");
            MonsterEquipmentDropTable table = AssetDatabase.LoadAssetAtPath<MonsterEquipmentDropTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<MonsterEquipmentDropTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            var data = new SerializedObject(table);
            Set(data, "normal", items.Take(Mathf.Max(1, items.Length / 2)).ToArray());
            Set(data, "elite", items.Skip(items.Length / 2).ToArray());
            Set(data, "boss", items.Skip(Mathf.Max(0, items.Length - Mathf.Max(2, items.Length / 3))).ToArray());
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log("MONSTER_DROP_TABLE_READY items=" + items.Length);
        }

        private static void Set(SerializedObject data, string name, ItemDefinition[] items)
        {
            SerializedProperty array = data.FindProperty(name);
            array.arraySize = items.Length;
            for (int index = 0; index < items.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = items[index];
        }
    }
}
