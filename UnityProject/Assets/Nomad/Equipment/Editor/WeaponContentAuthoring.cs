using System;
using System.IO;
using System.Linq;
using Nomad.Combat;
using Nomad.Game.Editor;
using Nomad.Game.Modules;
using Nomad.Gameplay.Editor;
using Nomad.Hud;
using Nomad.Inventory;
using Nomad.Inventory.Editor;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Equipment.Editor
{
    internal static class WeaponContentAuthoring
    {
        internal const string ItemFolder = "Assets/Nomad/Equipment/Items";
        internal const string ArrowPath = "Assets/Nomad/Equipment/PlayerArrow.prefab";
        internal const string VfxResourceFolder = "Assets/Nomad/Equipment/Resources";
        internal const string IconFolder = "Assets/Nomad/Equipment/Icons";
        private const string ModelFolder = "Assets/ThirdParty/Synty_Kingdom/PolygonFantasyKingdom/Prefabs/Weapons";
        private const string ArrowModel = "Assets/ThirdParty/Nomad/Monsters/Candidates/Arrow.prefab";
        private static readonly (string Id, string Model)[] Models =
        {
            ("weapon-dagger", "SM_Wep_Dagger_01"),
            ("demo-sword", "SM_Wep_Sword_01"),
            ("weapon-greatsword", "SM_Wep_Sword_18"),
            ("weapon-shortbow", "SM_Prop_Bow_01"),
            ("demo-bow", "SM_Prop_Bow_02"),
            ("weapon-longbow", "SM_Prop_Bow_Rigged_02"),
            ("weapon-apprentice-staff", "SM_Wep_Staff_01"),
            ("weapon-crystal-staff", "SM_Wep_Staff_04"),
            ("weapon-archmage-staff", "SM_Wep_Staff_08")
        };
        // 검토용 임시 시작 방어구·가방·장신구입니다. 장착하지 않고 가방에 넣어 직접 바꿔 보게 합니다(#160 판단 필요).
        private static readonly string[] StarterGear =
        {
            "gear-leather-cap", "gear-padded-armor", "gear-cloth-pants", "gear-leather-shoes",
            "gear-small-backpack", "gear-hawk-earring", "gear-life-ring"
        };

        [MenuItem("Nomad/Equipment/Update Weapon Content")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Import();
            GameObject rig = PrefabUtility.LoadPrefabContents(GameContentAuthoring.PlayerPrefabPath);
            try
            {
                PlayerEquipmentCombat combat = rig.GetComponentInChildren<PlayerEquipmentCombat>();
                var data = new SerializedObject(combat);
                Configure(combat.gameObject, (Camera)data.FindProperty("viewCamera").objectReferenceValue,
                    (Transform)data.FindProperty("muzzle").objectReferenceValue,
                    (Projectile)data.FindProperty("projectile").objectReferenceValue);
                PrefabUtility.SaveAsPrefabAsset(rig, GameContentAuthoring.PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rig);
            }

            EquipmentAuthoring.UpdateWeaponReferences();
            GameplayIntegrationSetup.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("WEAPON_CONTENT_BUILT");
        }

        internal static void Import()
        {
            GameObject arrow = Load<GameObject>(ArrowModel);
            GameObject[] models = Models.Select(entry => Load<GameObject>($"{ModelFolder}/{entry.Model}.prefab")).ToArray();
            ItemCsvImporter.Import(File.ReadAllText(EquipmentAuthoring.CsvPath), ItemFolder);
            for (int index = 0; index < Models.Length; index++)
            {
                var data = new SerializedObject(Load<ItemDefinition>($"{ItemFolder}/{Models[index].Id}.asset"));
                data.FindProperty("equippedVisual").objectReferenceValue = models[index];
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            AssignIcons();

            var root = new GameObject("PlayerArrow", typeof(Projectile));
            try
            {
                PrefabUtility.InstantiatePrefab(arrow, root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, ArrowPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            CreateVfxMaterial();
            AssetDatabase.SaveAssets();
        }

        // Icons/<item_id>.png가 있는 아이템에 스프라이트 아이콘을 연결합니다. 원본은 Icons~/make_icons.py로 생성합니다.
        private static void AssignIcons()
        {
            AssetDatabase.Refresh();
            foreach (ItemDefinition item in Definitions())
            {
                string path = $"{IconFolder}/{item.ItemId}.png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                    continue;

                if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                var data = new SerializedObject(item);
                data.FindProperty("icon").objectReferenceValue = Load<Sprite>(path);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 무기 VFX 재질입니다. Resources에 두어 플레이어 빌드에도 입자 셰이더가 포함되게 합니다.
        internal static void CreateVfxMaterial()
        {
            string path = $"{VfxResourceFolder}/{WeaponVfx.MaterialResource}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(VfxResourceFolder);
                material = new Material(Shader.Find(WeaponVfx.ShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find(WeaponVfx.ShaderName);
            WeaponVfx.ConfigureAdditive(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        internal static void Configure(GameObject player, Camera camera, Transform muzzle, Projectile projectile)
        {
            PlayerInventory inventory = GetOrAdd<PlayerInventory>(player);
            var bag = new SerializedObject(inventory);
            SerializedProperty catalog = bag.FindProperty("items");
            ItemDefinition[] existing = Enumerable.Range(0, catalog.arraySize)
                .Select(index => (ItemDefinition)catalog.GetArrayElementAtIndex(index).objectReferenceValue).ToArray();
            // CSV에서 지운 아이템은 null로 남으므로 목록에서 뺍니다.
            SetItems(bag, "items", existing.Where(item => item != null).Concat(Definitions()).Distinct().ToArray());

            ConfigureCombat(player, camera, muzzle, projectile);
            GetOrAdd<PlayerHud>(player);

            var kit = new SerializedObject(GetOrAdd<StarterKit>(player));
            SerializedProperty entries = kit.FindProperty("items");
            entries.arraySize = Models.Length + 1 + StarterGear.Length;
            for (int index = 0; index < Models.Length; index++)
            {
                string id = Models[index].Id;
                EquipmentSlot? slot = id == "demo-sword" ? EquipmentSlot.Weapon1 :
                    id == "demo-bow" ? EquipmentSlot.Weapon2 : null;
                SetEntry(entries.GetArrayElementAtIndex(index), id, 1, slot);
            }

            SetEntry(entries.GetArrayElementAtIndex(Models.Length), PlayerEquipmentCombat.ArrowItemId, 30, null);
            for (int index = 0; index < StarterGear.Length; index++)
                SetEntry(entries.GetArrayElementAtIndex(Models.Length + 1 + index), StarterGear[index], 1, null);
            kit.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void ConfigureCombat(GameObject player, Camera camera, Transform muzzle, Projectile projectile)
        {
            GetOrAdd<PlayerEquipment>(player);
            GetOrAdd<PlayerMana>(player);
            GetOrAdd<PlayerStats>(player);
            var combat = new SerializedObject(GetOrAdd<PlayerEquipmentCombat>(player));
            combat.FindProperty("viewCamera").objectReferenceValue = camera;
            combat.FindProperty("muzzle").objectReferenceValue = muzzle;
            combat.FindProperty("projectile").objectReferenceValue = projectile;
            combat.FindProperty("arrowProjectile").objectReferenceValue = Load<GameObject>(ArrowPath).GetComponent<Projectile>();
            combat.FindProperty("unarmedDefinition").objectReferenceValue = Load<ItemDefinition>($"{ItemFolder}/demo-unarmed.asset");
            combat.ApplyModifiedPropertiesWithoutUndo();
            GetOrAdd<EquipmentCombatVisuals>(player);
            GetOrAdd<EquipmentPanel>(player);
        }

        internal static ItemDefinition[] Definitions() => AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemFolder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(item => item.ItemId, StringComparer.Ordinal).ToArray();

        internal static void SetItems(SerializedObject data, string field, ItemDefinition[] items)
        {
            SerializedProperty array = data.FindProperty(field);
            array.arraySize = items.Length;
            for (int index = 0; index < items.Length; index++)
                array.GetArrayElementAtIndex(index).objectReferenceValue = items[index];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEntry(SerializedProperty entry, string id, int count, EquipmentSlot? slot)
        {
            entry.FindPropertyRelative("itemId").stringValue = id;
            entry.FindPropertyRelative("count").intValue = count;
            entry.FindPropertyRelative("equip").boolValue = slot.HasValue;
            entry.FindPropertyRelative("slot").enumValueIndex = (int)slot.GetValueOrDefault();
        }

        private static T GetOrAdd<T>(GameObject player) where T : Component =>
            player.TryGetComponent(out T component) ? component : player.AddComponent<T>();

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"무기 에셋을 찾을 수 없습니다. {path}");
    }
}
