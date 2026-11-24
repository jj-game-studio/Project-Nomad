using System;
using System.Linq;
using Nomad.Game;
using Nomad.Game.Modules;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nomad.Shop.Editor
{
    public static class VillageShopVerification
    {
        private const string SessionKey = "Nomad.Shop.VillageVerify";
        private static PlayerInventory inventory;
        private static Shopkeeper shop;
        private static Keyboard keyboard;
        private static ShopPanel panel;
        private static double deadline;
        private static int frame;
        private static int stage;
        private static int starterSlots;
        private static string tradeItemId;
        private static int tradeStart;
        private static Vector3 stoppedAt;
        private static string unexpectedError;

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("전용 checkout의 배치 모드에서 실행해 주세요.");

            var scene = EditorSceneManager.OpenScene(VillageShopAuthoring.ScenePath);
            Require(Object.FindObjectsByType<Shopkeeper>(FindObjectsSortMode.None).Length == 1, "저장된 마을 상점 하나");
            Require(GameObject.Find("Merchant NPC") != null && GameObject.Find("Field Exit Guide") != null,
                "NPC와 필드 출구 안내가 저장 씬에 존재");
            int roots = scene.rootCount;
            VillageShopAuthoring.ApplyToScene(scene);
            VillageShopAuthoring.ApplyToScene(scene);
            Require(scene.rootCount == roots && Object.FindObjectsByType<Shopkeeper>(FindObjectsSortMode.None).Length == 1 &&
                Object.FindObjectsByType<PlayerWallet>(FindObjectsSortMode.None).Length == 0, "재배치 멱등성과 별도 플레이어 없음");
            EditorSceneManager.OpenScene("Assets/Nomad/Game/Game.unity");

            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false))
                return;
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && trace.Contains("UnityEditor.Search.SearchDatabase") && !trace.Contains("Nomad."))
            {
                Debug.LogWarning("VILLAGE_SHOP_ENVIRONMENT: Unity Search startup exception.");
                return;
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                unexpectedError = message;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "마을 접근/거래 검증 시간 초과");
                Require(unexpectedError == null, "Unity 오류: " + unexpectedError);
                if (!Application.isPlaying || Time.frameCount <= frame)
                    return;
                frame = Time.frameCount;

                GameManager manager = GameManager.Instance;
                if (manager == null || manager.IsLoading || manager.Player == null || !manager.Player.activeInHierarchy)
                    return;
                if (inventory == null)
                {
                    if (manager.CurrentRegion.name != "Village")
                        return;
                    shop = Object.FindAnyObjectByType<Shopkeeper>();
                    if (shop == null)
                        return;
                    inventory = manager.Player.GetComponent<PlayerInventory>();
                    starterSlots = inventory.UsedSlots;
                    // 시작 장비에 같은 아이템(화살)이 있을 수 있으므로 시작 수량을 기준으로 증감만 검사합니다.
                    tradeItemId = shop.Catalog.GetOffer(0).Item.ItemId;
                    tradeStart = inventory.GetCount(tradeItemId);
                    Require(inventory.GetComponent<PlayerWallet>().Gold == 1000, "초기 지갑");
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    Queue();
                    Debug.Log($"VILLAGE_SHOP_ROUTE spawn={inventory.transform.position} shop={shop.transform.position}");
                }

                if (stage < 2)
                {
                    // 순간이동 없이 CharacterController 충돌을 거쳐 입구에서 시장으로 접근합니다.
                    Vector3 target = stage == 0 ? new Vector3(-4, 0, -10) : shop.transform.position;
                    Vector3 delta = target - inventory.transform.position;
                    delta.y = 0;
                    if (delta.magnitude > (stage == 0 ? 0.2f : 0.7f))
                    {
                        inventory.GetComponent<CharacterController>().Move(delta.normalized * Mathf.Min(0.15f, delta.magnitude));
                        return;
                    }
                    if (++stage < 2)
                        return;
                    Require(shop.CanAccess(inventory), "입구에서 도보 접근 후 실제 거리·가림 검사 통과");
                    ShopVerification.CaptureView("village-shop-location.png");
                    Queue(Key.E);
                    return;
                }

                PlayerWallet wallet = inventory.GetComponent<PlayerWallet>();
                switch (++stage)
                {
                    case 3:
                        panel = Object.FindAnyObjectByType<ShopPanel>();
                        Require(panel != null && panel.gameObject.scene == inventory.gameObject.scene &&
                            panel.transform.Find("Shop Window/Title").GetComponent<Text>().text == "마을 상점",
                            "Game에서 마을 E 상호작용으로 영속 플레이어의 한국어 UI 열기");
                        // 목록에서 B는 거래 팝업만 열고, 팝업 안의 B가 실제 구매합니다.
                        Queue(Key.B);
                        break;
                    case 4:
                        Require(panel.transform.Find("Shop Window/Trade Popup").gameObject.activeSelf && wallet.Gold == 1000,
                            "목록 B는 구매 팝업만 열기");
                        Queue();
                        break;
                    case 5: Queue(Key.B); break;
                    case 6:
                        Require(wallet.Gold == 995 && inventory.GetCount(tradeItemId) == tradeStart + 1 && shop.StockAt(0) == 29,
                            "마을 실제 가방으로 구매");
                        Queue(Key.Tab);
                        break;
                    case 7:
                        // 방금 구매한 칸을 팔도록 마지막 칸을 고릅니다.
                        int tradeSlot = Enumerable.Range(0, PlayerInventory.Capacity)
                            .Last(index => inventory.GetSlot(index).Item?.ItemId == tradeItemId);
                        panel.transform.Find($"Shop Window/Backpack/Content/Slot {tradeSlot}").GetComponent<Button>().onClick.Invoke();
                        Queue(Key.V);
                        break;
                    case 8:
                        Require(wallet.Gold == 997 && inventory.GetCount(tradeItemId) == tradeStart &&
                            inventory.UsedSlots == starterSlots && shop.StockAt(0) == 30,
                            "마을 실제 가방으로 판매");
                        stoppedAt = inventory.transform.position;
                        Queue(Key.W);
                        break;
                    case 9:
                        Require(Vector2.Distance(new Vector2(stoppedAt.x, stoppedAt.z),
                            new Vector2(inventory.transform.position.x, inventory.transform.position.z)) < 0.01f,
                            "상점 중 이동 차단");
                        ShopVerification.CaptureView("village-shop-ui.png", panel);
                        Queue(Key.Escape);
                        break;
                    case 10:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null &&
                            manager.GetComponentInChildren<PauseMenu>()?.IsOpen == false,
                            "Esc로 상점만 닫고 일시정지 메뉴는 열리지 않음");
                        Queue();
                        break;
                    case 11:
                        Require(shop.CanInteract(new InteractionContext(inventory.transform, inventory)), "닫기 후 거래 잠금 해제");
                        Queue(Key.E);
                        break;
                    case 12:
                        panel = Object.FindAnyObjectByType<ShopPanel>();
                        Require(panel != null && wallet.Gold == 997 && shop.StockAt(0) == 30, "재열기 시 재화·재고 유지");
                        Require(shop.TryBuy(inventory, 0, 1, out _), "지역 왕복 검증용 상품 구매");
                        shop.gameObject.SetActive(false);
                        Queue();
                        break;
                    case 13:
                        Require(Object.FindAnyObjectByType<ShopPanel>() == null, "상점 비활성화 시 UI 해제");
                        stoppedAt = inventory.transform.position;
                        Queue(Key.S);
                        break;
                    case 14:
                        Require(Vector2.Distance(new Vector2(stoppedAt.x, stoppedAt.z),
                            new Vector2(inventory.transform.position.x, inventory.transform.position.z)) > 0.0001f,
                            "상점 종료 후 실제 이동 입력 복구");
                        Require(manager.LoadRegion("Field"), "실제 Game에서 Field 로드 시작");
                        break;
                    case 15:
                        Require(manager.CurrentRegion.name == "Field" && manager.Player == inventory.gameObject &&
                            wallet.Gold == 992 && inventory.GetCount(tradeItemId) == tradeStart + 1 &&
                            Object.FindAnyObjectByType<Shopkeeper>() == null, "Field에서 상점 언로드와 가방·지갑 유지");
                        Require(manager.LoadRegion("Village"), "실제 Game에서 Village 복귀 시작");
                        break;
                    case 16:
                        Shopkeeper returnedShop = Object.FindAnyObjectByType<Shopkeeper>();
                        Require(manager.CurrentRegion.name == "Village" && manager.Player == inventory.gameObject &&
                            wallet.Gold == 992 && inventory.GetCount(tradeItemId) == tradeStart + 1 &&
                            returnedShop != null && returnedShop != shop && returnedShop.StockAt(0) == 30,
                            "복귀 후 같은 가방·지갑과 새 지역 상점 재고");
                        Debug.Log("VILLAGE_SHOP_PLAYMODE_PASS game=main route=walk buy=pass sell=pass return=pass");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"VILLAGE_SHOP_PLAYMODE_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static void Queue(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            Application.logMessageReceived -= OnLog;
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
