using System;
using System.Linq;
using Nomad.Inventory;
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

            var camera = new GameObject("Overview").AddComponent<Camera>();
            camera.transform.position = new Vector3(22, 35, -25);
            camera.transform.LookAt(Vector3.zero);
            camera.farClipPlane = 500;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = Color.gray;
            Selection.activeGameObject = chest.gameObject;
            Debug.Log("B PREVIEW: unsaved review scene. Play > LootPreview context menu 1/2/3. E input is pending A implementation.");
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
                Debug.Log($"B_LOOT_PROPS_PLAYMODE_PASS: frame={Time.frameCount}, time={Time.time:F2}; partial/repeated/reentrant/exception/empty/unknown loot; 30 seeds x2 real terrain; cleanup");
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
                    Require(first.Pieces.Any(p => p.Kind != TerrainPieceKind.Stairs && p.WorldOccupancy.Contains(point)
                        && PropScatter.PreservesPassages(p, point)), "통로/계단/연결점 보호");
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


