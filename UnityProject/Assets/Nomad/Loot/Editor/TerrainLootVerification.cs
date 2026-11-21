using System;
using System.Linq;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Terrain;
using Nomad.Terrain.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.Loot.Editor
{
    // 저장된 통합 씬에서 상자 지점 배치(#20)와 실제 E·가방 입력 파밍을 검증합니다.
    public static class TerrainLootVerification
    {
        private const string SessionKey = "Nomad.Loot.TerrainVerify";
        private const int SweepSeeds = 20;
        private static readonly string[] Items = { "scrap", "field-knife", "field-vest" };
        private static double deadline;
        private static int frame;
        private static int waitUntil;
        private static int stage;
        private static Keyboard keyboard;
        private static TerrainIntegrationControls controls;
        private static PlayerInventory inventory;
        private static LootChest chest;
        private static LootChest previous;
        private static string signature;
        private static long chestItems;
        private static int chestScrap;

        public static void Run()
        {
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "timeout stage=" + stage);
                if (!Application.isPlaying || Time.frameCount <= frame || Time.frameCount < 4) return;
                frame = Time.frameCount;
                if (frame < waitUntil) return;
                if (keyboard == null)
                {
                    Application.runInBackground = true;
                    Time.captureFramerate = 60;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
                    inventory = controls.Player.GetComponent<PlayerInventory>();
                    Require(controls.Generator.LastResult.Success && controls.Player.activeSelf &&
                            controls.Player.GetComponent<PlayerInteraction>() != null, "saved scene input and inventory connected");
                    RequirePlacement("initial seed");

                    // 시작 seed에 상자 지점이 없으면 지점이 있는 seed를 찾아 입력 검증을 진행합니다.
                    for (int i = 0; i < 30 && Chests().Length == 0; i++)
                        controls.Regenerate(true);
                    Require(Chests().Length > 0, "a seed with chest points within 30 tries");
                    RequirePlacement("input seed");
                    Require(Chests().All(c => c.GetComponent<BoxCollider>().bounds.Contains(c.InteractionPoint)), "points inside chest colliders");
                    signature = Signature();
                    MoveToChest();
                    Press(Key.F);
                    return;
                }
                Advance();
            }
            catch (Exception exception)
            {
                Debug.LogError($"TERRAIN_LOOT_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static void Advance()
        {
            switch (stage++)
            {
                case 0:
                    Require(Count("scrap") == 96 && inventory.UsedSlots == 20, "F fills real inventory");
                    chestItems = ItemsIn(chest);
                    chestScrap = ChestCount(chest, "scrap");
                    Require(chestScrap > 0, "every chest rolls scrap");
                    Press(Key.E);
                    break;
                case 1:
                    Require(LootPanel() != null && ItemsIn(chest) == chestItems, "E opens chest without transferring");
                    TakeAll();
                    int gained = Count("scrap") - 96;
                    Require(gained == Mathf.Min(3, chestScrap) && ItemsIn(chest) == chestItems - gained,
                        "full inventory takes only the space left and keeps the rest in the chest");
                    TakeAll();
                    Require(ItemsIn(chest) == chestItems - gained && Count("scrap") == 96 + gained, "repeated Take all does not duplicate");
                    Press(Key.V);
                    break;
                case 2:
                    Require(inventory.UsedSlots == 0, "V empties the review bag");
                    long remaining = ItemsIn(chest);
                    TakeAll();
                    Require(chest.Status == LootStatus.Empty && Total() == remaining, "remaining items collected exactly once");
                    TakeAll();
                    Require(Total() == remaining, "empty chest does not duplicate");
                    Press(Key.E);
                    break;
                case 3:
                    Require(chest.Status == LootStatus.Empty && LootPanel() == null, "E closes the UI");
                    Require(chest.GetComponent<BoxCollider>().enabled, "empty chest preserves collider");
                    Require(controls.GetComponent<TerrainLootIntegration>().Summary.Contains("0 items  ·  Empty"), "saved scene HUD shows loot state");
                    TerrainIntegrationValidation.Capture(controls, 0, "terrain-loot-player");
                    previous = chest;
                    chestItems = Total();
                    Press(Key.T);
                    break;
                case 4:
                    Require(previous == null || !previous.gameObject.activeInHierarchy, "regeneration retires previous chest");
                    Require(Signature() == signature && Chests().All(c => c.Status == LootStatus.Closed),
                        "same seed replays same chests and contents");
                    Require(Total() == chestItems, "regeneration preserves player inventory");
                    MoveToChest();
                    Press(Key.E);
                    break;
                case 5:
                    Require(LootPanel() != null, "regenerated chest opens");
                    TakeAll();
                    Require(chest.Status == LootStatus.Empty, "regenerated chest loot works");
                    Press(Key.E);
                    break;
                default:
                    Require(LootPanel() == null, "panel closed before sweep");
                    string sweep = Sweep();
                    TerrainIntegrationValidation.Capture(controls, 1, "terrain-loot-overview");
                    string report = $"TERRAIN_LOOT_PASS frame={frame} time={Time.time:F2} {sweep}: chest points only, seed replay, "
                        + "E opens UI, full-bag partial loot, no duplication, empty chest, stable collider, HUD, regeneration cleanup, zero points/failure";
                    System.IO.File.WriteAllText("Artifacts/terrain-loot-results.txt", report);
                    Debug.Log(report);
                    Finish(0);
                    break;
            }
        }

        // 여러 seed에서 지점 배치·재현성·정리를 확인하고, 지점 0개·실패 결과를 검사합니다.
        private static string Sweep()
        {
            int total = 0;
            int points = 0;
            int empty = 0;
            for (int i = 0; i < SweepSeeds; i++)
            {
                controls.Regenerate(true);
                Debug.Log($"TERRAIN_LOOT_SEED {controls.Seed}: {controls.Navigation.State} points={controls.Navigation.LastResult.ChestPoints.Count} chests={Chests().Length}");
                if (!controls.Navigation.LastResult.Success) continue;
                RequirePlacement($"seed {controls.Seed}");
                string first = Signature();
                LootChest[] placed = Chests();
                controls.GetComponent<TerrainLootIntegration>().Refresh(controls.Navigation.LastResult);
                Require(placed.All(c => c != null && c.gameObject.activeInHierarchy) && Chests().Length == placed.Length,
                    $"seed {controls.Seed}: same generationId is not placed twice");
                total += placed.Length;
                if (placed.Length == 0) empty++;
                points += controls.Navigation.LastResult.ChestPoints.Count;

                controls.Regenerate(false);
                Require(Signature() == first, $"seed {controls.Seed} replays same chests");
            }

            ChestSpawner spawner = controls.GetComponent<TerrainLootIntegration>().Spawner;
            Require(spawner.Spawn("verify-empty", 1, Array.Empty<FieldSpawnPoint>()).Count == 0 && Chests().Length == 0,
                "zero chest points spawns nothing and clears previous chests");
            Require(spawner.Spawn(null).Count == 0 && Chests().Length == 0, "missing or failed result spawns nothing");
            controls.Regenerate(false);
            RequirePlacement("restored after zero points");
            return $"sweep seeds={SweepSeeds} chests={total}/{points} points, seeds without chest={empty}";
        }

        private static void RequirePlacement(string label)
        {
            FieldGenerationResult field = controls.Navigation.LastResult;
            LootChest[] chests = Chests();
            int points = field.ChestPoints.Count;
            Require(points == 0 || chests.Length >= Mathf.CeilToInt(points * 0.5f), $"{label}: at least half of {points} points used ({chests.Length})");
            Require(chests.Length <= points, $"{label}: no more chests than points");
            Require(chests.Select(c => c.InteractionId).Distinct().Count() == chests.Length
                    && chests.All(c => c.InteractionId.StartsWith(field.GenerationId + "/")), $"{label}: unique IDs of this generation");
            Require(chests.All(c => field.ChestPoints.Any(p => Vector3.Distance(p.Position, c.transform.position) < 0.01f)),
                $"{label}: chests only on provided chest points");
            Require(chests.All(c => ItemsIn(c) > 0 && c.Status == LootStatus.Closed), $"{label}: new chests hold loot");
        }

        private static Transform LootPanel() => GameObject.Find("ChestLootPanel")?.transform;

        private static void TakeAll()
        {
            Transform panel = LootPanel();
            Require(panel != null, "loot panel opens for the generated chest");
            panel.Find("Take All").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        private static LootChest[] Chests() => Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None);

        // 위치와 내용물을 함께 비교해 같은 seed의 재현성을 확인합니다.
        private static string Signature() => string.Join(";", Chests()
            .Select(c => c.transform.position.ToString("F3") + c.transform.eulerAngles.y.ToString("F0") + ":"
                + string.Join(",", Enumerable.Range(0, c.StackCount).Select(i => c.GetStack(i).ItemId + c.GetStack(i).Count)))
            .OrderBy(s => s, StringComparer.Ordinal));

        private static void MoveToChest()
        {
            var player = controls.Player;
            player.SetActive(false);
            string reasons = string.Empty;
            foreach (LootChest target in Chests())
            {
                // 생성 결과가 검증한 접근점을 먼저 쓰고, 없으면 네 방향 1.2m 지점을 찾습니다.
                FieldSpawnPoint point = controls.Navigation.LastResult.ChestPoints
                    .FirstOrDefault(p => Vector3.Distance(p.Position, target.transform.position) < 0.01f);
                var samples = new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back }
                    .Select(direction => target.transform.position + direction * 1.2f).ToList();
                if (point != null) samples.Insert(0, point.ApproachPosition);

                foreach (Vector3 sample in samples)
                {
                    if (!Physics.Raycast(sample + Vector3.up * 4, Vector3.down, out RaycastHit hit, 8, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        reasons += $" {target.name}:noFloor";
                        continue;
                    }
                    Vector3 feet = hit.point + Vector3.up * 0.06f;
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.45f, 0.33f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        reasons += $" {target.name}:blocked({hit.collider.name})";
                        continue;
                    }
                    if (Vector3.Distance(feet, target.InteractionPoint) >= 2)
                    {
                        reasons += $" {target.name}:far";
                        continue;
                    }

                    chest = target;
                    player.transform.position = feet;
                    player.SetActive(true);
                    Physics.SyncTransforms();
                    controls.SetView(0);
                    return;
                }
            }
            throw new InvalidOperationException($"No supported adjacent position for generated chest ({Chests().Length} chests):{reasons}");
        }

        private static long ItemsIn(LootChest target) =>
            Enumerable.Range(0, target.StackCount).Sum(i => (long)target.GetStack(i).Count);

        private static int ChestCount(LootChest target, string id) =>
            Enumerable.Range(0, target.StackCount).Where(i => target.GetStack(i).ItemId == id).Sum(i => target.GetStack(i).Count);

        private static int Count(string id)
        {
            int count = 0;
            for (int i = 0; i < inventory.UsedSlots; i++)
                if (inventory.GetStack(i).Item.ItemId == id) count += inventory.GetStack(i).Count;
            return count;
        }

        private static long Total() => Items.Sum(Count);

        private static void Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            waitUntil = frame + 3;
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
