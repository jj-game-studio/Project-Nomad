using System;
using System.IO;
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
                        Require(panel != null && Read("Gold").Contains("1,000"), "E로 열기·현재 재화 표시");
                        Require(Read("Restock").StartsWith("재입고까지 ") && Read("Balance") == "거래 후 잔액 995 골드",
                            "재입고 타이머·구매 후 예상 잔액");
                        Require(Read("Backpack/Content/Slot 0/Price") == "판매 2 G" &&
                            Read("Stock/Content/Slot 0/Price") == "구매 5 G", "양쪽 칸별 가격 표시");
                        Require(Node("Backpack/Content").childCount == 20 &&
                            Node("Stock/Content").childCount == 3, "가방 20칸·상점 목록 표시");
                        Canvas.ForceUpdateCanvases();
                        RectTransform lastSlot = (RectTransform)Node("Backpack/Content/Slot 19");
                        ScrollRect backpack = Node("Backpack").GetComponent<ScrollRect>();
                        Require(-lastSlot.anchoredPosition.y + lastSlot.rect.height + 8 <= backpack.viewport.rect.height,
                            "스크롤 없이 가방 20칸 전체 표시");
                        Capture("shop-ui.png");
                        RequireVisible("Backpack/Content/Slot 0/Count");
                        RequireVisible("Stock/Content/Slot 0/Count");
                        RequireVisible("Backpack/Content/Slot 0/Name");
                        RequireVisible("Stock/Content/Slot 0/Price");
                        Font font = Resources.Load<Font>("ShopFonts/NanumGothic-Regular");
                        Text title = Node("Title").GetComponent<Text>();
                        Require(font != null && font.HasCharacter('상') && font.HasCharacter('점') &&
                            title.font == font && title.text == "마을 상점" && Quantity.textComponent.font == font,
                            "제목·수량 글꼴과 한글 글리프 확인");
                        Require(Node("Stock/Content/Slot 0").GetComponent<ShopRoundedRect>().Radius >= 10 &&
                            Node("Quantity").GetComponent<ShopRoundedRect>() != null, "둥근 카드·입력 칸 연결");
                        Queue(Key.B);
                        break;
                    case 2:
                        Require(wallet.Gold == 995 && inventory.GetSlot(0).Count == 13 && shop.StockAt(0) == 29,
                            "B 구매");
                        Queue();
                        break;
                    case 3: Queue(Key.Tab); break;
                    case 4:
                        Require(Read("Selection").StartsWith("판매"), "Tab으로 가방 선택");
                        Queue(Key.V);
                        break;
                    case 5:
                        Require(wallet.Gold == 997 && inventory.GetSlot(0).Count == 12 && shop.StockAt(0) == 30,
                            "V 판매");
                        Queue();
                        Pointer("Stock/Content/Slot 2", false);
                        break;
                    case 6: Pointer("Stock/Content/Slot 2", true); break;
                    case 7: Pointer("Stock/Content/Slot 2", false); break;
                    case 8:
                        Require(Read("Selection").Contains("구매  야전 조끼"), "마우스로 상점 상품 선택");
                        Pointer("Buy", true);
                        break;
                    case 9: Pointer("Buy", false); break;
                    case 10:
                        Require(wallet.Gold == 797 && shop.StockAt(2) == 2, "구매 버튼 클릭");
                        Pointer("Backpack/Content/Slot 0", true);
                        break;
                    case 11: Pointer("Backpack/Content/Slot 0", false); break;
                    case 12: Pointer("Sell", true); break;
                    case 13: Pointer("Sell", false); break;
                    case 14:
                        Require(wallet.Gold == 799 && inventory.GetSlot(0).Count == 11, "판매 버튼 클릭");
                        Queue(Key.Equals);
                        break;
                    case 15:
                        Require(Quantity.text == "2", "키로 수량 증가");
                        Queue(Key.Enter);
                        break;
                    case 16:
                        Require(wallet.Gold == 803 && inventory.GetSlot(0).Count == 9, "Enter 수량 거래·중복 실행 방지");
                        position = inventory.transform.position;
                        Queue(Key.W, Key.Space, Key.LeftShift);
                        break;
                    case 17:
                        Vector3 drift = inventory.transform.position - position;
                        // 거래 중에도 중력에 의한 바닥 정착은 유지하므로 수평 이동·점프·구르기를 구분합니다.
                        Require(new Vector2(drift.x, drift.z).sqrMagnitude < 0.0001f && drift.y <= 0.01f &&
                            !inventory.GetComponent<Nomad.Player.PlayerMovement>().IsRolling,
                            "거래 중 이동·점프·구르기 차단: " + drift);
                        Queue(Key.E);
                        break;
                    case 18:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null, "E 닫기·같은 프레임 재열림 방지");
                        Queue();
                        break;
                    case 19: Queue(Key.E); break;
                    case 20:
                        panel = Object.FindAnyObjectByType<ShopPanel>();
                        Require(panel != null, "E 재열기");
                        Queue();
                        Pointer("Close", true);
                        break;
                    case 21: Pointer("Close", false); break;
                    case 22:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null, "닫기 버튼 클릭");
                        Queue(Key.E);
                        break;
                    case 23:
                        Require(Object.FindAnyObjectByType<ShopPanel>() != null, "재화 보존·재접속");
                        Queue(Key.Escape);
                        break;
                    case 24:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null && wallet.Gold == 803, "Esc 닫기·재화 보존");
                        Queue();
                        break;
                    case 25: Queue(Key.Tab); break;
                    case 26:
                        Require(inventory.GetComponent<Nomad.Player.PlayerMovement>().enabled, "플레이어 활성 유지");
                        Queue(Key.E);
                        break;
                    case 27:
                        panel = Object.FindAnyObjectByType<ShopPanel>();
                        Require(panel != null && !Object.FindAnyObjectByType<InventoryPanel>().transform.Find("InventoryPanel").gameObject.activeSelf,
                            "가방 창에서 상점 전환 시 가방 닫기");
                        Queue();
                        inventory.transform.position += Vector3.right * 5;
                        break;
                    case 28:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null, "거리 이탈 자동 닫기");
                        inventory.transform.position = position;
                        Physics.SyncTransforms();
                        break;
                    case 29: Queue(Key.E); break;
                    case 30:
                        panel = Object.FindAnyObjectByType<ShopPanel>();
                        Require(panel != null, "범위 복귀 상점 열기");
                        Queue(Key.LeftShift);
                        Pointer("Backpack/Content/Slot 0", true);
                        break;
                    case 31: Pointer("Backpack/Content/Slot 0", false); break;
                    case 32:
                        Require(Quantity.text == "9" && wallet.Gold == 803, "Shift+클릭 보유 칸 전량 선택·즉시 판매 없음");
                        Queue();
                        Pointer("Stock/Content/Slot 0", true);
                        break;
                    case 33: Pointer("Stock/Content/Slot 0", false); break;
                    case 34:
                        Require(Quantity.text == "1", "일반 클릭 수량 초기화");
                        Queue(Key.RightShift);
                        Pointer("Stock/Content/Slot 0", true);
                        break;
                    case 35: Pointer("Stock/Content/Slot 0", false); break;
                    case 36:
                        Require(Quantity.text == shop.StockAt(0).ToString() && wallet.Gold == 803,
                            "오른쪽 Shift+클릭 상점 재고 전량 선택·즉시 구매 없음");
                        Queue();
                        Pointer("Quantity", true);
                        break;
                    case 37: Pointer("Quantity", false); break;
                    case 38:
                        Require(Quantity.isFocused, "수량 입력 칸 포커스");
                        Quantity.ProcessEvent(Event.KeyboardEvent("^a"));
                        break;
                    case 39: Quantity.ProcessEvent(Event.KeyboardEvent("backspace")); break;
                    case 40:
                        Require(Quantity.text == "" && !Node("Buy").GetComponent<Button>().interactable,
                            "입력 중 빈 값 거래 차단");
                        Queue();
                        TypeQuantity("3");
                        break;
                    case 41:
                        Require(Quantity.text == "3", "InputField 키 이벤트로 수량 지정");
                        Queue(Key.B, Key.V, Key.E, Key.L);
                        break;
                    case 42:
                        Require(wallet.Gold == 803 && panel != null && !wallet.IsSaleProtected(inventory.GetSlot(0).Item),
                            "편집 중 거래·닫기·판매 잠금 단축키 차단");
                        Queue(Key.Enter);
                        break;
                    case 43:
                        Require(wallet.Gold == 803 && Quantity.text == "3" && !Quantity.isFocused,
                            "입력 Enter는 확정만 하고 거래하지 않음");
                        Queue();
                        Pointer("Buy", true);
                        break;
                    case 44: Pointer("Buy", false); break;
                    case 45:
                        Require(wallet.Gold == 788 && inventory.GetSlot(0).Count == 12,
                            "직접 입력한 수량 3개 구매");
                        Pointer("Quantity", true);
                        break;
                    case 46: Pointer("Quantity", false); break;
                    case 47: Quantity.ProcessEvent(Event.KeyboardEvent("^a")); break;
                    case 48:
                        Quantity.ProcessEvent(Event.KeyboardEvent("backspace"));
                        TypeQuantity("-a9999999999");
                        break;
                    case 49:
                        Require(Quantity.text == shop.StockAt(0).ToString(), "음수·문자 제외 및 초과 입력 재고로 제한");
                        Queue(Key.Enter);
                        break;
                    case 50:
                        Require(wallet.Gold == 788, "초과 수량 입력 확정 시 거래 없음");
                        Queue();
                        Pointer("Stock/Content/Slot 1", true);
                        break;
                    case 51: Pointer("Stock/Content/Slot 1", false); break;
                    case 52: Pointer("Maximum", true); break;
                    case 53: Pointer("Maximum", false); break;
                    case 54:
                        Require(Quantity.text == "5", "Max 버튼 전체 선택");
                        Pointer("Backpack/Content/Slot 0", true);
                        break;
                    case 55: Pointer("Backpack/Content/Slot 0", false); break;
                    case 56:
                        Quantity.text = "4";
                        Require(Read("Selection").Contains("합계 8 골드"), "판매 직접 입력과 총액 갱신");
                        Queue(Key.V);
                        break;
                    case 57:
                        Require(wallet.Gold == 796 && inventory.GetSlot(0).Count == 8, "직접 지정한 수량 4개 판매");
                        Queue(Key.LeftShift);
                        Pointer("Backpack/Content/Slot 19", true);
                        break;
                    case 58: Pointer("Backpack/Content/Slot 19", false); break;
                    case 59:
                        Require(Quantity.text == "0" && !Quantity.interactable &&
                            !Node("Sell").GetComponent<Button>().interactable, "빈 칸 전체 선택 시 거래 불가");
                        Queue();
                        Pointer("Stock/Content/Slot 0", true);
                        break;
                    case 60: Pointer("Stock/Content/Slot 0", false); break;
                    case 61:
                        Capture("shop-ui-after-trade.png");
                        Node("Filter Equipment").GetComponent<Button>().onClick.Invoke();
                        Require(!Node("Stock/Content/Slot 0").gameObject.activeSelf &&
                            Node("Stock/Content/Slot 1").gameObject.activeSelf && Read("Description").Contains("장비"),
                            "상품 분류 필터·설명 표시");
                        Queue(Key.RightArrow);
                        break;
                    case 62:
                        Require(Read("Selection").Contains("야전 조끼"), "필터 안에서 키보드 선택 이동");
                        Node("Filter Supplies").GetComponent<Button>().onClick.Invoke();
                        Require(Read("Readiness") == "이 분류에는 상품이 없습니다." &&
                            !Node("Buy").GetComponent<Button>().interactable, "빈 분류 거래 차단·안내");
                        Queue(Key.B);
                        break;
                    case 63:
                        Require(wallet.Gold == 796, "빈 분류에서 B 거래 차단");
                        Node("Filter All").GetComponent<Button>().onClick.Invoke();
                        Node("Affordable").GetComponent<Button>().onClick.Invoke();
                        Require(Quantity.text == shop.BuyLimit(inventory, 0).ToString() &&
                            Node("Buy").GetComponent<Button>().interactable && wallet.Gold == 796,
                            "구매 한도 선택은 거래 없이 유효 수량 설정");
                        Capture("shop-ui-improved.png");
                        Queue();
                        Pointer("Backpack/Content/Slot 0", true);
                        break;
                    case 64: Pointer("Backpack/Content/Slot 0", false); break;
                    case 65: Queue(Key.L); break;
                    case 66:
                        Require(wallet.IsSaleProtected(inventory.GetSlot(0).Item) &&
                            Read("Backpack/Content/Slot 0/Keep") == "잠금" &&
                            !Node("Sell").GetComponent<Button>().interactable && Read("Readiness").Contains("판매가 잠겨 있습니다"),
                            "L 판매 잠금·스택 표시·판매 버튼/사유 일치");
                        Capture("shop-ui-kept.png");
                        Queue(Key.V);
                        break;
                    case 67:
                        Require(wallet.Gold == 796 && inventory.GetSlot(0).Count == 8 &&
                            !shop.TrySell(inventory, 0, 1, out _), "보관 잠금 중 키·직접 판매 모두 무변경");
                        panel.Close();
                        Queue();
                        break;
                    case 68: Queue(Key.E); break;
                    case 69:
                        panel = Object.FindAnyObjectByType<ShopPanel>();
                        Require(panel != null && Read("Backpack/Content/Slot 0/Keep") == "잠금", "재열기 후 판매 잠금 유지");
                        Queue();
                        Pointer("Backpack/Content/Slot 0", true);
                        break;
                    case 70: Pointer("Backpack/Content/Slot 0", false); break;
                    case 71:
                        Node("Protection").GetComponent<Button>().onClick.Invoke();
                        break;
                    case 72:
                        Require(!wallet.IsSaleProtected(inventory.GetSlot(0).Item) && Node("Sell").GetComponent<Button>().interactable &&
                            Read("Balance") == "거래 후 잔액 798 골드", "버튼 잠금 해제·판매 후 예상 잔액");
                        inventory.GetComponent<Health>().TryApplyDamage(1000);
                        Queue();
                        break;
                    case 73:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null &&
                            !shop.TryBuy(inventory, 0, 1, out _), "사망 시 닫기·거래 금지");
                        Require(File.Exists(Path.Combine(Application.dataPath, "../Artifacts/shop-ui.png")), "화면 캡처 생성");
                        break;
                    case 74:
                        if (!ShopRestockChecks.Step())
                        {
                            stage--;
                            break;
                        }
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

        private static Transform Node(string path) => panel.transform.Find("Shop Window/" + path);
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

        private static void Pointer(string path, bool held)
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
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, held));
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
