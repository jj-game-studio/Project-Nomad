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
        private static int changesBeforeDrag;

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
                    var inventoryView = UnityEngine.Object.FindAnyObjectByType<InventoryPanel>();
                    Require(inventoryView != null, "가방 프리팹 연결");
                    panel = (RectTransform)inventoryView.transform.Find("InventoryPanel");
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
                        Require(inventory.Add("scrap", 10) == 0 && inventory.Add("gear-padded-armor", 1) == 0, "추가 공간 없음");
                        Require(inventory.Add("unknown", 5) == 0 && inventory.Remove("scrap", -1) == 0, "유효하지 않은 요청");
                        Require(inventory.Remove("scrap", int.MaxValue) == 99 && inventory.UsedSlots == 19, "보유량까지만 제거");
                        Require(inventory.Remove("weapon-dagger", int.MaxValue) == 18 && inventory.UsedSlots == 1, "무기 중첩 제한");
                        Require(inventory.Add("scrap", int.MaxValue) == 19 * 99 && inventory.UsedSlots == 20, "대량 요청과 용량 제한");
                        Require(changes == 7, "성공한 변경당 알림 한 번");
                        inventory.Remove("scrap", int.MaxValue);
                        inventory.Remove("gear-padded-armor", int.MaxValue);
                        inventory.Add("scrap", 106);
                        inventory.Add("weapon-dagger", 1);
                        changesBeforeDrag = changes;
                        MovePointerToSlot(2, held: true);
                        break;
                    case 7:
                        MovePointerToSlot(19, held: true);
                        break;
                    case 8:
                        Require(panel.Find("DragPreview").gameObject.activeSelf, "드래그 미리보기 표시");
                        Require(SlotBorder(19) == (Color)new Color32(224, 182, 104, 255), "빈 드롭 대상 강조");
                        Require(inventory.GetSlot(2).Item.ItemId == "weapon-dagger" && changes == changesBeforeDrag,
                            "놓기 전 인벤토리 보존");
                        MovePointerToSlot(19);
                        break;
                    case 9:
                        Require(inventory.GetSlot(2).Item == null && inventory.GetSlot(19).Item.ItemId == "weapon-dagger" &&
                            inventory.GetStack(2).Item.ItemId == "weapon-dagger" && inventory.UsedSlots == 3,
                            "먼 빈 칸으로 이동하고 기존 스택 순회 유지");
                        Require(changes == changesBeforeDrag + 1 && !panel.Find("DragPreview").gameObject.activeSelf,
                            "성공 후 한 번 알림 및 미리보기 정리");
                        MovePointerToSlot(19, held: true);
                        break;
                    case 10:
                        MovePointerToSlot(0, held: true);
                        break;
                    case 11:
                        MovePointerToSlot(0);
                        break;
                    case 12:
                        Require(inventory.GetSlot(19).Count == 99 && inventory.GetSlot(0).Item.ItemId == "weapon-dagger",
                            "다른 아이템과 교환");
                        MovePointerToSlot(19, held: true);
                        break;
                    case 13:
                        MovePointerToSlot(1, held: true);
                        break;
                    case 14:
                        MovePointerToSlot(1);
                        break;
                    case 15:
                        Require(inventory.GetSlot(1).Count == 99 && inventory.GetSlot(19).Count == 7 &&
                            inventory.UsedSlots == 3, "최대 수량까지만 합치고 나머지 보존");
                        MovePointerToSlot(19, held: true);
                        break;
                    case 16:
                        QueuePointer(new Vector2(-20, -20), held: true);
                        break;
                    case 17:
                        QueuePointer(new Vector2(-20, -20), held: false);
                        break;
                    case 18:
                        Require(inventory.GetSlot(19).Count == 7 && changes == changesBeforeDrag + 3 &&
                            !panel.Find("DragPreview").gameObject.activeSelf, "가방 밖에 놓으면 원래 칸으로 복귀");
                        inventory.Remove("scrap", 99);
                        inventory.Add("scrap", 99);
                        Require(inventory.GetSlot(1).Count == 7 && inventory.GetStack(2).Count == 7 &&
                            !inventory.MoveStack(1, 1) && !inventory.MoveStack(1, -1) && !inventory.MoveStack(1, 19),
                            "빈 칸 재사용 및 잘못된 이동·가득 찬 스택 무시");
                        Require(inventory.MoveStack(19, 1), "합칠 수 있는 스택으로 이동");
                        inventory.Remove("scrap", 8);
                        Require(inventory.MoveStack(19, 1) && inventory.GetSlot(19).Item == null &&
                            inventory.GetSlot(1).Count == 98 && inventory.UsedSlots == 2,
                            "전량 합치면 출발 슬롯 반환");
                        inventory.MoveStack(0, 19);
                        MovePointerToSlot(19, held: true);
                        break;
                    case 19:
                        MovePointerToSlot(0, held: true);
                        break;
                    case 20:
                        Require(panel.Find("DragPreview").gameObject.activeSelf, "닫기 직전 드래그 중");
                        Queue(Key.Tab);
                        break;
                    case 21:
                        Require(!panel.gameObject.activeSelf, "Tab 화면 닫기");
                        Require(inventory.GetSlot(19).Item.ItemId == "weapon-dagger" &&
                            !panel.Find("DragPreview").gameObject.activeSelf, "가방 닫으면 드래그 취소");
                        Debug.Log($"INVENTORY_PLAYMODE_PASS: keyboard, 5x4 grid, icons, hover, drag/drop, stacks, partial add, removal, notifications, frame={frame}");
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

        private static void MovePointerToSlot(int index, bool held = false)
        {
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, grid.GetChild(index).position);
            QueuePointer(position, held);
        }

        private static void QueuePointer(Vector2 position, bool held)
        {
            var state = new MouseState { position = position }.WithButton(MouseButton.Left, held);
            InputSystem.QueueStateEvent(mouse, state);
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
