using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nomad.Inventory.Editor
{
    internal static class ItemCsvImporter
    {
        private const string Output = "Assets/Nomad/Inventory/Resources/CsvItems";

        [MenuItem("Nomad/Inventory/Import Item CSV")]
        private static void ImportFile()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("아이템 CSV", "Play를 중지한 뒤 가져오세요.", "확인");
                return;
            }

            string path = EditorUtility.OpenFilePanel("아이템 CSV", "", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                int count = Import(File.ReadAllText(path, new UTF8Encoding(false, true)));
                EditorUtility.DisplayDialog("아이템 CSV", $"{count}개 반영 완료. 새 아이템은 인벤토리 Items 목록에 연결하세요.", "확인");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("아이템 CSV 오류", error.Message, "확인");
            }
        }

        internal static int Import(string text, string output = Output, string searchRoot = "Assets/Nomad")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play 중에는 가져올 수 없습니다.");

            List<ItemCsv.Row> rows = ItemCsv.Parse(text);
            var existing = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { searchRoot }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrWhiteSpace(item.ItemId) || !existing.TryAdd(item.ItemId, item))
                    throw new InvalidOperationException($"기존 아이템 ID가 비어 있거나 중복됩니다. {item.ItemId}");
            }

            // 전체 입력과 쓰기 대상을 먼저 검증해 잘못된 CSV의 부분 반영을 막습니다.
            foreach (ItemCsv.Row row in rows)
            {
                if (existing.TryGetValue(row.Id, out ItemDefinition item))
                {
                    if (item.ItemId != row.Id)
                        throw new InvalidOperationException($"기존 ID의 대소문자를 유지하세요. {item.ItemId}");
                    if (!AssetDatabase.IsOpenForEdit(item))
                        throw new InvalidOperationException($"쓰기 불가능한 에셋입니다. {row.Id}");
                }
                else if (File.Exists($"{output}/{row.Id}.asset") || Directory.Exists($"{output}/{row.Id}.asset"))
                    throw new InvalidOperationException($"생성 경로에 다른 파일이 있습니다. {row.Id}");
            }

            EnsureFolder(output);
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("아이템 CSV 가져오기");
            foreach (ItemCsv.Row row in rows)
            {
                bool created = !existing.TryGetValue(row.Id, out ItemDefinition item);
                if (created)
                    item = ScriptableObject.CreateInstance<ItemDefinition>();
                else
                    Undo.RecordObject(item, "아이템 CSV");

                var serialized = new SerializedObject(item);
                serialized.FindProperty("itemId").stringValue = row.Id;
                serialized.FindProperty("displayName").stringValue = row.Name;
                serialized.FindProperty("description").stringValue = row.Description;
                serialized.FindProperty("maxStack").intValue = row.MaxStack;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (created)
                {
                    AssetDatabase.CreateAsset(item, $"{output}/{row.Id}.asset");
                    Undo.RegisterCreatedObjectUndo(item, "아이템 CSV");
                }
                else
                    EditorUtility.SetDirty(item);
            }

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            return rows.Count;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
