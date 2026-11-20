using System;
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
                if (!Application.isPlaying || frame == Time.frameCount)
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
                    inventory.RemoveFromSlotForTransfer(0, 96);
                    inventory.AddToSlotForTransfer(0, item, 96);
                    inventory.NotifyChanged();
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
                        Canvas.ForceUpdateCanvases();
                        Pointer(Slot(0));
                        Keys();
                        break;
                    case 2:
                        Check(Visible && Title.Contains(item.DisplayName), "hover name");
                        Check(Body.Contains("Recovered material") && Body.Contains("\n"), "multiline description");
                        Check(!tooltip.GetComponent<CanvasGroup>().blocksRaycasts, "does not intercept input");
                        foreach (var graphic in tooltip.GetComponentsInChildren<UnityEngine.UI.Text>())
                            Check(!graphic.raycastTarget && !graphic.supportRichText, "safe noninteractive text");
                        previousPosition = tooltip.position;
                        Pointer(Slot(0) + Vector2.right * 8);
                        break;
                    case 3:
                        Check(Visible && tooltip.position != previousPosition, "follows within same slot");
                        inventory.RemoveFromSlotForTransfer(0, 1);
                        inventory.NotifyChanged();
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
                        inventory.RemoveFromSlotForTransfer(19, 95);
                        inventory.NotifyChanged();
                        break;
                    case 15:
                        Check(!Visible, "removed item hidden without pointer move");
                        VerifyBounds();
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

        private static bool Visible => tooltip.gameObject.activeInHierarchy;
        private static string Title => tooltip.Find("Title").GetComponent<UnityEngine.UI.Text>().text;
        private static string Body => tooltip.Find("Description").GetComponent<UnityEngine.UI.Text>().text;

        private static void VerifyBounds()
        {
            var parent = new GameObject("TooltipBoundsFixture", typeof(RectTransform)).GetComponent<RectTransform>();
            parent.SetParent(tooltip.parent, false);
            parent.anchorMin = parent.anchorMax = Vector2.one * 0.5f;
            var fixture = new InventoryTooltip(parent, tooltip.Find("Title").GetComponent<UnityEngine.UI.Text>().font);
            foreach (Vector2 size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080) })
            {
                parent.sizeDelta = size;
                foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), Vector2.one })
                {
                    Vector2 point = corner * (size * 0.5f - Vector2.one * 2);
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, parent.TransformPoint(point));
                    fixture.Show(item, 2, screen, null);
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

        private static Vector2 Slot(int index) => RectTransformUtility.WorldToScreenPoint(null, grid.GetChild(index).position);

        private static void Pointer(Vector2 position, bool held = false)
        {
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
