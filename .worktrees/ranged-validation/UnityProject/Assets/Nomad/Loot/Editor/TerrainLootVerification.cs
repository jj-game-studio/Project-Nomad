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
    public static class TerrainLootVerification
    {
        private const string SessionKey = "Nomad.Loot.TerrainVerify";
        private static double deadline;
        private static int frame;
        private static int waitUntil;
        private static int stage;
        private static int count;
        private static Keyboard keyboard;
        private static TerrainIntegrationControls controls;
        private static PlayerInventory inventory;
        private static LootChest chest;
        private static LootChest previous;
        private static string signature;

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
            deadline = EditorApplication.timeSinceStartup + 240;
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
                    count = Chests().Length;
                    Require(count > 0 && Chests().Select(c => c.InteractionId).Distinct().Count() == count, "unique generated chest IDs");
                    Require(Chests().All(c => c.GetComponent<BoxCollider>().bounds.Contains(c.InteractionPoint)), "points inside existing cube colliders");
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
                    Require(Scrap() == 96 && inventory.UsedSlots == 20, "F prepares real inventory");
                    Press(Key.E);
                    break;
                case 1:
                    Require(Scrap() == 99 && chest.GetStack(0).Count == 7, "generated cube E partial loot");
                    Press(Key.V);
                    break;
                case 2:
                    Require(Scrap() == 0, "V frees scrap without conflicting G gallery");
                    Press(Key.E);
                    break;
                case 3:
                    Require(Scrap() == 7 && chest.Status == LootStatus.Empty, "remaining seven collected");
                    Press(Key.E);
                    break;
                case 4:
                    Require(Scrap() == 7 && chest.GetComponent<BoxCollider>().enabled, "empty cube preserves collider and no duplicate");
                    Require(controls.GetComponent<TerrainLootIntegration>().Summary.Contains("0/10 Empty"), "saved scene HUD shows loot state");
                    TerrainIntegrationValidation.Capture(controls, 0, "terrain-loot-player");
                    previous = chest;
                    Press(Key.T);
                    break;
                case 5:
                    Require(previous == null || !previous.gameObject.activeInHierarchy, "regeneration retires previous chest");
                    Require(Chests().Length == count && Signature() == signature && Chests().All(c => c.GetStack(0).Count == 10), "same seed regenerates same placements with fresh contents");
                    Require(Scrap() == 7, "regeneration preserves player inventory");
                    MoveToChest();
                    Press(Key.E);
                    break;
                case 6:
                    Require(Scrap() == 17 && chest.Status == LootStatus.Empty, "regenerated cube E works");
                    Press(Key.P);
                    break;
                case 7:
                    Require(Chests().Length == 0 && Scrap() == 17, "P removes loot with props");
                    Press(Key.P);
                    break;
                default:
                    Require(Chests().Length == count && Scrap() == 17, "P restores same placements and keeps inventory");
                    TerrainIntegrationValidation.Capture(controls, 1, "terrain-loot-overview");
                    string report = $"TERRAIN_LOOT_PASS frame={frame} time={Time.time:F2} chests={count}: saved scene E/F/V/T/P, partial 3+7, empty, stable collider, HUD, generation cleanup and replay";
                    System.IO.File.WriteAllText("Artifacts/terrain-loot-results.txt", report);
                    Debug.Log(report);
                    Finish(0);
                    break;
            }
        }

        private static LootChest[] Chests() => Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None);
        private static string Signature() => string.Join(";", Chests().Select(c => c.transform.position.ToString("F3")).OrderBy(p => p));

        private static void MoveToChest()
        {
            var player = controls.Player;
            player.SetActive(false);
            foreach (LootChest target in Chests())
            foreach (Vector3 direction in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
            {
                Vector3 sample = target.transform.position + direction * 1.2f;
                if (!Physics.Raycast(sample + Vector3.up * 4, Vector3.down, out RaycastHit hit, 8) ||
                    hit.collider.GetComponentInParent<TerrainPiece>() == null) continue;
                Vector3 feet = hit.point + Vector3.up * 0.06f;
                if (Physics.CheckCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.45f, 0.35f) ||
                    Vector3.Distance(feet, target.InteractionPoint) >= 2) continue;
                chest = target;
                player.transform.position = feet;
                player.SetActive(true);
                Physics.SyncTransforms();
                controls.SetView(0);
                return;
            }
            throw new InvalidOperationException("No supported adjacent position for generated chest");
        }

        private static int Scrap()
        {
            int count = 0;
            for (int i = 0; i < inventory.UsedSlots; i++)
                if (inventory.GetStack(i).Item.ItemId == "scrap") count += inventory.GetStack(i).Count;
            return count;
        }

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
