using System;
using System.Collections;
using System.IO;
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
            UnityEngine.Object.FindAnyObjectByType<PlayerInventory>().gameObject.AddComponent<PlayerHud>();
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

            Pointer(Bag(0), true);
            yield return null;
            Pointer(Vector2.one * 3, true);
            yield return null;
            Pointer(Vector2.one * 3, false);
            yield return null;
            Check(quick.GetItem(0).ItemId == "field-knife" && inventory.GetSlot(0).Count == 99, "outside drop preserves state");

            hud.enabled = false;
            Check(hud.FindQuickSlot(Slot(0)) == -1, "disabled HUD rejects drops");
            inventory.Remove("scrap", int.MaxValue);
            inventory.Remove("field-knife", 1);
            hud.enabled = true;
            for (int index = 0; index < PlayerQuickSlots.Capacity; index++)
                Check(quick.GetItem(index) == null && SlotRect(index).Find("Empty Content").gameObject.activeSelf,
                    "depletion clears references and re-enable refreshes slot " + index);

            inventory.Add("scrap", 96);
            inventory.Add("field-knife", 1);
            quick.Register(0, 1);
            quick.Register(1, 0);
            Canvas.ForceUpdateCanvases();
            Capture();
            Debug.Log($"QUICK_SLOTS_VERIFY_PASS: {checks} checks; pointer registration/replacement/cancellation/counts/depletion.");
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

        private static void Capture()
        {
            Camera camera = Camera.main;
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new RenderMode[canvases.Length];
            var cameras = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            var pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
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
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
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
