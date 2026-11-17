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

            EditorSceneManager.OpenScene(ShopAuthoring.ScenePath);
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
                        Require(Read("Backpack/Content/Slot 0/Price") == "Sell 2 G" &&
                            Read("Stock/Content/Slot 0/Price") == "Buy 5 G", "양쪽 칸별 가격 표시");
                        Require(Node("Backpack/Content").childCount == 20 &&
                            Node("Stock/Content").childCount == 3, "가방 20칸·상점 목록 표시");
                        Capture("shop-ui.png");
                        Queue(Key.B);
                        break;
                    case 2:
                        Require(wallet.Gold == 995 && inventory.GetSlot(0).Count == 13 && shop.StockAt(0) == 29,
                            "B 구매");
                        Queue();
                        break;
                    case 3: Queue(Key.Tab); break;
                    case 4:
                        Require(Read("Selection").StartsWith("SELL"), "Tab으로 가방 선택");
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
                        Require(Read("Selection").Contains("BUY  Field Vest"), "마우스로 상점 상품 선택");
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
                        Require(Read("Quantity") == "Qty 2", "키로 수량 증가");
                        Queue(Key.Enter);
                        break;
                    case 16:
                        Require(wallet.Gold == 803 && inventory.GetSlot(0).Count == 9, "Enter 수량 거래·중복 실행 방지");
                        position = inventory.transform.position;
                        Queue(Key.W, Key.Space, Key.LeftShift);
                        break;
                    case 17:
                        Require(Vector3.Distance(position, inventory.transform.position) < 0.01f, "거래 중 이동·점프·구르기 차단");
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
                        Require(Object.FindAnyObjectByType<ShopPanel>() != null, "범위 복귀 상점 열기");
                        inventory.GetComponent<Health>().TryApplyDamage(1000);
                        Queue();
                        break;
                    case 31:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null &&
                            !shop.TryBuy(inventory, 0, 1, out _), "사망 시 닫기·거래 금지");
                        Require(File.Exists(Path.Combine(Application.dataPath, "../Artifacts/shop-ui.png")), "화면 캡처 생성");
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
        private static string Read(string path) => Node(path).GetComponent<Text>().text;

        private static void Pointer(string path, bool held)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)Node(path);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, held));
        }

        private static void Queue(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static void Capture(string filename)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts"));
            Directory.CreateDirectory(directory);
            Camera camera = Camera.main;
            Canvas canvas = panel.GetComponent<Canvas>();
            var target = new RenderTexture(1440, 900, 24);
            var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                // 배치 모드는 Game View를 그리지 않으므로 같은 카메라와 UI를 명시적으로 렌더합니다.
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
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
                canvas.worldCamera = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Require(bool condition, string label) => ShopTransactionChecks.Require(condition, label);

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            Application.logMessageReceived -= OnLog;
            EditorApplication.Exit(exitCode);
        }
    }
}
