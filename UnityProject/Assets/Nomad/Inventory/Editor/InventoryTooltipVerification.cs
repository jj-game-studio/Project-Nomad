using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Inventory.Editor
{
    internal static class InventoryTooltipVerification
    {
        private const string SessionKey = "Nomad.Inventory.TooltipVerify";
        private static double deadline;
        private static int frame;
        private static int stage;
        private static int checks;
        private static InventoryPanel view;
        private static PlayerInventory inventory;
        private static RectTransform panel;
        private static RectTransform grid;
        private static RectTransform tooltip;
        private static Mouse mouse;
        private static Keyboard keyboard;
        private static ItemDefinition item;
        private static Vector3 previousPosition;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Nomad/Inventory/A_PlayerInventory.unity");
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false))
                return;

            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "timeout");
                if (!Application.isPlaying || Time.frameCount <= frame + 1)
                    return;
                frame = Time.frameCount;

                if (mouse == null)
                {
                    inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
                    if (inventory == null || inventory.UsedSlots == 0)
                        return;

                    view = UnityEngine.Object.FindAnyObjectByType<InventoryPanel>();
                    panel = (RectTransform)view.transform.Find("InventoryPanel");
                    grid = (RectTransform)panel.Find("Slots");
                    tooltip = view.GetComponentInChildren<Canvas>().transform.Find("InventoryTooltip") as RectTransform;
                    if (tooltip == null)
                        tooltip = view.transform.Find("InventoryTooltip") as RectTransform;
                    Check(tooltip != null && !tooltip.gameObject.activeSelf, "initially hidden");
                    item = UnityEngine.Object.Instantiate(inventory.GetSlot(0).Item);
                    InvokeInventory("RemoveFromSlotForTransfer", 0, 96);
                    InvokeInventory("AddToSlotForTransfer", 0, item, 96);
                    InvokeInventory("NotifyChanged");
                    // 런타임 인스턴스만 복제하여 원본 CSV/에셋을 수정하지 않습니다.
                    var serialized = new SerializedObject(item);
                    serialized.FindProperty("description").stringValue = "Recovered material.\nCan be stored or traded.";
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode =
                        InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
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
                        Pointer(Slot(0));
                        Keys();
                        break;
                    case 2:
                        Check(Visible && Title.Contains(item.DisplayName), "hover name");
                        Check(Body.Contains("Recovered material") && Body.Contains("\n"), "multiline description");
                        Check(!tooltip.GetComponent<CanvasGroup>().blocksRaycasts, "does not intercept input");
                        foreach (var graphic in tooltip.GetComponentsInChildren<UnityEngine.UI.Text>())
                            Check(!graphic.raycastTarget && (!graphic.supportRichText || graphic.name == "ComparisonStats"), "safe noninteractive text");
                        previousPosition = tooltip.position;
                        Pointer(Slot(0) + Vector2.right * 8);
                        break;
                    case 3:
                        Check(Visible && tooltip.position != previousPosition, "follows within same slot");
                        InvokeInventory("RemoveFromSlotForTransfer", 0, 1);
                        InvokeInventory("NotifyChanged");
                        break;
                    case 4:
                        Check(Title.Contains("95"), "stationary count refresh");
                        Pointer(Slot(0), true);
                        break;
                    case 5:
                        Check(!Visible, "hidden on press");
                        Pointer(Slot(19), true);
                        break;
                    case 6:
                        Check(!Visible, "hidden during bag drag");
                        Pointer(Slot(19));
                        break;
                    case 7:
                        Check(inventory.GetSlot(19).Item == item && inventory.GetSlot(0).Item == null,
                            "existing drag movement preserved");
                        Pointer(Slot(0));
                        break;
                    case 8:
                        Check(!Visible, "empty slot hidden");
                        Pointer(Slot(19));
                        break;
                    case 9:
                        Check(Visible, "hover after move");
                        view.SendMessage("OnApplicationFocus", false);
                        Check(!Visible, "focus loss hides immediately");
                        break;
                    case 10:
                        Check(!Visible, "stays hidden without focus");
                        view.SendMessage("OnApplicationFocus", true);
                        break;
                    case 11:
                        Check(Visible, "focus return restores hover");
                        Keys(Key.Tab);
                        break;
                    case 12:
                        Check(!Visible && !panel.gameObject.activeSelf, "Tab close hides");
                        Keys();
                        Keys(Key.Tab);
                        break;
                    case 13:
                        Keys();
                        Pointer(Slot(19));
                        break;
                    case 14:
                        Check(Visible, "reopen hover");
                        view.enabled = false;
                        Check(!Visible, "component disabled hides immediately");
                        view.enabled = true;
                        InvokeInventory("RemoveFromSlotForTransfer", 19, 95);
                        InvokeInventory("NotifyChanged");
                        break;
                    case 15:
                        Check(!Visible, "removed item hidden without pointer move");
                        VerifyStats();
                        VerifyBounds();
                        RenderPreview();
                        Debug.Log($"INVENTORY_TOOLTIP_PASS checks={checks} frames={frame}");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"INVENTORY_TOOLTIP_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static void InvokeInventory(string method, params object[] arguments)
        {
            typeof(PlayerInventory).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(inventory, arguments);
        }

        private static bool Visible => tooltip.gameObject.activeInHierarchy;
        private static string Title => tooltip.Find("Title").GetComponent<UnityEngine.UI.Text>().text;
        private static string Body => tooltip.Find("Description").GetComponent<UnityEngine.UI.Text>().text;

        private static void VerifyStats()
        {
            object presenter = typeof(InventoryPanel).GetField("tooltip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            Action show = () => presenter.GetType().GetMethod("Show").Invoke(presenter,
                new object[] { item, 1, Slot(0), null });
            var data = new SerializedObject(item);
            data.FindProperty("equipmentType").enumValueIndex = (int)EquipmentType.Weapon;
            data.FindProperty("attackPower").intValue = 25;
            data.FindProperty("defense").intValue = 5;
            data.ApplyModifiedPropertiesWithoutUndo();
            show();
            Check(Body.Contains("공격력 25") && Body.Contains("방어력 5") && Body.Contains(item.Description),
                "weapon stats and description");

            data.FindProperty("attackPower").intValue = 35;
            data.FindProperty("defense").intValue = 10;
            data.ApplyModifiedPropertiesWithoutUndo();
            show();
            Check(Body.Contains("공격력 35") && Body.Contains("방어력 10") && !Body.Contains("공격력 25"),
                "stationary stat refresh on same item");

            data.FindProperty("equipmentType").enumValueIndex = (int)EquipmentType.Body;
            data.FindProperty("description").stringValue = string.Empty;
            data.ApplyModifiedPropertiesWithoutUndo();
            show();
            Check(Body == "방어력 10", "armor defense without attack or blank description gap");

            data.FindProperty("defense").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            show();
            Check(Body == string.Empty && !tooltip.Find("Description").gameObject.activeSelf,
                "zero stats hidden");

            data.FindProperty("equipmentType").enumValueIndex = (int)EquipmentType.None;
            data.FindProperty("attackPower").intValue = 99;
            data.FindProperty("defense").intValue = 99;
            data.FindProperty("description").stringValue = "Material description";
            data.ApplyModifiedPropertiesWithoutUndo();
            show();
            Check(Body == "Material description", "non equipment hides unused stats");

            // 원본 에셋 대신 복제본으로 능력치가 있는 긴 툴팁의 경계·렌더를 확인합니다.
            data.FindProperty("equipmentType").enumValueIndex = (int)EquipmentType.Weapon;
            data.FindProperty("attackPower").intValue = 25;
            data.FindProperty("defense").intValue = 5;
            data.FindProperty("displayName").stringValue = "검";
            data.FindProperty("description").stringValue = "전방의 여러 대상을 타격하는 검";
            data.ApplyModifiedPropertiesWithoutUndo();
            show();
        }

        private static void VerifyBounds()
        {
            var parent = new GameObject("TooltipBoundsFixture", typeof(RectTransform)).GetComponent<RectTransform>();
            parent.SetParent(tooltip.parent, false);
            parent.anchorMin = parent.anchorMax = Vector2.one * 0.5f;
            Type type = typeof(InventoryPanel).Assembly.GetType("Nomad.Inventory.InventoryTooltip");
            object fixture = Activator.CreateInstance(type, parent, tooltip.Find("Title").GetComponent<UnityEngine.UI.Text>().font);
            foreach (Vector2 size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080) })
            {
                parent.sizeDelta = size;
                foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), Vector2.one })
                {
                    Vector2 point = corner * (size * 0.5f - Vector2.one * 2);
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, parent.TransformPoint(point));
                    type.GetMethod("Show").Invoke(fixture, new object[] { item, 2, screen, null });
                    var rect = (RectTransform)parent.Find("InventoryTooltip");
                    Vector3[] corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    foreach (Vector3 world in corners)
                    {
                        Vector2 local = parent.InverseTransformPoint(world);
                        Check(parent.rect.Contains(local), $"bounds {size} {corner}");
                    }
                }
            }

            UnityEngine.Object.Destroy(parent.gameObject);
        }

        private static void RenderPreview()
        {
            InvokeInventory("AddToSlotForTransfer", 0, item, 95);
            InvokeInventory("NotifyChanged");
            Canvas canvas = view.GetComponent<Canvas>();
            var cameraObject = new GameObject("TooltipRenderCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(31, 36, 39, 255);
            camera.cullingMask = 1 << 5;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = 5;

            object presenter = typeof(InventoryPanel).GetField("tooltip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1080) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                Vector2 pointer = RectTransformUtility.WorldToScreenPoint(camera, grid.GetChild(0).position);
                presenter.GetType().GetMethod("Show").Invoke(presenter, new object[] { item, 95, pointer, camera });
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts/tooltip");
                File.WriteAllBytes($"Artifacts/tooltip/tooltip-{size.x}.png", pixels.EncodeToPNG());
                RenderTexture.active = null;
                camera.targetTexture = null;
                UnityEngine.Object.Destroy(pixels);
                UnityEngine.Object.Destroy(target);
            }

            UnityEngine.Object.Destroy(cameraObject);
        }

        private static Vector2 Slot(int index) => RectTransformUtility.WorldToScreenPoint(null, grid.GetChild(index).position);

        private static void Pointer(Vector2 position, bool held = false)
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, held));
        }

        private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static void Check(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException(label);
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
