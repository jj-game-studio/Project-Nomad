using System.IO;
using Nomad.Game.Modules;
using Nomad.Inventory;
using Nomad.Shop;
using UnityEditor;
using UnityEngine;

namespace Nomad.Consumables.Editor
{
    public static class HealthPotionAuthoring
    {
        private const string Folder = "Assets/Nomad/Consumables/Resources";
        public const string ItemPath = Folder + "/health-potion.asset";

        [MenuItem("Nomad/Items/Refresh Health Potion")]
        public static void Apply()
        {
            ItemDefinition item = EnsureItem();
            const string path = "Assets/Nomad/Game/Prefabs/PlayerRig.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PlayerInventory inventory = root.GetComponentInChildren<PlayerInventory>();
                var data = new SerializedObject(inventory);
                SerializedProperty entries = data.FindProperty("items");
                bool found = false;
                for (int index = 0; index < entries.arraySize; index++)
                    if (entries.GetArrayElementAtIndex(index).objectReferenceValue == item) found = true;
                if (!found) { entries.InsertArrayElementAtIndex(entries.arraySize); entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = item; }
                data.ApplyModifiedPropertiesWithoutUndo();
                if (!inventory.TryGetComponent<PlayerHealing>(out _)) inventory.gameObject.AddComponent<PlayerHealing>();

                data = new SerializedObject(inventory.GetComponent<StarterKit>());
                entries = data.FindProperty("items");
                found = false;
                for (int index = 0; index < entries.arraySize; index++)
                    if (entries.GetArrayElementAtIndex(index).FindPropertyRelative("itemId").stringValue == PlayerHealing.PotionId) found = true;
                if (!found)
                {
                    entries.InsertArrayElementAtIndex(entries.arraySize);
                    SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    entry.FindPropertyRelative("itemId").stringValue = PlayerHealing.PotionId;
                    entry.FindPropertyRelative("count").intValue = 3;
                    entry.FindPropertyRelative("equip").boolValue = false;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            ShopCatalog shop = AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Nomad/Shop/Data/VillageMarket.asset");
            var catalog = new SerializedObject(shop);
            SerializedProperty offers = catalog.FindProperty("offers");
            bool hasOffer = false;
            for (int index = 0; index < offers.arraySize; index++)
                if (offers.GetArrayElementAtIndex(index).FindPropertyRelative("item").objectReferenceValue == item) hasOffer = true;
            if (!hasOffer)
            {
                offers.InsertArrayElementAtIndex(offers.arraySize);
                SerializedProperty offer = offers.GetArrayElementAtIndex(offers.arraySize - 1);
                offer.FindPropertyRelative("item").objectReferenceValue = item;
                offer.FindPropertyRelative("displayName").stringValue = item.DisplayName;
                offer.FindPropertyRelative("description").stringValue = item.Description;
                offer.FindPropertyRelative("category").enumValueIndex = 2;
                offer.FindPropertyRelative("buyPrice").intValue = 40;
                offer.FindPropertyRelative("sellPrice").intValue = 15;
                offer.FindPropertyRelative("initialStock").intValue = 20;
                offer.FindPropertyRelative("sellOnly").boolValue = false;
                catalog.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("HEALTH_POTION_READY heal=40 starter=3 key=Q stock=20");
        }

        public static ItemDefinition EnsureItem()
        {
            Directory.CreateDirectory(Folder);
            string iconPath = Folder + "/health-potion.png";
            if (!File.Exists(iconPath))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    bool bottle = x >= 6 && x <= 25 && y >= 4 && y <= 22;
                    bool neck = x >= 12 && x <= 19 && y >= 23 && y <= 27;
                    Color color = bottle || neck ? new Color(0.83f, 0.87f, 0.87f) : Color.clear;
                    if (bottle && x >= 8 && x <= 23 && y >= 6 && y <= 18) color = new Color(0.82f, 0.18f, 0.26f);
                    if (neck && y >= 25) color = new Color(0.73f, 0.53f, 0.28f);
                    if (bottle && ((x >= 14 && x <= 17 && y >= 8 && y <= 17) || (x >= 11 && x <= 20 && y >= 11 && y <= 14))) color = Color.white;
                    texture.SetPixel(x, y, color);
                }
                texture.Apply();
                File.WriteAllBytes(iconPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.Refresh();
            var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPath);
            if (item == null) { item = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item, ItemPath); }
            var data = new SerializedObject(item);
            data.FindProperty("itemId").stringValue = PlayerHealing.PotionId;
            data.FindProperty("displayName").stringValue = "체력 회복약";
            data.FindProperty("description").stringValue = "Q로 사용합니다. 체력 40 회복. 체력이 가득 차면 소모하지 않습니다.";
            data.FindProperty("maxStack").intValue = 10;
            data.FindProperty("tier").enumValueIndex = 1;
            data.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            data.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }
    }
}
