using System;
using System.IO;
using System.Reflection;
using Nomad.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Inventory.Editor
{
    internal static class InventorySplitVerification
    {
        private const string SessionKey = "Nomad.Inventory.SplitVerify";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static PlayerInventory inventory;
        private static InventoryPanel view;
        private static RectTransform panel;
        private static RectTransform grid;
        private static RectTransform dialog;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static PlayerQuickSlots quickSlots;
        private static EquipmentPanel equipmentPanel;
        private static UnityEngine.UI.InputField amount;
        private static UnityEngine.UI.Slider slider;
        private static UnityEngine.UI.Button confirm;
        private static ItemDefinition item;
        private static int stage;
        private static int frame;
        private static int checks;
        private static double deadline;

        [MenuItem("Nomad/Inventory/Open Stack Split Preview")]
        public static void OpenPreview()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene("Assets/Nomad/Inventory/A_PlayerInventory.unity");
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
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "timeout");
                if (!Application.isPlaying || Time.frameCount <= frame + 1) return;
                frame = Time.frameCount;
                if (mouse == null)
                {
                    inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
                    if (inventory == null || inventory.UsedSlots == 0) return;
                    view = UnityEngine.Object.FindAnyObjectByType<InventoryPanel>();
                    panel = (RectTransform)view.transform.Find("InventoryPanel");
                    grid = (RectTransform)panel.Find("Slots");
                    item = inventory.GetSlot(0).Item;
                    VerifyCore();
                    quickSlots = inventory.GetComponent<PlayerQuickSlots>();
                    if (quickSlots == null) quickSlots = inventory.gameObject.AddComponent<PlayerQuickSlots>();
                    quickSlots.Register(0, 0);
                    inventory.gameObject.AddComponent<PlayerEquipment>();
                    equipmentPanel = inventory.gameObject.AddComponent<EquipmentPanel>();
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    mouse = InputSystem.AddDevice<Mouse>();
                    mouse.MakeCurrent();
                    Keys(Key.Tab);
                    return;
                }

                switch (++stage)
                {
                    case 1:
                        Check(panel.gameObject.activeSelf, "Tab opens bag");
                        view.SendMessage("OnApplicationFocus", true);
                        Canvas.ForceUpdateCanvases();
                        Keys(Key.LeftCtrl);
                        Pointer(Slot(0), true);
                        break;
                    case 2:
                        dialog = (RectTransform)view.transform.Find("InventorySplitDialog");
                        Check(dialog != null && dialog.gameObject.activeSelf, "Ctrl click opens split");
                        amount = dialog.Find("Card/Amount").GetComponent<UnityEngine.UI.InputField>();
                        slider = dialog.Find("Card/Slider").GetComponent<UnityEngine.UI.Slider>();
                        confirm = dialog.Find("Card/Confirm").GetComponent<UnityEngine.UI.Button>();
                        Check(amount.text == "48" && slider.value == 48, "half default");
                        Check(slider.minValue == 1 && slider.maxValue == 95 && slider.wholeNumbers, "slider range");
                        Check(inventory.GetSlot(0).Count == 96 && inventory.UsedSlots == 3, "open does not mutate");
                        Check(!view.transform.Find("InventoryTooltip").gameObject.activeSelf, "tooltip hidden");
                        Check((int)typeof(EquipmentPanel).GetField("selected", Private).GetValue(equipmentPanel) == -1, "opening click does not select equipment source");
                        Pointer(Slot(0)); Keys();
                        break;
                    case 3:
                        amount.text = "0";
                        Check(!confirm.interactable, "zero disabled");
                        amount.text = "96";
                        Check(!confirm.interactable, "full stack disabled");
                        amount.text = "9999999999";
                        Check(!confirm.interactable, "overflow disabled");
                        amount.text = "";
                        Check(!confirm.interactable, "empty disabled");
                        amount.text = "56";
                        Check(confirm.interactable && slider.value == 56, "numeric input sync");
                        Check(dialog.Find("Card/Remaining").GetComponent<UnityEngine.UI.Text>().text.Contains("40"), "remaining count");
                        slider.value = 1;
                        Check(amount.text == "1", "slider sync minimum");
                        slider.value = 95;
                        Check(amount.text == "95", "slider sync maximum");
                        Pointer(ScreenPoint((RectTransform)slider.transform, -.3f), true);
                        break;
                    case 4:
                        Check(slider.value > 1 && slider.value < 48, "actual slider pointer changes value");
                        Pointer(Slot(1));
                        Keys();
                        break;
                    case 5:
                        Pointer(Slot(1), true);
                        break;
                    case 6:
                        Check((int)typeof(EquipmentPanel).GetField("selected", Private).GetValue(equipmentPanel) == -1, "modal blocks bag equipment selection");
                        Check(inventory.UsedSlots == 3 && inventory.GetSlot(0).Count == 96, "modal background click does not move");
                        amount.text = "56";
                        Pointer(Slot(1)); Keys(Key.Enter);
                        break;
                    case 7:
                        Check(!dialog.gameObject.activeSelf, "Enter confirms");
                        Check(inventory.GetSlot(0).Count == 40 && inventory.GetSlot(3).Item == item && inventory.GetSlot(3).Count == 56, "actual input splits first empty slot");
                        Check(quickSlots.GetItem(0) == item && quickSlots.GetCount(0) == 96, "quickslot registration and total unchanged");
                        Keys(); Pointer(Slot(0));
                        break;
                    case 8:
                        Keys(Key.RightCtrl); Pointer(Slot(0), true);
                        break;
                    case 9:
                        Check(dialog.gameObject.activeSelf && amount.text == "20", "right Ctrl opens current count");
                        Pointer(Slot(0)); Keys(Key.Escape);
                        break;
                    case 10:
                        Check(!dialog.gameObject.activeSelf && panel.gameObject.activeSelf && inventory.GetSlot(0).Count == 40, "Esc cancels only split");
                        Keys();
                        break;
                    case 11:
                        Keys(Key.LeftCtrl); Pointer(Slot(0), true);
                        break;
                    case 12:
                        Check(dialog.gameObject.activeSelf, "reopen for stale source");
                        inventory.Remove(item.ItemId, 57);
                        Check(!confirm.interactable && inventory.GetSlot(0).Count == 39, "source changes invalidate dialog");
                        confirm.onClick.Invoke();
                        Check(inventory.GetSlot(0).Count == 39 && inventory.UsedSlots == 3, "invalid confirmation does not mutate");
                        Pointer(Slot(0)); Keys(Key.Escape);
                        break;
                    case 13:
                        Keys();
                        break;
                    case 14:
                        Keys(Key.LeftCtrl); Pointer(Slot(0), true);
                        break;
                    case 15:
                        Check(amount.text == "19", "odd count defaults down to half");
                        view.SendMessage("OnApplicationFocus", false);
                        Check(!dialog.gameObject.activeSelf && inventory.GetSlot(0).Count == 39, "focus loss cancels");
                        view.SendMessage("OnApplicationFocus", true);
                        Keys(); Pointer(Slot(0));
                        break;
                    case 16:
                        Keys(Key.LeftCtrl); Pointer(Slot(0), true);
                        break;
                    case 17:
                        Keys(Key.Tab); Pointer(Slot(0));
                        break;
                    case 18:
                        Check(!panel.gameObject.activeSelf && !dialog.gameObject.activeSelf, "Tab closes bag and split");
                        Keys();
                        break;
                    case 19:
                        Keys(Key.Tab);
                        break;
                    case 20:
                        Keys(); Pointer(Slot(1));
                        break;
                    case 21:
                        Keys(Key.LeftCtrl); Pointer(Slot(1), true);
                        break;
                    case 22:
                        Check(!dialog.gameObject.activeSelf, "single item cannot split");
                        Keys(); Pointer(Slot(0));
                        FillBag();
                        break;
                    case 23:
                        Keys(Key.LeftCtrl); Pointer(Slot(0), true);
                        break;
                    case 24:
                        Check(dialog.gameObject.activeSelf && !confirm.interactable, "full bag dialog disabled");
                        Check(dialog.Find("Card/Feedback").GetComponent<UnityEngine.UI.Text>().text.Contains("빈 가방칸"), "full bag feedback");
                        int total = quickSlots.GetCount(0);
                        confirm.onClick.Invoke(); confirm.onClick.Invoke();
                        Check(quickSlots.GetCount(0) == total, "repeated invalid confirm preserves total");
                        Pointer(Slot(0)); Keys(Key.Escape);
                        break;
                    case 25:
                        inventory.RemoveFromSlotForVerification(19);
                        Keys();
                        break;
                    case 26:
                        Keys(Key.LeftCtrl); Pointer(Slot(0), true);
                        break;
                    case 27:
                        Check(dialog.gameObject.activeSelf && confirm.interactable, "free space restores split on reopen");
                        amount.text = "1";
                        Pointer(Slot(0)); Keys();
                        Render();
                        break;
                    case 28:
                        confirm.onClick.Invoke(); confirm.onClick.Invoke();
                        Check(inventory.GetSlot(0).Count == 38 && inventory.GetSlot(19).Count == 1, "repeated valid confirm commits once");
                        Check(!dialog.gameObject.activeSelf, "button confirms and closes");
                        Debug.Log($"INVENTORY_SPLIT_PASS checks={checks}");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"INVENTORY_SPLIT_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static void VerifyCore()
        {
            var fixture = new GameObject("SplitCoreFixture").AddComponent<PlayerInventory>();
            MethodInfo add = typeof(PlayerInventory).GetMethod("AddToSlotForTransfer", Private);
            add.Invoke(fixture, new object[] { 5, item, 9 });
            add.Invoke(fixture, new object[] { 0, item, 3 });
            int notifications = 0;
            fixture.Changed += () =>
            {
                notifications++;
                Check(fixture.GetSlot(5).Count == 5 && fixture.GetSlot(1).Count == 4, "notification sees both completed slots");
            };
            Check(fixture.TrySplit(5, 4, item, 9, out int target, out _) && target == 1, "first empty ignores same item partial stack");
            Check(fixture.GetSlot(0).Count == 3 && fixture.UsedSlots == 3 && notifications == 1, "one notification and slot order");
            Check(fixture.GetStack(2).Count == 4, "stack iteration includes new slot");
            foreach (int invalid in new[] { -1, 0, 5, 6, int.MaxValue })
                Check(!fixture.TrySplit(5, invalid, item, 5, out _, out _), "invalid amount rejected");
            Check(!fixture.TrySplit(5, 1, item, 9, out _, out _), "stale snapshot rejected");
            Check(!fixture.TrySplit(2, 1, item, 5, out _, out _), "empty source rejected");
            Check(!fixture.TrySplit(-1, 1, item, 5, out _, out _), "negative index rejected");
            Check(!fixture.TrySplit(20, 1, item, 5, out _, out _), "large index rejected");
            Check(notifications == 1 && fixture.GetSlot(5).Count == 5, "failures do not notify or mutate");
            for (int index = 0; index < PlayerInventory.Capacity; index++)
                if (fixture.GetSlot(index).Item == null) add.Invoke(fixture, new object[] { index, item, 1 });
            Check(!fixture.TrySplit(5, 1, item, 5, out _, out string error) && error.Contains("빈 가방칸"), "full bag rejected");
            Check(fixture.GetSlot(5).Count == 5 && notifications == 1, "full bag preserves source");
            UnityEngine.Object.Destroy(fixture.gameObject);

            var minimum = new GameObject("SplitMinimumFixture").AddComponent<PlayerInventory>();
            add.Invoke(minimum, new object[] { 0, item, 2 });
            Check(minimum.TrySplit(0, 1, item, 2, out int last, out _) && last == 1, "two items split into one and one");
            Check(minimum.GetSlot(0).Count == 1 && minimum.GetSlot(1).Count == 1, "minimum source preserved");
            UnityEngine.Object.Destroy(minimum.gameObject);
        }

        private static void FillBag()
        {
            MethodInfo add = typeof(PlayerInventory).GetMethod("AddToSlotForTransfer", Private);
            for (int index = 0; index < PlayerInventory.Capacity; index++)
                if (inventory.GetSlot(index).Item == null) add.Invoke(inventory, new object[] { index, item, 1 });
            typeof(PlayerInventory).GetMethod("NotifyChanged", Private).Invoke(inventory, null);
        }

        private static void RemoveFromSlotForVerification(this PlayerInventory target, int slot)
        {
            typeof(PlayerInventory).GetMethod("RemoveFromSlotForTransfer", Private).Invoke(target, new object[] { slot, target.GetSlot(slot).Count });
            typeof(PlayerInventory).GetMethod("NotifyChanged", Private).Invoke(target, null);
        }

        private static void Render()
        {
            // 실제 포인터 검증과 별개로 최종 표시 상태를 세 해상도에 렌더합니다.
            Canvas canvas = view.GetComponent<Canvas>();
            var cameraObject = new GameObject("SplitRenderCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(31, 36, 39, 255);
            camera.cullingMask = 1 << 5;
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            object presenter = typeof(InventoryPanel).GetField("splitDialog", Private).GetValue(view);
            Directory.CreateDirectory("Artifacts/inventory-split");
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1080) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                presenter.GetType().GetMethod("Tick").Invoke(presenter, null);
                Canvas.ForceUpdateCanvases();
                Vector3[] corners = new Vector3[4];
                ((RectTransform)dialog.Find("Card")).GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 pixel = camera.WorldToScreenPoint(corner);
                    Check(pixel.x >= 0 && pixel.x <= size.x && pixel.y >= 0 && pixel.y <= size.y, "card fits screen");
                }
                camera.Render();
                RenderTexture.active = target;
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                pixels.Apply();
                File.WriteAllBytes($"Artifacts/inventory-split/split-{size.x}.png", pixels.EncodeToPNG());
                RenderTexture.active = null;
                camera.targetTexture = null;
                UnityEngine.Object.Destroy(pixels);
                UnityEngine.Object.Destroy(target);
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            UnityEngine.Object.Destroy(cameraObject);
        }

        private static Vector2 Slot(int slot) => RectTransformUtility.WorldToScreenPoint(null, grid.GetChild(slot).position);
        private static Vector2 ScreenPoint(RectTransform rect, float x) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.width * x, 0)));
        private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static void Pointer(Vector2 position, bool pressed = false) => InputSystem.QueueStateEvent(mouse,
            new MouseState { position = position }.WithButton(MouseButton.Left, pressed));

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks++;
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            EditorApplication.Exit(code);
        }
    }
}
