using System;
using System.Collections;
using System.IO;
using Nomad.Combat;
using Nomad.Inventory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Hud.Editor
{
    public static class QuickSlotAuthoring
    {
        public const string ScenePath = "Assets/Nomad/Hud/QuickSlotPreview.unity";
        private const string VerifyKey = "Nomad.QuickSlots.Verify";
        private static IEnumerator steps;
        private static double deadline;
        private static int frame;
        private static int checks;
        private static bool failed;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static Keyboard previousKeyboard;
        private static Mouse previousMouse;
        private static bool previousRunInBackground;
        private static InputSettings.BackgroundBehavior previousBackground;
        private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private static PlayerInventory inventory;
        private static PlayerHud hud;
        private static RectTransform grid;
        private static RectTransform panel;

        [MenuItem("Nomad/HUD/Create Quick Slot Preview")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene("Assets/Nomad/Inventory/A_PlayerInventory.unity");
            GameObject player = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>().gameObject;
            if (player.GetComponent<Health>() == null)
                player.AddComponent<Health>();
            player.AddComponent<PlayerHud>();

            // 기존 데모 안내를 위로 옮겨 하단 퀵슬롯을 가리지 않도록 합니다.
            foreach (var label in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>())
            {
                if (label.name != "Controls")
                    continue;
                RectTransform rect = (RectTransform)label.transform.parent;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
                rect.anchoredPosition = new Vector2(0, -16);
                rect.sizeDelta = new Vector2(1200, 92);
                label.text = "TAB: Backpack    Drag items to quick slots 1-4    1/2/3: demo add/remove";
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Nomad/HUD/Open Quick Slot Preview")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!File.Exists(ScenePath))
                Create();
            else
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Nomad/HUD/Verify Quick Slots")]
        public static void Verify()
        {
            Create();
            SessionState.SetBool(VerifyKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(VerifyKey, false))
                return;

            steps = null;
            failed = false;
            checks = 0;
            frame = -1;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayState;
            EditorApplication.playModeStateChanged += PlayState;
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "verification deadline");
                EditorApplication.QueuePlayerLoopUpdate();
                if (!Application.isPlaying || Time.frameCount < 8 || Time.frameCount == frame)
                    return;

                frame = Time.frameCount;
                steps ??= Exercise();
                if (!steps.MoveNext())
                    Finish();
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
                Finish();
            }
        }

        private static IEnumerator Exercise()
        {
            inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
            hud = inventory.GetComponent<PlayerHud>();
            var view = UnityEngine.Object.FindAnyObjectByType<InventoryPanel>();
            panel = (RectTransform)view.transform.Find("InventoryPanel");
            grid = (RectTransform)panel.Find("Slots");
            PlayerQuickSlots quick = hud.QuickSlots;
            Check(quick != null && inventory.UsedSlots == 3, "inventory and HUD connected");

            previousKeyboard = Keyboard.current;
            previousMouse = Mouse.current;
            previousRunInBackground = Application.runInBackground;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            QueueKey(Key.Tab);
            yield return null;
            QueueKey();
            yield return null;
            Check(panel.gameObject.activeSelf, "Tab opens inventory");
            Canvas.ForceUpdateCanvases();
            var controls = GameObject.Find("DemoControls").GetComponent<RectTransform>();
            Check(RectTransformUtility.WorldToScreenPoint(null, controls.position).y > Screen.height / 2f,
                "preview controls stay above HUD");

            int inventoryChanges = 0;
            inventory.Changed += () => inventoryChanges++;
            Check(!quick.Register(-1, 0) && !quick.Register(20, 0) && !quick.Register(0, 4) &&
                !quick.Register(19, 0), "invalid and empty source rejected");
            for (int index = 0; index < PlayerQuickSlots.Capacity; index++)
            {
                Pointer(Bag(0), true);
                yield return null;
                Pointer(Slot(index), true);
                yield return null;
                Check(SlotRect(index).Find("Drop Highlight").gameObject.activeSelf, "drop target highlighted");
                Pointer(Slot(index), false);
                yield return null;
                Check(quick.GetItem(index) == inventory.GetSlot(0).Item && quick.GetCount(index) == 96,
                    "real pointer drag registers slot " + index);
                Check(!SlotRect(index).Find("Drop Highlight").gameObject.activeSelf &&
                    !panel.Find("DragPreview").gameObject.activeSelf, "release clears drag visuals");
                Check(SlotRect(index).Find("Item Icon").GetComponent<UnityEngine.UI.Image>().sprite == inventory.GetSlot(0).Item.Icon,
                    "registered icon displayed");
            }
            Check(inventoryChanges == 0 && inventory.GetSlot(0).Count == 96, "registration does not move or consume");
            IEnumerator unregister = VerifyUnregister(view);
            while (unregister.MoveNext())
                yield return unregister.Current;
            Check(inventoryChanges == 0, "unregister never raises inventory change");


            Pointer(Bag(1), true);
            yield return null;
            Pointer(Slot(0), true);
            yield return null;
            Pointer(Slot(0), false);
            yield return null;
            Check(quick.GetItem(0).ItemId == "field-knife" && inventoryChanges == 0, "replace existing registration");
            IEnumerator equipment = VerifyEquipmentDisplay();
            while (equipment.MoveNext())
                yield return equipment.Current;
            Check(inventory.MoveStack(1, 19) && quick.GetItem(0).ItemId == "field-knife", "registration follows moved item");
            inventory.Add("scrap", 10);
            Check(quick.GetCount(1) == 106 && SlotRect(1).Find("Item Count").GetComponent<TMP_Text>().text == "×106",
                "count sums all stacks and refreshes HUD");

            Pointer(Bag(2), true);
            yield return null;
            Pointer(Slot(0), true);
            yield return null;
            QueueKey(Key.Escape);
            yield return null;
            Pointer(Slot(0), false);
            QueueKey();
            yield return null;
            Check(quick.GetItem(0).ItemId == "field-knife", "Escape cancels without replacing");

            Pointer(Bag(2), true);
            yield return null;
            Pointer(Slot(0), true);
            yield return null;
            QueueKey(Key.Tab);
            yield return null;
            Pointer(Slot(0), false);
            QueueKey();
            yield return null;
            Check(!panel.gameObject.activeSelf && quick.GetItem(0).ItemId == "field-knife", "closing cancels pending drop");
            QueueKey(Key.Tab);
            yield return null;
            QueueKey();
            yield return null;

            Pointer(Bag(2), true);
            yield return null;
            Pointer(Slot(0), true);
            yield return null;
            inventory.Remove("field-vest", 1);
            Pointer(Slot(0), false);
            yield return null;
            Check(quick.GetItem(0).ItemId == "field-knife", "inventory change cancels stale drag");

            inventory.Add("field-vest", 1);

            Pointer(Bag(2), true);
            yield return null;
            Pointer(new Vector2(5, Screen.height - 5), true);
            yield return null;
            Pointer(new Vector2(5, Screen.height - 5), false);
            yield return null;
            Check(quick.GetItem(0).ItemId == "field-knife" && inventory.GetSlot(2).Item.ItemId == "field-vest",
                "drop outside keeps registration and inventory");

            Pointer(Bag(2), true);
            yield return null;
            Pointer(Slot(0), true);
            yield return null;
            view.SendMessage("OnApplicationFocus", false);
            Pointer(Slot(0), false);
            yield return null;
            Check(quick.GetItem(0).ItemId == "field-knife" && !SlotRect(0).Find("Drop Highlight").gameObject.activeSelf,
                "focus loss cancels drag and highlight");

            hud.enabled = false;
            Check(hud.FindQuickSlot(Slot(0)) == -1, "hidden HUD rejects drops");
            inventory.Remove("scrap", 10);
            hud.enabled = true;
            Check(quick.GetCount(1) == 96 && SlotRect(1).Find("Item Count").GetComponent<TMP_Text>().text == "×96",
                "HUD reenable refreshes current count");
            inventory.Remove("field-knife", 1);
            Check(quick.GetItem(0) == null, "last item removal clears registration");
            inventory.Remove("scrap", int.MaxValue);
            Check(quick.GetItem(1) == null && quick.GetItem(2) == null && quick.GetItem(3) == null,
                "depletion clears all duplicates");

            inventory.Add("field-knife", PlayerInventory.Capacity);
            inventory.Add("scrap", int.MaxValue);
            Check(inventory.UsedSlots == PlayerInventory.Capacity, "full backpack prepared");
            quick.Register(0, 1);
            var fullBag = SnapshotInventory();
            Pointer(Slot(1), true);
            yield return null;
            Pointer(Bag(0), true);
            yield return null;
            Pointer(Bag(0), false);
            yield return null;
            Check(quick.GetItem(1) == null && SameInventory(fullBag), "full backpack unassigns without moving items");
            inventory.Remove("scrap", int.MaxValue);
            inventory.Remove("field-knife", int.MaxValue);

            // 최종 렌더는 등록된 아이템과 열린 가방을 함께 보여줍니다.
            inventory.Add("scrap", 106);
            inventory.Add("field-knife", 1);
            quick.Register(0, 1);
            for (int index = 0; index < PlayerInventory.Capacity; index++)
            {
                if (inventory.GetSlot(index).Item?.ItemId == "field-knife")
                    quick.Register(index, 0);
            }
            yield return null;
            inventory.Add("field-knife", 2);
            inventory.Add("field-vest", 1);
            for (int index = 0; index < PlayerInventory.Capacity; index++)
            {
                if (inventory.GetSlot(index).Item?.ItemId == "field-vest")
                    quick.Register(index, 3);
            }
            yield return null;
            Check(EquipmentIconOnly(0) && EquipmentIconOnly(3), "final multiple-equipment display has no quantity labels");
            IEnumerator capture = Capture();
            while (capture.MoveNext())
                yield return capture.Current;
            Debug.Log($"QUICK_SLOTS_PLAYMODE_PASS checks={checks} frame={Time.frameCount}");
        }

        private static IEnumerator VerifyEquipmentDisplay()
        {
            PlayerQuickSlots quick = hud.QuickSlots;
            Check(EquipmentIconOnly(0), "single weapon has no x1 label");
            quick.Register(2, 3);
            Check(EquipmentIconOnly(3), "single armor has no x1 label");
            inventory.Add("field-knife", 3);
            inventory.Add("field-vest", 2);
            Check(quick.GetCount(0) == 4 && EquipmentIconOnly(0), "multiple weapons show one icon without total count");
            Check(quick.GetCount(3) == 3 && EquipmentIconOnly(3), "multiple armor show one icon without total count");
            Check(inventory.UsedSlots == 8, "equipment still occupies separate backpack slots");
            for (int index = 0; index < PlayerInventory.Capacity; index++)
            {
                PlayerInventory.ItemStack stack = inventory.GetSlot(index);
                Check(stack.Item == null || stack.Item.MaxStack > 1 || stack.Count == 1, "backpack equipment never stacks");
            }

            var snapshot = SnapshotInventory();
            foreach (int index in new[] { 0, 3 })
            {
                Pointer(Slot(index), true);
                yield return null;
                Pointer(Bag(0), true);
                yield return null;
                Check(panel.Find("DragPreview/Count").GetComponent<UnityEngine.UI.Text>().text == string.Empty,
                    "equipment drag preview omits aggregate count");
                Pointer(new Vector2(5, Screen.height - 5), false);
                yield return null;
                Check(EquipmentIconOnly(index) && SameInventory(snapshot), "equipment preview cancellation preserves all copies");
            }
            Check(SlotRect(1).Find("Item Count").GetComponent<TMP_Text>().text == "×96",
                "material count remains visible alongside equipment");
            inventory.Remove("field-knife", 3);
            inventory.Remove("field-vest", 2);
            Check(EquipmentIconOnly(0) && EquipmentIconOnly(3), "equipment remains icon only after partial removal");
            quick.Register(0, 3);
            Debug.Log("QUICK_SLOTS_EQUIPMENT_DISPLAY_PASS: one and multiple weapons/armor; icon only; backpack and material counts preserved");
        }

        private static bool EquipmentIconOnly(int index)
        {
            Transform slot = SlotRect(index);
            var count = slot.Find("Item Count").GetComponent<TMP_Text>();
            var icon = slot.Find("Item Icon").GetComponent<UnityEngine.UI.Image>();
            return !count.gameObject.activeSelf && count.text == string.Empty && icon.enabled &&
                icon.sprite == hud.QuickSlots.GetItem(index).Icon &&
                slot.GetComponentsInChildren<UnityEngine.UI.Image>().Length == 1;
        }

        private static IEnumerator VerifyUnregister(InventoryPanel view)
        {
            PlayerQuickSlots quick = hud.QuickSlots;
            var before = SnapshotInventory();
            Check(!quick.Unregister(-1) && !quick.Unregister(PlayerQuickSlots.Capacity), "invalid unassign rejected");
            Pointer(Slot(1), true);
            yield return null;
            Pointer(Slot(1), false);
            yield return null;
            Check(quick.GetItem(1) != null, "click without drag retains registration");

            Vector2 padding = RectTransformUtility.WorldToScreenPoint(null,
                panel.TransformPoint(new Vector3(panel.rect.xMin + 5, panel.rect.yMin + 5)));
            Vector2[] targets = { Bag(0), Bag(19),
                RectTransformUtility.WorldToScreenPoint(null, panel.Find("Capacity").position), padding };
            for (int index = 0; index < PlayerQuickSlots.Capacity; index++)
            {
                Pointer(Slot(index), true);
                yield return null;
                Pointer(targets[index], true);
                yield return null;
                Check(panel.Find("DragPreview").gameObject.activeSelf &&
                    SlotRect(index).GetComponent<CanvasGroup>().alpha < 1f, "reverse drag preview and dimming");
                Check(quick.GetItem(index) != null, "reverse drag does not unassign until release");
                Pointer(targets[index], false);
                yield return null;
                Check(quick.GetItem(index) == null && SameInventory(before), "drop on backpack unassigns only shortcut " + index);
                Check(!panel.Find("DragPreview").gameObject.activeSelf &&
                    SlotRect(index).GetComponent<CanvasGroup>().alpha == 1f, "reverse drag visuals restored");
                for (int other = 0; other < PlayerQuickSlots.Capacity; other++)
                    Check(other == index || quick.GetItem(other) != null, "other duplicate registrations preserved");
                Check(!quick.Unregister(index), "empty shortcut cannot unassign again");
                quick.Register(0, index);
            }

            Vector2[] invalidTargets = { new Vector2(5, Screen.height - 5), Slot(3) };
            foreach (Vector2 target in invalidTargets)
            {
                Pointer(Slot(1), true);
                yield return null;
                Pointer(target, true);
                yield return null;
                Pointer(target, false);
                yield return null;
                Check(quick.GetItem(1) != null && SameInventory(before), "outside and quickslot drop preserve registration");
            }

            Pointer(Slot(1), true);
            yield return null;
            Pointer(Bag(0), true);
            yield return null;
            QueueKey(Key.Escape);
            yield return null;
            Pointer(Bag(0), false);
            QueueKey();
            yield return null;
            Check(quick.GetItem(1) != null && SameInventory(before), "reverse drag Escape cancellation");

            Pointer(Slot(1), true);
            yield return null;
            Pointer(Bag(0), true);
            yield return null;
            QueueKey(Key.Tab);
            yield return null;
            Pointer(Bag(0), false);
            QueueKey();
            yield return null;
            Check(!panel.gameObject.activeSelf && quick.GetItem(1) != null, "reverse drag closing cancellation");
            Pointer(Slot(1), true);
            yield return null;
            Pointer(Bag(0), true);
            yield return null;
            Pointer(Bag(0), false);
            yield return null;
            Check(quick.GetItem(1) != null, "closed backpack cannot begin reverse drag");
            QueueKey(Key.Tab);
            yield return null;
            QueueKey();
            yield return null;

            Pointer(Slot(1), true);
            yield return null;
            Pointer(Bag(0), true);
            yield return null;
            view.SendMessage("OnApplicationFocus", false);
            Pointer(Bag(0), false);
            yield return null;
            Check(quick.GetItem(1) != null && SlotRect(1).GetComponent<CanvasGroup>().alpha == 1f,
                "reverse drag focus cancellation restores source");

            Pointer(Slot(1), true);
            yield return null;
            Pointer(Bag(0), true);
            yield return null;
            hud.enabled = false;
            yield return null;
            Pointer(Bag(0), false);
            yield return null;
            hud.enabled = true;
            Check(quick.GetItem(1) != null && SameInventory(before), "hidden HUD cancels reverse drag");

            Pointer(Slot(1), true);
            yield return null;
            Pointer(Bag(0), true);
            yield return null;
            quick.Register(1, 1);
            Pointer(Bag(0), false);
            yield return null;
            Check(quick.GetItem(1) == inventory.GetSlot(1).Item, "replacement during drag is not unassigned");
            quick.Register(0, 1);
            Debug.Log("QUICK_SLOTS_UNREGISTER_PASS: all slots, occupied/empty/header/padding/full bag, cancellations, no inventory mutation");
        }

        private static PlayerInventory.ItemStack[] SnapshotInventory()
        {
            var snapshot = new PlayerInventory.ItemStack[PlayerInventory.Capacity];
            for (int index = 0; index < snapshot.Length; index++)
                snapshot[index] = inventory.GetSlot(index);
            return snapshot;
        }

        private static bool SameInventory(PlayerInventory.ItemStack[] snapshot)
        {
            for (int index = 0; index < snapshot.Length; index++)
            {
                PlayerInventory.ItemStack current = inventory.GetSlot(index);
                if (current.Item != snapshot[index].Item || current.Count != snapshot[index].Count)
                    return false;
            }
            return true;
        }

        private static RectTransform SlotRect(int index) =>
            (RectTransform)hud.Canvas.transform.Find($"Bottom Bar/Slot {index + 1}");

        private static Vector2 Slot(int index) => RectTransformUtility.WorldToScreenPoint(null, SlotRect(index).position);
        private static Vector2 Bag(int index) => RectTransformUtility.WorldToScreenPoint(null, grid.GetChild(index).position);

        private static void Pointer(Vector2 position, bool held) =>
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, held));

        private static void QueueKey(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Quick slots: " + message);
            // 프레임 대기용 시간 제한 검사는 결과 건수에서 제외합니다.
            if (message != "verification deadline")
                checks++;
        }

        private static IEnumerator Capture()
        {
            Camera camera = Camera.main;
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new RenderMode[canvases.Length];
            var cameras = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                for (int index = 0; index < canvases.Length; index++)
                {
                    modes[index] = canvases[index].renderMode;
                    cameras[index] = canvases[index].worldCamera;
                    distances[index] = canvases[index].planeDistance;
                    canvases[index].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[index].worldCamera = camera;
                    canvases[index].planeDistance = camera.nearClipPlane + 1;
                }

                camera.targetTexture = target;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts/quick-slots");
                File.WriteAllBytes("Artifacts/quick-slots/registered.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                for (int index = 0; index < canvases.Length; index++)
                {
                    canvases[index].renderMode = modes[index];
                    canvases[index].worldCamera = cameras[index];
                    canvases[index].planeDistance = distances[index];
                }

                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void Finish()
        {
            SessionState.SetBool(VerifyKey, false);
            EditorApplication.update -= Tick;
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                previousKeyboard?.MakeCurrent();
                previousMouse?.MakeCurrent();
                Application.runInBackground = previousRunInBackground;
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }

            EditorApplication.ExitPlaymode();
        }

        private static void PlayState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;
            EditorApplication.playModeStateChanged -= PlayState;
            if (Application.isBatchMode)
                EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
