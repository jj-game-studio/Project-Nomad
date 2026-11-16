using System;
using System.IO;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.Loot.Editor
{
    public static class ChestInteractionVerification
    {
        private const string SessionKey = "Nomad.Loot.ChestInteractionVerify";
        private static double deadline;
        private static int frame;
        private static int stage;
        private static int calls;
        private static float rollStart;
        private static Keyboard keyboard;
        private static PlayerMovement player;
        private static PlayerInventory inventory;
        private static LootChest chest;
        private static GameObject wall;

        [MenuItem("Nomad/Loot/Open B Chest E Key Preview")]
        public static void OpenPreview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene("Assets/Nomad/Inventory/A_PlayerInventory.unity");
            // 장면 간 참조는 Play 재로드 시 유실되므로 현재 장면에 구성하고 원본은 저장하지 않습니다.
            player = Object.FindAnyObjectByType<PlayerMovement>();
            inventory = player.GetComponent<PlayerInventory>();
            var demo = Object.FindAnyObjectByType<InventoryDemo>();
            var status = new SerializedObject(demo).FindProperty("status").objectReferenceValue;
            demo.enabled = false;

            chest = CreateChest("b-e-key-chest", player.transform.position + new Vector3(-0.9f, 0, 1.1f));
            var controls = chest.gameObject.AddComponent<LootPreview>();
            var data = new SerializedObject(controls);
            data.FindProperty("inventory").objectReferenceValue = inventory;
            data.FindProperty("chest").objectReferenceValue = chest;
            data.FindProperty("status").objectReferenceValue = status;
            data.FindProperty("keyboardControls").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = chest.gameObject;
            Debug.Log("B CHEST PREVIEW: Play + Game. E loot 3/10, G free scrap, E loot remaining 7. F fill inventory. Tab inventory.");
        }

        private static LootChest CreateChest(string id, Vector3 position)
        {
            var root = new GameObject(id);
            root.SetActive(false);
            root.transform.position = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = Vector3.up * 0.4f;
            collider.size = new Vector3(1, 0.8f, 1);
            var target = root.AddComponent<LootChest>();
            target.Initialize(id, new[] { new LootStack("scrap", 10) });
            var point = new GameObject("Interaction Point").transform;
            point.SetParent(root.transform, false);
            point.localPosition = collider.center;
            var settings = new SerializedObject(target);
            settings.FindProperty("interactionPoint").objectReferenceValue = point;

            string[] fields = { "closedVisual", "openVisual", "emptyVisual" };
            for (int i = 0; i < fields.Length; i++)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = fields[i];
                visual.transform.SetParent(root.transform, false);
                float height = i == 0 ? 0.8f : i == 1 ? 0.4f : 0.2f;
                visual.transform.localScale = new Vector3(1, height, 1);
                visual.transform.localPosition = Vector3.up * (height * 0.5f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                settings.FindProperty(fields[i]).objectReferenceValue = visual;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            return target;
        }

        public static void Run()
        {
            OpenPreview();
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "timeout");
                if (!Application.isPlaying || Time.frameCount <= frame) return;
                frame = Time.frameCount;

                if (keyboard == null)
                {
                    player = Object.FindAnyObjectByType<PlayerMovement>();
                    inventory = player.GetComponent<PlayerInventory>();
                    chest = Object.FindAnyObjectByType<LootChest>();
                    if (!player.GetComponent<CharacterController>().isGrounded || inventory.UsedSlots != 20) return;
                    Require(player.GetComponent<PlayerInteraction>() != null, "A E input attached");
                    Require(!chest.CanInteract(default), "invalid context rejected");
                    chest.Interact(default);
                    Require(chest.GetStack(0).Count == 10, "invalid context preserves contents");
                    chest.Changed += () => calls++;
                    Time.captureFramerate = 60;
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    Physics.SyncTransforms();
                    Queue(Key.E);
                    return;
                }
                Advance();
            }
            catch (Exception exception)
            {
                Debug.LogError($"B_CHEST_INTERACTION_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static void Advance()
        {
            switch (++stage)
            {
                case 1:
                    Require(Scrap() == 99 && chest.GetStack(0).Count == 7 && calls == 1, "E partial 3/10");
                    CheckCollider();
                    Capture();
                    break;
                case 2:
                    Require(calls == 1 && Scrap() == 99, "hold does not repeat");
                    Queue();
                    break;
                case 3: Queue(Key.E); break;
                case 4:
                    Require(calls == 2 && chest.GetStack(0).Count == 7 && Scrap() == 99, "full inventory retry preserves 7");
                    Queue(Key.G);
                    break;
                case 5:
                    Require(Scrap() == 0, "G frees real player inventory");
                    Queue(Key.E);
                    break;
                case 6:
                    Require(Scrap() == 7 && chest.Status == LootStatus.Empty && calls == 3, "E receives remaining 7");
                    CheckCollider();
                    Queue();
                    break;
                case 7: Queue(Key.E); break;
                case 8:
                    Require(Scrap() == 7 && calls == 3, "empty chest excluded, no duplicate");
                    chest.gameObject.SetActive(false);
                    chest = CreateChest("b-gate-check", player.transform.position + Vector3.forward * 3);
                    Queue();
                    break;
                case 9: Queue(Key.E); break;
                case 10:
                    Require(chest.Status == LootStatus.Closed && Scrap() == 7, "outside 2m excluded");
                    chest.transform.position = player.transform.position + new Vector3(-0.9f, 0, 0);
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = player.transform.position + new Vector3(-0.45f, 0.6f, 0);
                    wall.transform.localScale = new Vector3(0.1f, 1.2f, 0.8f);
                    Physics.SyncTransforms();
                    Queue();
                    break;
                case 11: Queue(Key.E); break;
                case 12:
                    Require(chest.Status == LootStatus.Closed && Scrap() == 7, "wall blocks real chest");
                    wall.SetActive(false);
                    Physics.SyncTransforms();
                    Queue();
                    break;
                case 13:
                    rollStart = Time.time;
                    Queue(Key.LeftShift, Key.E);
                    break;
                case 14:
                    Require(player.IsRolling && chest.Status == LootStatus.Closed && Scrap() == 7, "roll start blocks E");
                    Queue();
                    break;
                case 15:
                    if (Time.time - rollStart < 0.4f) { stage--; return; }
                    Require(!player.IsRolling && chest.Status == LootStatus.Closed, "roll input not reserved");
                    chest.transform.position = player.transform.position + new Vector3(-0.9f, 0, 0);
                    Physics.SyncTransforms();
                    Queue(Key.E);
                    break;
                case 16:
                    Require(chest.Status == LootStatus.Empty && Scrap() == 17, "fresh E works after roll");
                    Debug.Log($"B_CHEST_INTERACTION_PASS frame={Time.frameCount}: real E partial/full/retry/hold/empty/collider/2m/wall/roll");
                    Finish(0);
                    break;
            }
        }

        private static void Capture()
        {
            Directory.CreateDirectory("Artifacts");
            var camera = Camera.main;
            var canvas = Object.FindAnyObjectByType<Canvas>();
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Artifacts/chest-e-preview.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        private static void CheckCollider()
        {
            var collider = chest.GetComponent<BoxCollider>();
            Require(collider.enabled && collider.gameObject.activeInHierarchy &&
                    collider.bounds.Contains(chest.InteractionPoint), "stable collider and point inside after state change");
        }

        private static int Scrap()
        {
            int count = 0;
            for (int i = 0; i < inventory.UsedSlots; i++)
                if (inventory.GetStack(i).Item.ItemId == "scrap") count += inventory.GetStack(i).Count;
            return count;
        }

        private static void Queue(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            EditorApplication.Exit(code);
        }
    }
}
