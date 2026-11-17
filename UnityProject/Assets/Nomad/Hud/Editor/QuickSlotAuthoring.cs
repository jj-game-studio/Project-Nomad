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

            Pointer(Bag(1), true);
            yield return null;
            Pointer(Slot(0), true);
            yield return null;
            Pointer(Slot(0), false);
            yield return null;
            Check(quick.GetItem(0).ItemId == "field-knife" && inventoryChanges == 0, "replace existing registration");
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
            IEnumerator capture = Capture();
            while (capture.MoveNext())
                yield return capture.Current;
            Debug.Log($"QUICK_SLOTS_PLAYMODE_PASS checks={checks} frame={Time.frameCount}");
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
