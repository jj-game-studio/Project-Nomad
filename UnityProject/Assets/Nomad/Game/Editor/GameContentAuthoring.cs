using System.IO;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Game.Modules;
using Nomad.Hud;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Loot;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Player;
using Nomad.Terrain;
using Nomad.Village.Editor;
using Nomad.WorldHud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Nomad.Game.Editor
{
    // 팀원 기능을 메인 게임 모듈로 묶습니다: Player 프리팹, Field 지역 씬, 마을 입출구, GameConfig 등록.
    // 각 기능의 원본 테스트 씬은 수정하지 않고, 다시 실행하면 최신 main 기준으로 같은 결과를 다시 만듭니다.
    internal static class GameContentAuthoring
    {
        internal const string PlayerPrefabPath = "Assets/Nomad/Game/Prefabs/PlayerRig.prefab";
        internal const string MonsterModulePath = "Assets/Nomad/Game/Prefabs/FieldMonsters.prefab";
        internal const string DeathRestartPath = "Assets/Nomad/Game/Prefabs/DeathRestart.prefab";
        internal const string FieldPath = GameSceneAuthoring.FieldPath;
        // 몬스터 외형·재질은 #59 통합 씬(GameplayIntegrationSetup)과 같은 에셋을 씁니다.
        private const string MonsterMaterialFolder = "Assets/Nomad/Monsters/Demo/CombatMaterials";
        private const string MonsterAppearancePath = "Assets/ThirdParty/Nomad/Monsters/Skeletons/SkeletonAppearances.asset";
        // 필드 원본: #73 TerrainTest(Terrain 지면·꾸밈·조명·Volume). 원본 씬은 수정하지 않고 복사합니다(#94).
        private const string FieldSourcePath = Nomad.Terrain.Editor.FieldDressingAuthoring.ScenePath;
        private const string ProjectilePath = "Assets/Nomad/Combat/PlayerProjectile.prefab";
        private static readonly string[] ItemFolders = { "Assets/Nomad/Inventory/Demo", "Assets/Nomad/Equipment/Items" };

        [MenuItem("Nomad/Game/Build Game Content")]
        public static void BuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            GameObject player = BuildPlayerPrefab();
            BuildFieldScene();
            ConnectVillage();
            ConfigureGame(player);
            BuildFieldMonsters();
            BuildDeathRestart();

            EditorSceneManager.OpenScene(GameSceneAuthoring.ScenePath);
            Debug.Log("GAME_CONTENT_BUILT");
        }

        // 필드 몬스터 지역 모듈만 다시 만들고 GameConfig의 Field 지역 모듈로 등록합니다(#89 5단계).
        [MenuItem("Nomad/Game/Build Field Monsters")]
        public static void BuildFieldMonsters()
        {
            var module = new GameObject("FieldMonsters");
            var factory = module.AddComponent<MonsterActorFactory>();
            factory.ConfigureMaterials(MonsterMaterial("Monster"), MonsterMaterial("Elite"),
                MonsterMaterial("HeavyWarning"), MonsterMaterial("WarningFill"));
            factory.ConfigureAppearances(AssetDatabase.LoadAssetAtPath<MonsterAppearanceSet>(MonsterAppearancePath));
            module.AddComponent<FieldMonsters>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(module, MonsterModulePath);
            Object.DestroyImmediate(module);

            RegisterRegionModule("Field", prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("FIELD_MONSTERS_BUILT");
        }

        // 사망 안내·R 키 재시작 모듈을 만들고 GameConfig의 영속 모듈로 등록합니다.
        [MenuItem("Nomad/Game/Build Death Restart")]
        public static void BuildDeathRestart()
        {
            var module = new GameObject("DeathRestart");
            module.AddComponent<DeathRestart>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(module, DeathRestartPath);
            Object.DestroyImmediate(module);

            var data = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameConfig>(GameSceneAuthoring.ConfigPath));
            AddModule(data.FindProperty("modules"), prefab);
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("DEATH_RESTART_BUILT");
        }

        // 이동·전투·체력·가방·장비·HUD·상호작용을 한 Player에 모으고, 카메라·EventSystem을 같은 프리팹에 둡니다.
        internal static GameObject BuildPlayerPrefab()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rig = new GameObject("PlayerRig");

            var player = new GameObject("Player") { tag = GameManager.PlayerTag };
            player.transform.SetParent(rig.transform, false);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 50f; // 마을 계단 경사로를 오를 수 있게 기본 45도보다 넉넉히
            controller.minMoveDistance = 0f;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Player Body";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = controller.center;
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player.transform, false);
            muzzle.localPosition = new Vector3(0f, 1.2f, 0.45f);

            var cameraObject = new GameObject("Top Down Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(rig.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 600f; // 필드 전경 보기에서도 잘리지 않게
            cameraObject.AddComponent<AudioListener>();
            Set(cameraObject.AddComponent<TopDownCamera>(), "target", player.transform);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(rig.transform, false);

            AddPlayerComponents(player, camera, muzzle);

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rig, PlayerPrefabPath);
            Object.DestroyImmediate(rig);
            return prefab;
        }

        private static void AddPlayerComponents(GameObject player, Camera camera, Transform muzzle)
        {
            Projectile projectile = AssetDatabase.LoadAssetAtPath<Projectile>(ProjectilePath);
            ItemDefinition[] items = AssetDatabase.FindAssets("t:ItemDefinition", ItemFolders)
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(item => item.ItemId).ToArray();

            Set(player.AddComponent<PlayerMovement>(), "viewCamera", camera);
            player.AddComponent<Health>();
            SetArray(player.AddComponent<PlayerInventory>(), "items", items);

            // 기존 사격 컴포넌트는 장비 전투가 켜지면 스스로 꺼지며, 기존 참조 호환을 위해 둡니다.
            PlayerCombat legacy = player.AddComponent<PlayerCombat>();
            Set(legacy, "viewCamera", camera);
            Set(legacy, "muzzle", muzzle);
            Set(legacy, "projectile", projectile);

            player.AddComponent<PlayerEquipment>();
            PlayerEquipmentCombat combat = player.AddComponent<PlayerEquipmentCombat>();
            Set(combat, "viewCamera", camera);
            Set(combat, "muzzle", muzzle);
            Set(combat, "projectile", projectile);
            Set(combat, "unarmedDefinition", items.Single(item => item.ItemId == "demo-unarmed"));
            player.AddComponent<EquipmentCombatVisuals>();
            player.AddComponent<EquipmentPanel>();

            player.AddComponent<PlayerQuickSlots>();
            player.AddComponent<PlayerHud>();
            player.AddComponent<PlayerInteraction>();
            Set(player.AddComponent<CharacterStaminaHud>(), "viewCamera", camera);
            Set(player.AddComponent<InteractionHints>(), "viewCamera", camera);

            // 임시 시작 장비: 검·활을 지급해 무기 1·2칸에 장착합니다(#89 판단 필요).
            var kit = new SerializedObject(player.AddComponent<StarterKit>());
            SerializedProperty entries = kit.FindProperty("items");
            entries.arraySize = 2;
            SetEntry(entries.GetArrayElementAtIndex(0), "demo-sword", EquipmentSlot.Weapon1);
            SetEntry(entries.GetArrayElementAtIndex(1), "demo-bow", EquipmentSlot.Weapon2);
            kit.ApplyModifiedPropertiesWithoutUndo();
        }

        // TerrainTest 씬을 복사해 검토용 Player·카메라·상태 UI·디버그 키만 빼고 메인 게임 Player에 연결합니다.
        internal static void BuildFieldScene()
        {
            Scene scene = EditorSceneManager.OpenScene(FieldSourcePath, OpenSceneMode.Single);
            var controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
            var data = new SerializedObject(controls);
            var testPlayer = (GameObject)data.FindProperty("player").objectReferenceValue;
            var testCamera = (Camera)data.FindProperty("viewCamera").objectReferenceValue;
            var status = (Component)data.FindProperty("status").objectReferenceValue;

            data.FindProperty("player").objectReferenceValue = null;
            data.FindProperty("viewCamera").objectReferenceValue = null;
            data.FindProperty("status").objectReferenceValue = null;
            data.FindProperty("testControls").boolValue = false;
            data.FindProperty("newSeedOnStart").boolValue = true; // 진입마다 새 지형(#94 판단 5)
            data.ApplyModifiedPropertiesWithoutUndo();

            // 원본 카메라의 후처리·배경은 필드에 있는 동안 영속 카메라에 적용합니다.
            var region = new SerializedObject(controls.gameObject.AddComponent<FieldRegion>());
            if (testCamera != null)
            {
                UniversalAdditionalCameraData cameraData = testCamera.GetUniversalAdditionalCameraData();
                region.FindProperty("postProcessing").boolValue = cameraData.renderPostProcessing;
                region.FindProperty("antialiasing").enumValueIndex = (int)cameraData.antialiasing;
                region.FindProperty("clearFlags").enumValueFlag = (int)testCamera.clearFlags;
                region.FindProperty("background").colorValue = testCamera.backgroundColor;
                region.ApplyModifiedPropertiesWithoutUndo();
            }

            DestroyRoot(testPlayer);
            DestroyRoot(testCamera);
            DestroyRoot(status);

            var loot = Object.FindAnyObjectByType<TerrainLootIntegration>();
            if (loot != null)
            {
                var lootData = new SerializedObject(loot);
                DestroyRoot((Component)lootData.FindProperty("status").objectReferenceValue);
                lootData.FindProperty("inventory").objectReferenceValue = null;
                lootData.FindProperty("status").objectReferenceValue = null;
                lootData.ApplyModifiedPropertiesWithoutUndo();
            }

            // 카메라·입력은 Player 모듈이 가져오므로 지역 씬에는 남기지 않습니다.
            foreach (EventSystem system in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
                DestroyRoot(system);
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
                Object.DestroyImmediate(listener);

            EditorSceneManager.SaveScene(scene, FieldPath, true);
        }

        // 마을 씬의 자체 Player·카메라를 기본 입구로 바꾸고, 필드 출구와 돌아올 입구를 연결합니다.
        internal static void ConnectVillage()
        {
            Scene scene = EditorSceneManager.OpenScene(VillageAuthoring.ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();

            GameObject oldPlayer = roots.FirstOrDefault(root => root.name == "Player");
            Vector3 start = oldPlayer != null ? oldPlayer.transform.position : VillageAuthoring.StartPosition;
            foreach (GameObject root in roots.Where(root => root.name == "Player" || root.name == "Top Down Camera"))
                Object.DestroyImmediate(root);

            if (!roots.Any(root => root != null && root.GetComponentInChildren<RegionEntrance>() != null))
                VillageAuthoring.CreateEntrance("Player Entrance", null, start, Quaternion.identity);

            Transform trigger = roots.Where(root => root != null)
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(child => child.name == "Field Exit Trigger");
            VillageAuthoring.LinkFieldExit(trigger);

            EditorSceneManager.SaveScene(scene);
        }

        // Player 모듈과 지역 목록을 등록하고 Build Settings에 Field를 추가합니다. 다른 작업이 등록한 모듈은 유지합니다.
        internal static void ConfigureGame(GameObject player)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameSceneAuthoring.ConfigPath);
            var data = new SerializedObject(config);

            AddModule(data.FindProperty("modules"), player);

            SerializedProperty regions = data.FindProperty("regionScenes");
            foreach (string region in GameSceneAuthoring.Regions.Select(path => Path.GetFileNameWithoutExtension(path)))
            {
                if (Enumerable.Range(0, regions.arraySize).Any(i => regions.GetArrayElementAtIndex(i).stringValue == region))
                    continue;

                regions.arraySize++;
                regions.GetArrayElementAtIndex(regions.arraySize - 1).stringValue = region;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            GameSceneAuthoring.RegisterBuildScenes();
            AssetDatabase.SaveAssets();
        }

        // 지역 모듈 목록에 프리팹을 한 번만 추가합니다. 다른 작업이 등록한 항목은 유지합니다.
        // 이미 등록된 모듈은 다시 넣지 않습니다.
        private static void AddModule(SerializedProperty modules, GameObject prefab)
        {
            if (Enumerable.Range(0, modules.arraySize).Any(i => modules.GetArrayElementAtIndex(i).objectReferenceValue == prefab))
                return;

            modules.arraySize++;
            modules.GetArrayElementAtIndex(modules.arraySize - 1).objectReferenceValue = prefab;
        }

        private static void RegisterRegionModule(string region, GameObject prefab)
        {
            var data = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameConfig>(GameSceneAuthoring.ConfigPath));
            SerializedProperty entries = data.FindProperty("regionModules");

            SerializedProperty entry = null;
            for (int i = 0; i < entries.arraySize && entry == null; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("region").stringValue == region)
                    entry = entries.GetArrayElementAtIndex(i);
            }

            if (entry == null)
            {
                entries.arraySize++;
                entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("region").stringValue = region;
                entry.FindPropertyRelative("prefabs").arraySize = 0;
            }

            SerializedProperty prefabs = entry.FindPropertyRelative("prefabs");
            if (!Enumerable.Range(0, prefabs.arraySize).Any(i => prefabs.GetArrayElementAtIndex(i).objectReferenceValue == prefab))
            {
                prefabs.arraySize++;
                prefabs.GetArrayElementAtIndex(prefabs.arraySize - 1).objectReferenceValue = prefab;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material MonsterMaterial(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MonsterMaterialFolder}/{name}.mat");

        private static void DestroyRoot(Component component)
        {
            if (component != null)
                DestroyRoot(component.gameObject);
        }

        private static void DestroyRoot(GameObject target)
        {
            if (target != null)
                Object.DestroyImmediate(target.transform.root.gameObject);
        }

        private static void SetEntry(SerializedProperty entry, string itemId, EquipmentSlot slot)
        {
            entry.FindPropertyRelative("itemId").stringValue = itemId;
            entry.FindPropertyRelative("count").intValue = 1;
            entry.FindPropertyRelative("equip").boolValue = true;
            entry.FindPropertyRelative("slot").enumValueIndex = (int)slot;
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
