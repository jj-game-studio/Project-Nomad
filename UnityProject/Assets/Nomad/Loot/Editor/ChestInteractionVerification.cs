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
        private static Mouse mouse;
        private static PlayerMovement player;
        private static PlayerInventory inventory;
        private static LootChest chest;
        private static GameObject wall;

        [MenuItem("Nomad/Loot/Open Chest Loot Preview")]
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
            foreach (var label in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.text.Contains("Add scrap"))
                    label.text = "E Open chest    F Fill backpack    G Free scrap    Tab Backpack";
            }

            chest = CreateChest("b-e-key-chest", player.transform.position + new Vector3(-0.9f, 0, 1.1f));
            var controls = chest.gameObject.AddComponent<LootPreview>();
            var data = new SerializedObject(controls);
            data.FindProperty("inventory").objectReferenceValue = inventory;
            data.FindProperty("chest").objectReferenceValue = chest;
            data.FindProperty("status").objectReferenceValue = status;
            data.FindProperty("keyboardControls").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = chest.gameObject;
            Debug.Log("B CHEST PREVIEW: Play + Game. E opens loot, double-click a row or use Take all. F fills inventory, G frees scrap.");
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
            target.Initialize(id, new[] { new LootStack("scrap", 10), new LootStack("field-vest", 1) });
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
                    mouse = InputSystem.AddDevice<Mouse>();
                    mouse.MakeCurrent();
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
                    Require(Panel() != null && chest.Status == LootStatus.Remaining && Scrap() == 96 &&
                        chest.GetStack(0).Count == 10 && calls == 1, "E opens both panels without transferring");
                    Require(((RectTransform)Panel().Find("Title")).rect.height > 0 &&
                        ((RectTransform)Panel().Find("Summary")).rect.height > 0 &&
                        ((RectTransform)Panel().Find("Close")).rect.height > 0, "loot header and close button have visible bounds");
                    CheckCollider();
                    break;
                case 2:
                    Require(calls == 1 && Panel() != null, "holding E does not repeat");
                    Queue();
                    Click("Viewport/Loot Stacks/Loot Stack 0", true);
                    break;
                case 3:
                    Click("Viewport/Loot Stacks/Loot Stack 0", false);
                    break;
                case 4:
                    Require(Scrap() == 96, "single click does not take a stack");
                    Click("Viewport/Loot Stacks/Loot Stack 0", true);
                    break;
                case 5:
                    Click("Viewport/Loot Stacks/Loot Stack 0", false);
                    break;
                case 6:
                    Require(Scrap() == 99 && chest.GetStack(0).Count == 7 && chest.GetStack(1).Count == 1 &&
                        calls == 2 && Feedback().Contains("Not enough space"), "double-click transfers only the selected stack");
                    Require(Panel().Find("Viewport/Loot Stacks/Loot Stack 0/Count")
                        .GetComponent<UnityEngine.UI.Text>().text == "×7", "chest row displays remaining quantity");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        Capture();
                    Click("Take All", true);
                    break;
                case 7:
                    Click("Take All", false);
                    break;
                case 8:
                    Require(calls == 3 && chest.GetStack(0).Count == 7 && chest.GetStack(1).Count == 1 &&
                        Feedback().Contains("Not enough"), "full inventory preserves all remaining items");
                    Queue(Key.G);
                    break;
                case 9:
                    Require(Scrap() == 0, "G frees a slot");
                    inventory.Remove("field-knife", 1);
                    Queue();
                    Click("Take All", true);
                    break;
                case 10:
                    Click("Take All", false);
                    break;
                case 11:
                    Require(Scrap() == 7 && chest.Status == LootStatus.Empty && calls == 4 &&
                        chest.GetStack(1).Count == 0 && !Panel().Find("Take All").GetComponent<UnityEngine.UI.Button>().interactable,
                        "Take all collects remaining items and disables the button");
                    CheckCollider();
                    Click("Close", true);
                    break;
                case 12:
                    Click("Close", false);
                    break;
                case 13:
                    Require(Panel() == null, "close button closes an open chest");
                    Queue(Key.E);
                    break;
                case 14:
                    Require(Panel() != null && Scrap() == 7 && calls == 4 && chest.Status == LootStatus.Empty,
                        "empty chest reopens for storage without changing quantities");
                    ClickBackpackScrap();
                    break;
                case 15:
                    Click("Viewport", true);
                    break;
                case 16:
                    Require(Panel().Find("Viewport").GetComponent<UnityEngine.UI.Outline>().enabled &&
                        Panel().parent.Find("DragPreview").gameObject.activeSelf, "drop area and dragged item are visible");
                    Require(Scrap() == 7 && chest.GetStack(0).Count == 0, "drag does not transfer before release");
                    Click("Viewport", false);
                    break;
                case 17:
                    Require(Scrap() == 0 && chest.GetStack(0).Count == 7 && calls == 5 &&
                        chest.Status == LootStatus.Remaining && Feedback() == "Stored 7", "drop stores the exact backpack stack");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        Capture("chest-store-preview");
                    inventory.Add("scrap", 96);
                    Click("Viewport/Loot Stacks/Loot Stack 0", true);
                    break;
                case 18:
                    ClickBackpackSlot(0, true);
                    break;
                case 19:
                    Require(Panel().parent.Find("DragPreview").gameObject.activeSelf &&
                        inventory.GetSlot(0).Count == 96 && chest.GetStack(0).Count == 7,
                        "loot drag previews the stack without transferring before release");
                    ClickBackpackSlot(0, false);
                    break;
                case 20:
                    Require(inventory.GetSlot(0).Count == 99 && chest.GetStack(0).Count == 4 && calls == 6 &&
                        Feedback().Contains("Took 3"), "drop on same item fills only the chosen stack");
                    Click("Viewport/Loot Stacks/Loot Stack 0", true);
                    break;
                case 21:
                    ClickBackpackSlot(1, true);
                    break;
                case 22:
                    ClickBackpackSlot(1, false);
                    break;
                case 23:
                    Require(inventory.GetSlot(1).Item.ItemId == "field-knife" && chest.GetStack(0).Count == 4 &&
                        calls == 6 && Feedback().Contains("another item"), "drop on another item preserves both stacks");
                    inventory.Remove("scrap", int.MaxValue);
                    inventory.Remove("field-knife", 1);
                    Click("Viewport/Loot Stacks/Loot Stack 0", true);
                    break;
                case 24:
                    ClickBackpackSlot(18, true);
                    break;
                case 25:
                    ClickBackpackSlot(18, false);
                    break;
                case 26:
                    Require(inventory.GetSlot(0).Item == null && inventory.GetSlot(18).Count == 4 &&
                        chest.GetStack(0).Count == 0 && calls == 7 && chest.Status == LootStatus.Empty,
                        "loot drop goes to the selected empty slot and closes the round trip");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        Capture("chest-to-backpack-preview");
                    chest.gameObject.SetActive(false);
                    chest = CreateChest("b-gate-check", player.transform.position + Vector3.forward * 3);
                    Queue();
                    break;
                case 27:
                    Queue(Key.E);
                    break;
                case 28:
                    Require(Panel() == null && chest.Status == LootStatus.Closed, "outside reach excluded");
                    chest.transform.position = player.transform.position + new Vector3(-0.9f, 0, 0);
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = player.transform.position + new Vector3(-0.45f, 0.6f, 0);
                    wall.transform.localScale = new Vector3(0.1f, 1.2f, 0.8f);
                    Physics.SyncTransforms();
                    Queue();
                    break;
                case 29:
                    Queue(Key.E);
                    break;
                case 30:
                    Require(Panel() == null && chest.Status == LootStatus.Closed, "wall blocks real chest");
                    wall.SetActive(false);
                    Physics.SyncTransforms();
                    Queue();
                    break;
                case 31:
                    rollStart = Time.time;
                    Queue(Key.LeftShift, Key.E);
                    break;
                case 32:
                    Require(player.IsRolling && chest.Status == LootStatus.Closed && Panel() == null, "roll start blocks E");
                    Queue();
                    break;
                case 33:
                    if (Time.time - rollStart < 0.4f) { stage--; return; }
                    Require(!player.IsRolling && chest.Status == LootStatus.Closed, "roll input is not reserved");
                    chest.transform.position = player.transform.position + new Vector3(-0.9f, 0, 0);
                    Physics.SyncTransforms();
                    Queue(Key.E);
                    break;
                case 34:
                    Require(chest.Status == LootStatus.Remaining && Panel() != null, "fresh E opens after roll");
                    ClickBackpackScrap();
                    break;
                case 35:
                    Click("Viewport", true);
                    break;
                case 36:
                    chest.transform.position += Vector3.forward * 3;
                    Physics.SyncTransforms();
                    Click("Viewport", false);
                    break;
                case 37:
                    Require(Panel() == null && Scrap() == 4 && chest.GetStack(0).Count == 10,
                        "moving the chest out of reach closes the panel and preserves its contents");
                    Debug.Log($"B_CHEST_INTERACTION_PASS frame={Time.frameCount}: E open, double-click, Take all, hold, partial, full, empty, bidirectional drag, exact slot, merge, incompatible slot, stale drop, collider, reach, wall, roll");
                    Finish(0);
                    break;
            }
        }

        private static Transform Panel() => GameObject.Find("ChestLootPanel")?.transform;

        private static string Feedback() => Panel().Find("Feedback").GetComponent<UnityEngine.UI.Text>().text;

        private static void Click(string target, bool pressed)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, Panel().Find(target).position);
            QueueMouse(point, pressed);
        }

        private static void ClickBackpackScrap()
        {
            int slot = 0;
            while (slot < PlayerInventory.Capacity && inventory.GetSlot(slot).Item?.ItemId != "scrap")
                slot++;

            Require(slot < PlayerInventory.Capacity, "backpack has scrap to drag");
            ClickBackpackSlot(slot, true);
        }

        private static void ClickBackpackSlot(int slot, bool pressed)
        {
            Canvas.ForceUpdateCanvases();
            var grid = Panel().parent.Find("InventoryPanel/Slots");
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, grid.GetChild(slot).position);
            QueueMouse(point, pressed);
        }

        private static void QueueMouse(Vector2 position, bool pressed)
        {
            var state = new MouseState { position = position }.WithButton(MouseButton.Left, pressed);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private static void Capture(string name = "chest-loot-preview")
        {
            Directory.CreateDirectory("Artifacts");
            var camera = Camera.main;
            var canvas = Panel().GetComponentInParent<Canvas>();
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
                File.WriteAllBytes($"Artifacts/{name}.png", pixels.EncodeToPNG());
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
