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
        private static PlayerInventory inventory;
        private static UnityEngine.UI.Text capacity;
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
                    Require(!panel.gameObject.activeSelf, "초기 화면 닫힘");
                    inventory.Changed += () => changes++;

                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode =
                        InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    Queue(Key.Tab);
                    return;
                }

                switch (++stage)
                {
                    case 1:
                        Require(panel.gameObject.activeSelf && capacity.text == "3 / 20 slots", "Tab 화면 열기");
                        Require(SlotText(0).Contains("Scrap   ×96"), "초기 수량과 표시 이름");
                        Queue(Key.Digit1);
                        break;
                    case 2:
                        Require(changes == 1 && inventory.UsedSlots == 4, "스택 추가 및 알림");
                        Require(SlotText(0).Contains("×99") && SlotText(3).Contains("×7"), "기존 스택 우선과 UI 갱신");
                        Queue(Key.Digit2);
                        break;
                    case 3:
                        Require(changes == 2 && inventory.UsedSlots == 3 && inventory.GetStack(0).Count == 96,
                            "여러 스택 제거 및 빈 슬롯 반환");
                        Queue(Key.Digit3);
                        break;
                    case 4:
                        Require(changes == 3 && inventory.UsedSlots == 20 && capacity.text == "20 / 20 slots", "20슬롯 채우기");
                        Require(inventory.Add("scrap", 10) == 3 && inventory.GetStack(0).Count == 99,
                            "가득 찬 상태에서 부분 추가");
                        Require(inventory.Add("scrap", 10) == 0 && inventory.Add("field-vest", 1) == 0, "추가 공간 없음");
                        Require(inventory.Add("unknown", 5) == 0 && inventory.Remove("scrap", -1) == 0, "유효하지 않은 요청");
                        Require(inventory.Remove("scrap", int.MaxValue) == 99 && inventory.UsedSlots == 19, "보유량까지만 제거");
                        Require(inventory.Remove("field-knife", int.MaxValue) == 18 && inventory.UsedSlots == 1, "무기 중첩 제한");
                        Require(inventory.Add("scrap", int.MaxValue) == 19 * 99 && inventory.UsedSlots == 20, "대량 요청과 용량 제한");
                        Require(changes == 7, "성공한 변경당 알림 한 번");
                        Canvas.ForceUpdateCanvases();
                        var grid = (RectTransform)panel.Find("Slots");
                        var last = (RectTransform)grid.GetChild(19);
                        Require(grid.childCount == 20 && last.rect.width > 0 && last.rect.height > 0, "슬롯 크기와 개수");
                        Queue(Key.Tab);
                        break;
                    case 5:
                        Require(!panel.gameObject.activeSelf, "Tab 화면 닫기");
                        Debug.Log($"INVENTORY_PLAYMODE_PASS: keyboard, UI, stacks, partial add, removal, notifications, frame={frame}");
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

        private static string SlotText(int index)
        {
            return panel.Find("Slots").GetChild(index).GetComponentInChildren<UnityEngine.UI.Text>().text;
        }

        private static void Queue(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
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
