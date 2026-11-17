using System;
using System.Linq;
using Nomad.Inventory;
using Nomad.Player;
using Nomad.Props;
using Nomad.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Loot.Editor
{
    public static class LootVerification
    {
        private const string SessionKey = "Nomad.Loot.Verify";
        private static double deadline;
        private static int frame;

        [MenuItem("Nomad/Loot/Open B Props Loot Preview")]
        public static void OpenPreview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var generator = new GameObject("B Terrain").AddComponent<TerrainGenerator>();
            generator.transform.position = new Vector3(60, 0, 60);
            generator.Configure(LoadPieces(), 12, 16, new Bounds(Vector3.zero, new Vector3(400, 100, 400)));
            var scatter = new GameObject("B Props").AddComponent<PropScatter>();
            GameObject template = CreateProp();
            template.SetActive(false);
            scatter.Configure(generator, template);
            generator.Generate();

            var pad = new GameObject("B Fixed Chest Review Pad");
            Cube("Floor", pad.transform, new Vector3(0, -0.25f, 0), new Vector3(10, 0.5f, 10));
            var inventory = CreateInventory();
            var chest = new GameObject("B Chest - inspect LootPreview during Play").AddComponent<LootChest>();
            chest.transform.position = new Vector3(2.5f, 0, 2.5f);
            chest.Initialize("b-review-chest", new[] { new LootStack("scrap", 10) });
            GameObject closed = Cube("Closed", chest.transform, new Vector3(0, 0.4f, 0), new Vector3(1, 0.8f, 1));
            GameObject opened = Cube("Open - items remain", chest.transform, new Vector3(0, 0.2f, 0), new Vector3(1, 0.4f, 1));
            GameObject empty = Cube("Empty", chest.transform, new Vector3(0, 0.1f, 0), new Vector3(1, 0.2f, 1));
            Set(chest, "closedVisual", closed);
            Set(chest, "openVisual", opened);
            Set(chest, "emptyVisual", empty);
            opened.SetActive(false);
            empty.SetActive(false);
            var preview = chest.gameObject.AddComponent<LootPreview>();
            Set(preview, "inventory", inventory);
            Set(preview, "chest", chest);

            var player = new GameObject("B Review Player");
            player.SetActive(false);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.05f;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Player Visual";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = Vector3.up * 0.9f;
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var cameraRoot = new GameObject("Player Camera");
            cameraRoot.SetActive(false);
            var camera = cameraRoot.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 500;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.16f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            Set(player.AddComponent<PlayerMovement>(), "viewCamera", camera);
            Set(cameraRoot.AddComponent<TopDownCamera>(), "target", player.transform);
            cameraRoot.SetActive(true);

            var field = new GameObject("B Field Controls").AddComponent<FieldPreview>();
            Set(field, "generator", generator);
            Set(field, "scatter", scatter);
            Set(field, "player", player);
            Set(field, "status", CreateStatus());
            var settings = new SerializedObject(field);
            SerializedProperty pieces = settings.FindProperty("pieces");
            TerrainPiece[] sources = LoadPieces();
            pieces.arraySize = sources.Length;
            for (int i = 0; i < sources.Length; i++)
                pieces.GetArrayElementAtIndex(i).objectReferenceValue = sources[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            field.Regenerate(false);
            camera.transform.position = player.transform.position + new Vector3(0, 12, -8);
            camera.transform.LookAt(player.transform.position);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = Color.gray;
            Selection.activeGameObject = player;
            Debug.Log("B PREVIEW: Play + Game view. R new seed, T replay, WASD move. Player camera enabled. For E input use Nomad > Loot > Open B Chest E Key Preview.");
        }

        private static UnityEngine.UI.Text CreateStatus()
        {
            var root = new GameObject("B Preview HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Status Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(16, -16);
            rect.sizeDelta = new Vector2(670, 155);
            var background = panel.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.025f, 0.04f, 0.07f, 0.92f);
            background.raycastTarget = false;

            var label = new GameObject("Status", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            label.transform.SetParent(panel.transform, false);
            var textRect = (RectTransform)label.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 10);
            textRect.offsetMax = new Vector2(-16, -10);
            var text = label.GetComponent<UnityEngine.UI.Text>();
            // 기존 인벤토리와 동일한 uGUI Text/내장 폰트를 사용합니다.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 21;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "검증 시간 초과");
                if (!Application.isPlaying || Time.frameCount < 3) return;
                Application.runInBackground = true;

                if (frame == 0)
                {
                    VerifyLoot();
                    VerifyScatter();
                    frame = Time.frameCount;
                    return;
                }
                if (Time.frameCount <= frame + 2) return;
                Require(!Object.FindObjectsByType<PropScatter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(), "소품 지연 삭제");
                Require(!Object.FindObjectsByType<TerrainPiece>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(), "지형 지연 삭제");
                Debug.Log($"B_LOOT_PROPS_PLAYMODE_PASS: frame={Time.frameCount}, time={Time.time:F2}; partial/repeated/empty/unknown loot, storage, reentry, subscriber exceptions, 30 seeds x2 real terrain, cleanup");
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void VerifyLoot()
        {
            PlayerInventory inventory = CreateInventory();

            var chest = new GameObject("Loot test").AddComponent<LootChest>();
            chest.Initialize("test", new[] { new LootStack("scrap", 10) });
            inventory.Add("scrap", 96);
            inventory.Add("field-knife", 19);
            int recursive = -1;
            Action reenter = () => recursive = chest.TryLoot(inventory);
            inventory.Changed += reenter;
            Require(chest.TryLoot(inventory) == 3 && recursive == 0, "부분 추가 및 재진입 차단");
            inventory.Changed -= reenter;
            Require(chest.GetStack(0).Count == 7 && chest.Status == LootStatus.Remaining, "잔여 7개");
            for (int i = 0; i < 10; i++) Require(chest.TryLoot(inventory) == 0 && chest.GetStack(0).Count == 7, "반복 입력 수량 보존");
            inventory.Remove("scrap", int.MaxValue);
            Require(chest.TryLoot(inventory) == 7 && chest.Status == LootStatus.Empty, "남은 수량 회수");
            Require(chest.TryLoot(inventory) == 0 && inventory.GetStack(19).Count == 7, "빈 상자 중복 획득 방지");
            Object.Destroy(chest.gameObject);

            chest = new GameObject("Unknown and ordered loot").AddComponent<LootChest>();
            chest.Initialize("ordered", new[] { new LootStack("unknown", 3), new LootStack("scrap", 100), new LootStack("field-knife", 2) });
            Require(chest.TryLoot(inventory) == 92, "목록 순서 및 기존 스택 우선");
            Require(chest.GetStack(0).Count == 3 && chest.GetStack(1).Count == 8 && chest.GetStack(2).Count == 2, "미등록 ID/공간 부족 보존");
            Object.Destroy(chest.gameObject);

            inventory.Remove("scrap", int.MaxValue);
            chest = new GameObject("Callback exception").AddComponent<LootChest>();
            chest.Initialize("exception", new[] { new LootStack("scrap", 10) });
            Action fail = () => throw new InvalidOperationException("Expected subscriber failure");
            inventory.Changed += fail;
            try { chest.TryLoot(inventory); throw new Exception("예외가 필요합니다."); }
            catch (InvalidOperationException) { }
            inventory.Changed -= fail;
            Require(chest.GetStack(0).Count == 0 && chest.TryLoot(inventory) == 0, "외부 알림 예외 후 복제 방지 및 잠금 해제");
            Object.Destroy(chest.gameObject);

            inventory.Add("scrap", 89);
            chest = new GameObject("Storage test").AddComponent<LootChest>();
            chest.Initialize("store", new[] { new LootStack("scrap", 96), new LootStack("field-knife", 1) });
            bool observedComplete = false;
            Action observe = () =>
            {
                observedComplete = inventory.GetSlot(0).Item == null && chest.GetStack(0).Count == 99 &&
                    chest.GetStack(2).Count == 96 && chest.TryLoot(inventory) == 0;
            };
            inventory.Changed += observe;
            Require(chest.TryStoreStack(inventory, 0) == 99 && observedComplete && inventory.UsedSlots == 19,
                "상자 중첩 한도·남은 새 스택·전송 완료 상태·재진입 차단");
            inventory.Changed -= observe;
            Require(chest.TryStoreStack(inventory, 0) == 0 && chest.TryStoreStack(inventory, -1) == 0,
                "빈 슬롯과 잘못된 슬롯 입력 차단");

            inventory.Changed += fail;
            bool threw = false;
            try { chest.TryStoreStack(inventory, 1); }
            catch (InvalidOperationException exception) when (exception.Message == "Expected subscriber failure") { threw = true; }
            inventory.Changed -= fail;
            Require(threw && inventory.GetSlot(1).Item == null && chest.GetStack(3).Count == 1 &&
                chest.TryStoreStack(inventory, 2) == 1, "보관 알림 예외 뒤에도 중복 방지와 잠금 해제");

            inventory.Add("scrap", 96);
            inventory.Changed += fail;
            threw = false;
            try { chest.TryLootStackToSlot(inventory, 2, 0); }
            catch (InvalidOperationException exception) when (exception.Message == "Expected subscriber failure") { threw = true; }
            inventory.Changed -= fail;
            Require(threw && inventory.GetSlot(0).Count == 99 && chest.GetStack(2).Count == 93,
                "지정 슬롯 부분 획득과 알림 예외의 수량 보존");
            Require(chest.TryLootStackToSlot(inventory, 2, 3) == 0 && chest.TryLootStackToSlot(inventory, 2, -1) == 0 &&
                chest.GetStack(2).Count == 93, "다른 아이템과 잘못된 대상 슬롯 차단");
            Require(chest.TryLootStackToSlot(inventory, 2, 2) == 93 && inventory.GetSlot(2).Count == 93 &&
                inventory.GetSlot(1).Item == null && chest.GetStack(2).Count == 0, "드롭한 빈 슬롯으로 스택 이전");
            Object.Destroy(chest.gameObject);
            Object.Destroy(inventory.gameObject);
        }

        private static void VerifyScatter()
        {
            var host = new GameObject("Scatter verification");
            var generator = host.AddComponent<TerrainGenerator>();
            var scatter = host.AddComponent<PropScatter>();
            GameObject template = CreateProp();
            template.SetActive(false);
            scatter.Configure(generator, template);
            int count = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                generator.Configure(LoadPieces(), 12, seed, new Bounds(Vector3.zero, new Vector3(400, 100, 400)));
                TerrainGenerationResult first = generator.Generate();
                Require(first.Success, "지형 생성");
                Vector3[] signature = scatter.Positions.ToArray();
                count += signature.Length;
                Require(scatter.CandidateChecks <= 12 * PropScatter.CandidatesPerPiece, "유한 후보");
                foreach (Vector3 point in signature)
                {
                    Require(first.Pieces.Any(p => p.Kind != TerrainPieceKind.Stairs && new Bounds(p.WorldOccupancy.center, p.WorldOccupancy.size + Vector3.one * 0.02f).Contains(point)
                        && PropScatter.PreservesPassages(p, point)), "통로/계단/연결점 보호 seed=" + seed + " point=" + point);
                }
                for (int i = 0; i < signature.Length; i++)
                for (int j = i + 1; j < signature.Length; j++)
                    Require(Vector3.Distance(signature[i], signature[j]) >= PropScatter.PropRadius * 2, "소품 간 중첩 방지");

                scatter.Scatter(first);
                Require(signature.SequenceEqual(scatter.Positions), "같은 generationId 중복 배치 방지");
                generator.Generate();
                Require(signature.SequenceEqual(scatter.Positions), "동일 seed 재현");
            }
            Require(count > 100, "실제 소품 배치 결과");
            GameObject obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstruction.transform.localScale = new Vector3(400, 40, 400);
            Physics.SyncTransforms();
            generator.Generate();
            Require(scatter.Positions.Count == 0 && scatter.CandidateChecks <= 600, "공간 부족 시 유한 종료");
            obstruction.SetActive(false);
            Object.Destroy(obstruction);

            template.transform.localScale = Vector3.one * 2;
            bool rejected = false;
            try { generator.Generate(); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && scatter.Positions.Count == 0, "예약 크기를 벗어난 프리팹 거부");
            template.transform.localScale = Vector3.one;

            generator.Configure(LoadPieces(), 12, 1, new Bounds(Vector3.zero, Vector3.one));
            Require(!generator.Generate().Success && scatter.Positions.Count == 0 && scatter.GenerationId == null, "실패 결과 소품 정리");
            Object.Destroy(host);
            Object.Destroy(template);
        }

        private static TerrainPiece[] LoadPieces()
        {
            return new[] { "Flat", "Stairs", "Raised", "Corner", "Tee", "Cross", "StairsCorner", "StairsTee", "StairsCross" }
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ThirdParty/Nomad/Terrain/{name}.prefab").GetComponent<TerrainPiece>()).ToArray();
        }

        private static PlayerInventory CreateInventory()
        {
            var root = new GameObject("B Review Inventory");
            root.SetActive(false);
            var inventory = root.AddComponent<PlayerInventory>();
            var data = new SerializedObject(inventory);
            SerializedProperty items = data.FindProperty("items");
            items.arraySize = 2;
            items.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Nomad/Inventory/Demo/scrap.asset");
            items.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Nomad/Inventory/Demo/field-knife.asset");
            data.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            return inventory;
        }

        private static GameObject CreateProp()
        {
            var root = new GameObject("B Prop Template (1m cube)");
            Cube("Crate", root.transform, Vector3.up * 0.5f, Vector3.one);
            return root;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            return cube;
        }

        private static void Set(Object target, string name, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(name).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("B_LOOT_PROPS_FAIL: " + message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            EditorApplication.Exit(code);
        }
    }
}
