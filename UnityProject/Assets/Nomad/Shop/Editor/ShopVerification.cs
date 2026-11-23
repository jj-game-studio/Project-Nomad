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
        private static double notificationDeadline;
        private static int remainingScrap;

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
                        Require(Node("Paper").GetComponent<ShopRoundedRect>().color.grayscale -
                            Node("Capacity").GetComponent<Text>().color.grayscale > 0.5f &&
                            Node("Restock").GetComponent<Text>().color.grayscale -
                            panel.transform.Find("Shop Window").GetComponent<ShopRoundedRect>().color.grayscale > 0.5f &&
                            Node("Restock").GetComponent<Text>().fontSize >= 14,
                            "가방 칸 수·재입고 보조 정보 대비와 크기");
                        Require(Node("Backpack hint") == null && Node("Keys") == null &&
                            Read("Stock/Content/Slot 0/Price") == "5 G" &&
                            !Node("Backpack/Content/Slot 0/Price").gameObject.activeSelf,
                            "기본 화면 안내/반복 가격 제거·판매가는 팝업으로 이동");
                        Require(Node("Backpack").GetComponent<RectTransform>().rect.height == 392 &&
                            Node("Stock").GetComponent<RectTransform>().rect.height == 212 &&
                            Node("Backpack/Content/Slot 0/Icon").GetComponent<RectTransform>().rect.width >= 44,
                            "목록 여백 축소·상품 수에 맞는 높이·가방 아이콘 확대");
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
                        Require(Popup.activeSelf && wallet.Gold == expectedGold && Read("Item title") == "화살", "좌클릭 구매 팝업·자동 거래 없음");
                        Require(Quantity.text == "1" && Slider.wholeNumbers && Slider.minValue == 1 && Slider.maxValue == 30,
                            "정수 슬라이더·초기 수량·구매 한도");
                        Require(Node("Buy").gameObject.activeSelf && !Node("Sell").gameObject.activeSelf, "구매 버튼만 표시");
                        Require(Quantity.textComponent.font == Resources.Load<Font>("ShopFonts/Pretendard-Regular") &&
                            Node("Item title").GetComponent<Text>().font == Resources.Load<Font>("ShopFonts/Jua-Regular"), "한글 폰트");
                        Capture("shop-popup-buy.png");
                        Pointer("Quantity Slider", true);
                        break;
                    case 6: Pointer("Quantity Slider", false); break;
                    case 7:
                        selectedQuantity = int.Parse(Quantity.text);
                        Require(selectedQuantity > 1 && selectedQuantity < 30 && Slider.value == selectedQuantity &&
                            Read("Selection") == $"{selectedQuantity * 5:N0} G", "실제 슬라이더 클릭·수량·합계 동기화");
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
                        Require(Popup.activeSelf && Read("Item title") == "단검", "가방 클릭은 해당 아이템 판매");
                        Queue(Key.L);
                        break;
                    case 16:
                        Require(wallet.IsSaleProtected(inventory.GetSlot(1).Item) && !Node("Sell").GetComponent<Button>().interactable,
                            "판매 잠금 버튼 비활성");
                        Queue(Key.V);
                        break;
                    case 17:
                        Require(wallet.Gold == expectedGold && inventory.GetSlot(1).Count == 1 &&
                            !Node("Sale notification").gameObject.activeSelf, "판매 잠금 중 키 거래 차단·완료 알림 없음");
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
                        Require(wallet.Gold == expectedGold && inventory.GetSlot(1).Count == 0 && !Popup.activeSelf &&
                            Node("Sale notification").gameObject.activeSelf &&
                            Read("Sale notification/Message") == "단검 판매 +60 G" &&
                            !Node("Protection").gameObject.activeSelf, "전량 판매 후 팝업 닫기·실제 금액 알림·잠금 숨김");
                        Capture("shop-sale-complete.png");
                        Queue();
                        break;
                    case 22: Queue(); break;
                    case 23: Queue(Key.Tab); break;
                    case 24: Queue(Key.RightArrow); break;
                    case 25: Queue(Key.Enter); break;
                    case 26:
                        Require(Popup.activeSelf && Read("Item title") == "단검" && wallet.Gold == expectedGold,
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
                        Require(Popup.activeSelf && Read("Item title") == "단검", "필터 내 팝업 열기");
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
                        Queue(Key.F1);
                        break;
                    case 44:
                        Require(Node("Shop Help").gameObject.activeSelf && !Popup.activeSelf, "F1 기본 화면 도움말 열기");
                        Queue(Key.Escape);
                        break;
                    case 45:
                        Require(!Node("Shop Help").gameObject.activeSelf && panel != null, "Esc는 도움말만 닫기");
                        Queue();
                        Pointer("Stock/Content/Slot 0", true);
                        break;
                    case 46: Pointer("Stock/Content/Slot 0", false); break;
                    case 47:
                        Require(Popup.activeSelf && Read("Buy/Label") == "구매" && Read("Cancel/Label") == "취소" &&
                            Read("Unit price") == "개당 5 G" && Read("Total caption") == "총 구매 금액",
                            "팝업 정보 분리·버튼에서 단축키 표기 제거");
                        Capture("shop-popup-buy.png");
                        Queue(Key.F1);
                        break;
                    case 48:
                        Require(Node("Shop Help").gameObject.activeSelf && Popup.activeSelf, "팝업 위 F1 도움말 열기");
                        Queue(Key.Enter);
                        break;
                    case 49:
                        Require(wallet.Gold == expectedGold, "도움말 뒤 거래 입력 차단");
                        Queue(Key.Escape);
                        break;
                    case 50:
                        Require(!Node("Shop Help").gameObject.activeSelf && Popup.activeSelf, "도움말 닫기 후 원래 팝업 유지");
                        Queue(Key.Tab);
                        break;
                    case 51:
                        Require(new SerializedObject(Node("Buy").GetComponent<ShopRoundedRect>()).FindProperty("strokeWidth").floatValue == 3,
                            "Tab 구매 버튼 포커스 강조");
                        Queue();
                        break;
                    case 52: Queue(Key.Tab); break;
                    case 53:
                        Require(new SerializedObject(Node("Cancel").GetComponent<ShopRoundedRect>()).FindProperty("strokeWidth").floatValue == 3,
                            "Tab 다음 활성 버튼으로 이동");
                        Capture("shop-popup-focus.png");
                        Queue(Key.Enter);
                        break;
                    case 54:
                        Require(!Popup.activeSelf && wallet.Gold == expectedGold, "Enter는 포커스된 취소 실행·구매하지 않음");
                        Node("Help").GetComponent<Button>().onClick.Invoke();
                        Capture("shop-help.png");
                        Queue();
                        break;
                    case 55:
                        Require(Node("Shop Help").gameObject.activeSelf, "물음표 버튼 도움말 열기");
                        Node("Shop Help/Card/Close").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 56:
                        Require(!Node("Shop Help").gameObject.activeSelf, "도움말 닫기 버튼");
                        remainingScrap = inventory.GetSlot(0).Count;
                        Pointer("Backpack/Content/Slot 0", true);
                        break;
                    case 57: Pointer("Backpack/Content/Slot 0", false); break;
                    case 58:
                        Require(Popup.activeSelf, "일부 판매 검사 팝업 열기");
                        Quantity.text = "2";
                        Node("Sell").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 59:
                        expectedGold += 4;
                        Require(Popup.activeSelf && inventory.GetSlot(0).Count == remainingScrap - 2 &&
                            wallet.Gold == expectedGold && Read("Sale notification/Message") == "화살 2개 판매 +4 G" &&
                            Read("Status") == "" && Node("Protection").gameObject.activeSelf,
                            "일부 판매는 팝업 유지·실제 금액 알림·팝업 완료 문구 제거");
                        Capture("shop-sale-partial.png");
                        notificationDeadline = EditorApplication.timeSinceStartup + 3.2;
                        Queue();
                        break;
                    case 60:
                        if (EditorApplication.timeSinceStartup < notificationDeadline) { stage--; break; }
                        Require(!Node("Sale notification").gameObject.activeSelf, "판매 알림은 3초 후 숨김");
                        Node("Cancel").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 61:
                        inventory.GetComponent<Health>().TryApplyDamage(1000);
                        Queue();
                        break;
                    case 62:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null && !shop.TryBuy(inventory, 0, 1, out _), "사망 시 닫기·거래 차단");
                        break;
                    case 63:
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
