using System;
using System.IO;
using System.Linq;
using Nomad.Combat;
using Nomad.Interaction;
using Nomad.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nomad.Shop.Editor
{
    public static class ShopVerification
    {
        private const string SessionKey = "Nomad.Shop.Verify";
        private static double deadline;
        private static int frame;
        private static int stage;
        private static int transactionChecks;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static PlayerInventory inventory;
        private static PlayerWallet wallet;
        private static Shopkeeper shop;
        private static ShopPanel panel;
        private static Vector3 position;
        private static string unexpectedError;
        private static int expectedGold;
        private static int selectedQuantity;
        private static Vector2 backpackPosition;
        private static Vector2 stockPosition;
        private static Vector3 slotPosition;

        [MenuItem("Nomad/Shop/Verify Shop (batch only)")]
        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("이 검증은 종료 코드를 반환하므로 배치 모드에서 실행해 주세요.");

            ShopAuthoring.Build();
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false))
                return;

            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            // 새 검증 checkout의 검색 DB가 없을 때 나는 Editor 시작 오류는 게임 검증과 구분합니다.
            if (type == LogType.Exception && trace.Contains("UnityEditor.Search.SearchDatabase") &&
                !trace.Contains("Nomad."))
            {
                Debug.LogWarning("SHOP_VERIFY_ENVIRONMENT: Unity Search startup exception (outside gameplay). " + message);
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                unexpectedError = message;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "검증 시간 초과");
                Require(unexpectedError == null, "Unity 오류: " + unexpectedError);
                if (!Application.isPlaying || Time.frameCount <= frame)
                    return;
                frame = Time.frameCount;

                if (keyboard == null)
                {
                    inventory = Object.FindAnyObjectByType<PlayerInventory>();
                    if (inventory == null || inventory.UsedSlots == 0)
                        return;

                    wallet = inventory.GetComponent<PlayerWallet>();
                    shop = Object.FindAnyObjectByType<Shopkeeper>();
                    Require(wallet.Gold == 1000 && inventory.GetSlot(0).Count == 12 && shop.StockAt(0) == 30,
                        "데모 초기 재화·아이템·재고");
                    transactionChecks = ShopTransactionChecks.Run();
                    Require(shop.CanInteract(new InteractionContext(inventory.transform, inventory)), "상점 상호작용 가능");
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode =
                        InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    mouse = InputSystem.AddDevice<Mouse>();
                    mouse.MakeCurrent();
                    Queue(Key.E);
                    return;
                }

                switch (++stage)
                {
                    case 1:
                        panel = Object.FindAnyObjectByType<ShopPanel>();
                        Require(panel != null && Read("Gold").Contains("1,000") && !Popup.activeSelf && Node("Trade tray") == null,
                            "상점 열기는 목록만 표시·하단 거래 제거");
                        Canvas.ForceUpdateCanvases();
                        backpackPosition = Node("Backpack/Content").GetComponent<RectTransform>().anchoredPosition;
                        stockPosition = Node("Stock/Content").GetComponent<RectTransform>().anchoredPosition;
                        slotPosition = Node("Stock/Content/Slot 0").GetComponent<RectTransform>().anchoredPosition;
                        expectedGold = 1000;
                        RequireVisible("Backpack/Content/Slot 0/Count");
                        RequireVisible("Stock/Content/Slot 0/Name");
                        Capture("shop-list.png");
                        Pointer("Stock/Content/Slot 0", true, MouseButton.Right);
                        break;
                    case 2: Pointer("Stock/Content/Slot 0", false, MouseButton.Right); break;
                    case 3:
                        Require(!Popup.activeSelf && wallet.Gold == expectedGold, "목록 우클릭 무동작·거래 없음");
                        Pointer("Stock/Content/Slot 0", true);
                        break;
                    case 4: Pointer("Stock/Content/Slot 0", false); break;
                    case 5:
                        Require(Popup.activeSelf && wallet.Gold == expectedGold && Read("Item title") == "고철", "좌클릭 구매 팝업·자동 거래 없음");
                        Require(Quantity.text == "1" && Slider.wholeNumbers && Slider.minValue == 1 && Slider.maxValue == 30,
                            "정수 슬라이더·초기 수량·구매 한도");
                        Require(Node("Buy").gameObject.activeSelf && !Node("Sell").gameObject.activeSelf, "구매 버튼만 표시");
                        Require(Quantity.textComponent.font == Resources.Load<Font>("ShopFonts/Pretendard-Regular") &&
                            Node("Item title").GetComponent<Text>().font == Resources.Load<Font>("ShopFonts/Pretendard-SemiBold"), "한글 폰트");
                        Capture("shop-popup-buy.png");
                        Pointer("Quantity Slider", true);
                        break;
                    case 6: Pointer("Quantity Slider", false); break;
                    case 7:
                        selectedQuantity = int.Parse(Quantity.text);
                        Require(selectedQuantity > 1 && selectedQuantity < 30 && Slider.value == selectedQuantity &&
                            Read("Selection").Contains($"합계 {selectedQuantity * 5:N0} 골드"), "실제 슬라이더 클릭·수량·합계 동기화");
                        Queue(Key.Enter);
                        break;
                    case 8:
                        expectedGold -= selectedQuantity * 5;
                        Require(wallet.Gold == expectedGold && inventory.GetSlot(0).Count == 12 + selectedQuantity && Popup.activeSelf,
                            "Enter 지정 수량 구매·팝업 유지");
                        Queue(Key.Escape);
                        break;
                    case 9:
                        Require(!Popup.activeSelf && Node("Backpack/Content").GetComponent<RectTransform>().anchoredPosition == backpackPosition &&
                            Node("Stock/Content").GetComponent<RectTransform>().anchoredPosition == stockPosition &&
                            (Vector3)Node("Stock/Content/Slot 0").GetComponent<RectTransform>().anchoredPosition == slotPosition,
                            "팝업·거래 후 슬롯/스크롤 위치 고정");
                        Queue(Key.LeftShift);
                        Pointer("Backpack/Content/Slot 0", true);
                        break;
                    case 10: Pointer("Backpack/Content/Slot 0", false); break;
                    case 11:
                        Require(Popup.activeSelf && Quantity.text == inventory.GetSlot(0).Count.ToString() &&
                            Node("Sell").gameObject.activeSelf && !Node("Buy").gameObject.activeSelf && wallet.Gold == expectedGold,
                            "Shift 가방 클릭은 전량 판매 팝업·자동 판매 없음");
                        Capture("shop-popup-sell.png");
                        Queue();
                        Pointer("Cancel", true, MouseButton.Right);
                        break;
                    case 12: Pointer("Cancel", false, MouseButton.Right); break;
                    case 13:
                        Require(!Popup.activeSelf && wallet.Gold == expectedGold, "우클릭은 팝업만 닫기");
                        Pointer("Backpack/Content/Slot 1", true);
                        break;
                    case 14: Pointer("Backpack/Content/Slot 1", false); break;
                    case 15:
                        Require(Popup.activeSelf && Read("Item title") == "야전 나이프", "가방 클릭은 해당 아이템 판매");
                        Queue(Key.L);
                        break;
                    case 16:
                        Require(wallet.IsSaleProtected(inventory.GetSlot(1).Item) && !Node("Sell").GetComponent<Button>().interactable,
                            "판매 잠금 버튼 비활성");
                        Queue(Key.V);
                        break;
                    case 17:
                        Require(wallet.Gold == expectedGold && inventory.GetSlot(1).Count == 1, "판매 잠금 중 키 거래 차단");
                        Queue();
                        break;
                    case 18: Queue(Key.L); break;
                    case 19:
                        Require(!wallet.IsSaleProtected(inventory.GetSlot(1).Item) && Node("Sell").GetComponent<Button>().interactable,
                            "판매 잠금 해제");
                        Queue();
                        Pointer("Sell", true);
                        break;
                    case 20: Pointer("Sell", false); break;
                    case 21:
                        expectedGold += 60;
                        Require(wallet.Gold == expectedGold && inventory.GetSlot(1).Count == 0 && Quantity.text == "0" &&
                            Slider.maxValue == 0 && !Slider.interactable && !Node("Sell").GetComponent<Button>().interactable &&
                            Read("Item title") == "야전 나이프", "전량 판매 후 이미지 유지·수량/거래 비활성");
                        Capture("shop-popup-sold.png");
                        Queue(Key.Escape);
                        break;
                    case 22: Queue(); break;
                    case 23: Queue(Key.Tab); break;
                    case 24: Queue(Key.RightArrow); break;
                    case 25: Queue(Key.Enter); break;
                    case 26:
                        Require(Popup.activeSelf && Read("Item title") == "야전 나이프" && wallet.Gold == expectedGold,
                            "Tab·방향키 선택 후 Enter는 팝업만 열기");
                        Queue(Key.Q);
                        break;
                    case 27:
                        Require(Quantity.isFocused, "Q 수량 입력 포커스");
                        Quantity.text = "";
                        TypeQuantity("3x");
                        Queue(Key.Enter);
                        break;
                    case 28:
                        Require(wallet.Gold == expectedGold && Quantity.text == "3" && Slider.value == 3,
                            "직접 입력 동기화·확정 Enter 중복 거래 없음");
                        Queue();
                        break;
                    case 29: Queue(Key.Enter); break;
                    case 30:
                        expectedGold -= 360;
                        int knives = Enumerable.Range(0, PlayerInventory.Capacity).Sum(index =>
                            inventory.GetSlot(index).Item == shop.Catalog.GetOffer(1).Item ? inventory.GetSlot(index).Count : 0);
                        Require(wallet.Gold == expectedGold && knives == 3 && Popup.activeSelf, "직접 입력 3개 구매·장비는 슬롯별 보관");
                        Queue(Key.Escape);
                        break;
                    case 31:
                        Queue();
                        Node("Filter Equipment").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 32:
                        Require(!Popup.activeSelf && !Node("Stock/Content/Slot 0").gameObject.activeSelf &&
                            Node("Stock/Content/Slot 1").gameObject.activeSelf, "분류 변경은 목록만 갱신");
                        Queue(Key.Enter);
                        break;
                    case 33:
                        Require(Popup.activeSelf && Read("Item title") == "야전 나이프", "필터 내 팝업 열기");
                        Node("Maximum").GetComponent<Button>().onClick.Invoke();
                        Queue();
                        break;
                    case 34:
                        Require(int.Parse(Quantity.text) == shop.BuyLimit(inventory, 1), "최대 버튼 구매 한도");
                        typeof(PlayerWallet).GetProperty(nameof(PlayerWallet.Gold)).SetValue(wallet, 0);
                        wallet.TryAddGold(1);
                        break;
                    case 35:
                        Require(Quantity.text == "0" && Slider.maxValue == 0 && !Node("Buy").GetComponent<Button>().interactable &&
                            Read("Readiness").Contains("부족"), "재화 변경 시 한도 0·거래 비활성·사유");
                        wallet.TryAddGold(expectedGold - 1);
                        Queue(Key.Escape);
                        break;
                    case 36:
                        Require(!Popup.activeSelf && wallet.Gold == expectedGold, "Esc는 팝업 먼저 닫기");
                        Queue();
                        break;
                    case 37: Queue(Key.Escape); break;
                    case 38:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null, "목록에서 Esc 상점 닫기");
                        Queue();
                        break;
                    case 39: Queue(Key.E); break;
                    case 40:
                        panel = Object.FindAnyObjectByType<ShopPanel>();
                        Require(panel != null && !Popup.activeSelf, "재열기는 목록에서 시작");
                        position = inventory.transform.position;
                        inventory.transform.position += Vector3.right * 10;
                        Physics.SyncTransforms();
                        Queue();
                        break;
                    case 41:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null, "거리 이탈 닫기");
                        inventory.transform.position = position;
                        Physics.SyncTransforms();
                        Queue();
                        break;
                    case 42: Queue(Key.E); break;
                    case 43:
                        panel = Object.FindAnyObjectByType<ShopPanel>();
                        Require(panel != null, "복귀 후 열기");
                        inventory.GetComponent<Health>().TryApplyDamage(1000);
                        Queue();
                        break;
                    case 44:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null && !shop.TryBuy(inventory, 0, 1, out _), "사망 시 닫기·거래 차단");
                        break;
                    case 45:
                        if (!ShopRestockChecks.Step()) { stage--; break; }
                        Debug.Log($"SHOP_PLAYMODE_PASS transaction_checks={transactionChecks} ui_stages={stage}");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"SHOP_PLAYMODE_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static Transform Node(string path) => panel.transform.Find("Shop Window/" + path) ??
            panel.transform.Find("Shop Window/Trade Popup/Card/" + path);
        private static GameObject Popup => Node("Trade Popup").gameObject;
        private static Slider Slider => Node("Quantity Slider").GetComponent<Slider>();
        private static InputField Quantity => Node("Quantity").GetComponent<InputField>();
        private static string Read(string path) => Node(path).GetComponent<Text>().text;

        private static void RequireVisible(string path)
        {
            Text text = Node(path).GetComponent<Text>();
            Require(text.cachedTextGenerator.characterCountVisible >= text.text.Length, "슬롯 수량 글자 잘림 없음: " + path);
        }

        private static void TypeQuantity(string value)
        {
            // InputSystem 가상 키 상태는 IMGUI 문자 큐를 생성하지 않으므로 uGUI의 키 처리 API를 사용합니다.
            foreach (char character in value)
                Quantity.ProcessEvent(new Event { type = EventType.KeyDown, character = character });
        }

        private static void Pointer(string path, bool held, MouseButton button = MouseButton.Left)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)Node(path);
            ScrollRect scroll = rect.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                float top = -rect.anchoredPosition.y;
                float offset = scroll.content.anchoredPosition.y;
                if (top < offset || top + rect.rect.height > offset + scroll.viewport.rect.height)
                    scroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0,
                        top < offset ? top : top + rect.rect.height - scroll.viewport.rect.height));
                Canvas.ForceUpdateCanvases();
            }
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(button, held));
        }

        private static void Queue(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static void Capture(string filename) => CaptureView(filename, panel);

        internal static void CaptureView(string filename, ShopPanel view = null)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts"));
            Directory.CreateDirectory(directory);
            Camera camera = Camera.main;
            Canvas canvas = view != null ? view.GetComponent<Canvas>() : null;
            var target = new RenderTexture(1440, 900, 24);
            var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                // 배치 모드는 Game View를 그리지 않으므로 같은 카메라와 UI를 명시적으로 렌더합니다.
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1;
                }
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, filename), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                if (canvas != null)
                {
                    canvas.worldCamera = null;
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Require(bool condition, string label) => ShopTransactionChecks.Require(condition, label);

        private static void Finish(int exitCode)
        {
            ShopRestockChecks.Reset();
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            Application.logMessageReceived -= OnLog;
            EditorApplication.Exit(exitCode);
        }
    }
}
