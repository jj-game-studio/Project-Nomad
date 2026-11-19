using System;
using UnityEditor;
using UnityEngine;

namespace Nomad.Inventory.Editor
{
    internal static class ItemCsvVerification
    {
        private const string Root = "Assets/Nomad/Inventory/Editor/CsvVerificationTemporary";
        private const string Header = "item_id,display_name,description,max_stack\n";
        private static int checks;

        public static void Run()
        {
            bool ownsFixture = false;
            try
            {
                if (AssetDatabase.IsValidFolder(Root))
                    throw new InvalidOperationException("이전 임시 검사 폴더를 확인하세요.");

                AssetDatabase.CreateFolder("Assets/Nomad/Inventory/Editor", "CsvVerificationTemporary");
                ownsFixture = true;

                var rows = ItemCsv.Parse("\uFEFF" + Header + "test,검," + "\"쉼표, 큰따옴표 \"\"검\"\"\n둘째 줄\",1\r\n");
                Check(rows[0].Description == "쉼표, 큰따옴표 \"검\"\n둘째 줄", "한글/BOM/쉼표/인용/개행");
                foreach (string bad in new[] {
                    Header, Header + "a,검,,0", Header + "a,검,,2147483648",
                    Header + "../a,검,,1", Header + "a,,,1",
                    Header + "a,검,,1\na,검,,1", Header + "a,검,,1\nA,검,,1",
                    Header + "a,\"검,,1", Header + "a,검\",,1", Header + "a,\"검\"x,,1",
                    Header + "a,검,1", Header + "a,검,,1,extra", "bad\na,검,,1" })
                    Reject(() => ItemCsv.Parse(bad), "잘못된 CSV");

                Check(ItemCsvImporter.Import(Header + "test,첫 이름,설명,1", Root, Root) == 1, "새 에셋 생성");
                string path = Root + "/test.asset";
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                Texture2D texture = new Texture2D(2, 2);
                Sprite icon = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
                AssetDatabase.CreateAsset(texture, Root + "/icon.asset");
                AssetDatabase.AddObjectToAsset(icon, texture);
                var serialized = new SerializedObject(item);
                serialized.FindProperty("icon").objectReferenceValue = icon;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                ItemCsvImporter.Import(Header + "test,둘째 이름,새 설명,3", Root, Root);
                Check(item == AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) &&
                    guid == AssetDatabase.AssetPathToGUID(path), "객체/GUID 유지");
                Check(item.Icon == icon && item.DisplayName == "둘째 이름" &&
                    item.Description == "새 설명" && item.MaxStack == 3, "아이콘 유지/문구·중첩 반영");

                ItemCsvImporter.Import(Header + "test,둘째 이름,새 설명,3", Root, Root);
                Check(AssetDatabase.FindAssets("t:ItemDefinition", new[] { Root }).Length == 1, "재가져오기 중복 없음");
                Reject(() => ItemCsvImporter.Import(Header + "test,손상,,1\nbad,오류,,0", Root, Root), "전체 검증");
                Check(item.DisplayName == "둘째 이름" && item.MaxStack == 3, "오류 시 기존 데이터 유지");
                Reject(() => ItemCsvImporter.Import(Header + "TEST,손상,,1", Root, Root), "기존 ID 대소문자 보호");

                var duplicate = UnityEngine.Object.Instantiate(item);
                AssetDatabase.CreateAsset(duplicate, Root + "/duplicate.asset");
                Reject(() => ItemCsvImporter.Import(Header + "test,손상,,1", Root, Root), "기존 에셋 ID 중복");
                Check(item.DisplayName == "둘째 이름", "중복 에셋 오류 무변경");

                AssetDatabase.DeleteAsset(Root);
                Debug.Log($"ITEM_CSV_PASS: {checks} checks");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (ownsFixture)
                    AssetDatabase.DeleteAsset(Root);
                EditorApplication.Exit(1);
            }
        }

        private static void Check(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException(label);
            checks++;
        }

        private static void Reject(Action action, string label)
        {
            try { action(); }
            catch (FormatException) { checks++; return; }
            catch (InvalidOperationException) { checks++; return; }
            throw new Exception(label + ": 거절되지 않음");
        }
    }
}
