using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Inventory.Editor
{
    internal static class InventoryVerification
    {
        private const string SessionKey = "Nomad.Inventory.Verify";
        private static double deadline;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static PlayerInventory inventory;
        private static UnityEngine.UI.Text capacity;
        private static UnityEngine.UI.Text itemDetails;
        private static RectTransform grid;
        private static RectTransform panel;
        private static int frame;
        private static int stage;
        private static int changes;

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

            deadline = EditorApplication.timeSinceStartup + 45;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "검증 시간 초과");
                if (!Application.isPlaying || Time.frameCount <= frame)
                    return;

                frame = Time.frameCount;
                if (keyboard == null)
                {
                    inventory = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
                    if (inventory == null || inventory.UsedSlots == 0)
                        return;

                    Require(inventory.UsedSlots == 3, "초기 테스트 아이템");
                    panel = (RectTransform)GameObject.Find("InventoryCanvas").transform.Find("InventoryPanel");
                    capacity = panel.Find("Capacity").GetComponent<UnityEngine.UI.Text>();
                    itemDetails = panel.Find("ItemDetails").GetComponent<UnityEngine.UI.Text>();
                    grid = (RectTransform)panel.Find("Slots");
                    Require(!panel.gameObject.activeSelf, "초기 화면 닫힘");
                    inventory.Changed += () => changes++;

                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode =
                        InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    mouse = InputSystem.AddDevice<Mouse>();
                    mouse.MakeCurrent();
                    Queue(Key.Tab);
                    return;
                }

                switch (++stage)
                {
                    case 1:
                        Require(panel.gameObject.activeSelf && capacity.text == "3 / 20 slots", "Tab 화면 열기");
                        Require(SlotCount(0) == "×96" && SlotCount(1) == "", "중첩 수량만 표시");
                        for (int index = 0; index < 3; index++)
                        {
                            var icon = grid.GetChild(index).Find("Icon").GetComponent<UnityEngine.UI.Image>();
                            Require(icon.enabled && icon.sprite != null, $"데모 아이콘 {index}");
                        }

                        Canvas.ForceUpdateCanvases();
                        VerifyLayout();
                        MovePointerToSlot(0);
                        Queue();
                        break;
                    case 2:
                        Require(itemDetails.text == "Scrap  ×96", "호버 이름과 수량");
                        Require(SlotBorder(0) == (Color)new Color32(224, 182, 104, 255), "호버 테두리");
                        Queue(Key.Digit1);
                        break;
                    case 3:
                        Require(changes == 1 && inventory.UsedSlots == 4, "스택 추가 및 알림");
                        Require(SlotCount(0) == "×99" && SlotCount(3) == "×7", "기존 스택 우선과 UI 갱신");
                        Require(itemDetails.text == "Scrap  ×99", "커서를 움직이지 않아도 호버 수량 갱신");
                        Queue(Key.Digit2);
                        break;
                    case 4:
                        Require(changes == 2 && inventory.UsedSlots == 3 && inventory.GetStack(0).Count == 96,
                            "여러 스택 제거 및 빈 슬롯 반환");
                        MovePointerToSlot(3);
                        Queue();
                        break;
                    case 5:
                        Require(itemDetails.text == "" && SlotCount(3) == "", "빈 슬롯 수량과 호버 상세 숨김");
                        Require(SlotBorder(0) != (Color)new Color32(224, 182, 104, 255), "호버 해제 시 테두리 복원");
                        Queue(Key.Digit3);
                        break;
                    case 6:
                        Require(changes == 3 && inventory.UsedSlots == 20 && capacity.text == "20 / 20 slots", "20슬롯 채우기");
                        Require(inventory.Add("scrap", 10) == 3 && inventory.GetStack(0).Count == 99,
                            "가득 찬 상태에서 부분 추가");
                        Require(inventory.Add("scrap", 10) == 0 && inventory.Add("field-vest", 1) == 0, "추가 공간 없음");
                        Require(inventory.Add("unknown", 5) == 0 && inventory.Remove("scrap", -1) == 0, "유효하지 않은 요청");
                        Require(inventory.Remove("scrap", int.MaxValue) == 99 && inventory.UsedSlots == 19, "보유량까지만 제거");
                        Require(inventory.Remove("field-knife", int.MaxValue) == 18 && inventory.UsedSlots == 1, "무기 중첩 제한");
                        Require(inventory.Add("scrap", int.MaxValue) == 19 * 99 && inventory.UsedSlots == 20, "대량 요청과 용량 제한");
                        Require(changes == 7, "성공한 변경당 알림 한 번");
                        Queue(Key.Tab);
                        break;
                    case 7:
                        Require(!panel.gameObject.activeSelf, "Tab 화면 닫기");
                        Debug.Log($"INVENTORY_PLAYMODE_PASS: keyboard, 5x4 grid, icons, hover, stacks, partial add, removal, notifications, frame={frame}");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"INVENTORY_PLAYMODE_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static void VerifyLayout()
        {
            var first = (RectTransform)grid.GetChild(0);
            var fifth = (RectTransform)grid.GetChild(4);
            var sixth = (RectTransform)grid.GetChild(5);
            var last = (RectTransform)grid.GetChild(19);

            Require(grid.childCount == 20 && first.rect.width > 0 && first.rect.width == first.rect.height,
                "20개 정사각형 슬롯");
            Require(first.anchoredPosition.y == fifth.anchoredPosition.y &&
                first.anchoredPosition.x == sixth.anchoredPosition.x &&
                sixth.anchoredPosition.y < first.anchoredPosition.y &&
                last.anchoredPosition.x == fifth.anchoredPosition.x &&
                last.anchoredPosition.y < sixth.anchoredPosition.y, "5열 슬롯 배치");
        }

        private static string SlotCount(int index)
        {
            return grid.GetChild(index).Find("Count").GetComponent<UnityEngine.UI.Text>().text;
        }

        private static Color SlotBorder(int index)
        {
            return grid.GetChild(index).GetComponent<UnityEngine.UI.Outline>().effectColor;
        }

        private static void MovePointerToSlot(int index)
        {
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, grid.GetChild(index).position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        }

        private static void Queue(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        private static void Require(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException(label);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            EditorApplication.Exit(code);
        }
    }
}
