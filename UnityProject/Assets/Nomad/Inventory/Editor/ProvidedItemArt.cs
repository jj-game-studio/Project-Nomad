using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nomad.Inventory.Editor
{
    public static class ProvidedItemArt
    {
        private const string Folder = "Assets/Nomad/Inventory/Resources/ItemUi/";

        public static void Apply()
        {
            // 첨부 PNG 원본을 바꾸지 않고 Sprite 영역만 나눕니다. 좌표는 이미지 왼쪽 위 기준입니다.
            Slice("EquipmentSheet", 768, new[]
            {
                Icon("sword", 20, 16, 220, 205), Icon("dagger", 277, 34, 196, 160),
                Icon("greatsword", 532, 8, 221, 213), Icon("katana", 788, 34, 209, 159),
                Icon("arrow", 48, 274, 180, 188), Icon("staff", 277, 272, 200, 193),
                Icon("orb-staff", 531, 270, 199, 195), Icon("bow", 784, 272, 214, 198),
                Icon("shortbow", 16, 526, 220, 215), Icon("longbow", 266, 514, 220, 225),
                Icon("armor", 531, 522, 200, 210), Icon("ore", 788, 524, 220, 210),
            });
            Slice("StyleSheet", 1024, new[]
            {
                Icon("potion", 42, 237, 108, 102), Icon("helmet", 1100, 68, 102, 100),
                Icon("boots", 1298, 74, 103, 92), Icon("accessory", 1413, 74, 105, 91),
                Icon("bag", 34, 925, 80, 69), Icon("wood", 708, 418, 106, 88),
                Icon("cloth", 965, 410, 100, 94), Icon("ring", 343, 582, 99, 103),
                Icon("slot-weapon", 35, 760, 64, 62), Icon("slot-head", 220, 760, 60, 61),
                Icon("slot-body", 309, 760, 64, 61), Icon("slot-legs", 402, 760, 66, 61),
                Icon("slot-feet", 586, 760, 68, 61), Icon("slot-bag", 128, 760, 61, 61),
                Icon("slot-accessory", 680, 760, 62, 61),
            });

            var icons = new Dictionary<string, Sprite>();
            foreach (string sheet in new[] { "EquipmentSheet", "StyleSheet" })
                foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(Folder + sheet + ".png").OfType<Sprite>())
                    icons[sprite.name] = sprite;

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Nomad" }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                string key = Key(item);
                if (key == null || !icons.TryGetValue(key, out Sprite sprite)) continue;
                var data = new SerializedObject(item);
                data.FindProperty("icon").objectReferenceValue = sprite;
                data.ApplyModifiedPropertiesWithoutUndo();
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"PROVIDED_ITEM_ART_APPLIED items={changed} sprites={icons.Count}");
        }

        private static string Key(ItemDefinition item)
        {
            string id = item.ItemId;
            if (id == "health-potion") return "potion";
            if (id == "weapon-arrow") return "arrow";
            if (id.Contains("wood") || id.Contains("log")) return "wood";
            if (id.Contains("cloth")) return "cloth";
            switch (item.EquipmentType)
            {
                case EquipmentType.Weapon:
                    if (item.AttackMode == WeaponAttackMode.Bow)
                        return id.Contains("short") ? "shortbow" : id.Contains("long") ? "longbow" : "bow";
                    if (item.AttackMode == WeaponAttackMode.Staff)
                        return id.Contains("apprentice") ? "orb-staff" : "staff";
                    if (item.AttackMode != WeaponAttackMode.Melee || id == "demo-unarmed") return null;
                    return id.Contains("dagger") || id.Contains("knife") ? "dagger" :
                        id.Contains("great") || id.Contains("dragonfang") ? "greatsword" : id.Contains("rune") ? "katana" : "sword";
                case EquipmentType.Head: return "helmet";
                case EquipmentType.Body: return "armor";
                case EquipmentType.Legs: return "slot-legs";
                case EquipmentType.Feet: return "boots";
                case EquipmentType.Bag: return "bag";
                case EquipmentType.Accessory: return "ring";
                default: return id.Contains("ore") || id.Contains("scrap") || id.Contains("stone") ? "ore" : null;
            }
        }

        private static SpriteMetaData Icon(string name, int x, int y, int width, int height) =>
            new SpriteMetaData { name = name, rect = new Rect(x, y, width, height), alignment = 9, pivot = Vector2.one * 0.5f };

        private static void Slice(string sheet, int height, SpriteMetaData[] icons)
        {
            string path = Folder + sheet + ".png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            for (int index = 0; index < icons.Length; index++)
            {
                Rect rect = icons[index].rect;
                rect.y = height - rect.y - rect.height;
                icons[index].rect = rect;
            }
#pragma warning disable CS0618
            importer.spritesheet = icons;
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }
    }
}
