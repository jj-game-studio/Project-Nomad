using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nomad.Shop.Editor
{
    // 실제 게임 시계와 프레임을 사용해 닫힌/비활성 상점과 일시정지 경계를 검사합니다.
    internal static class ShopRestockChecks
    {
        private static ShopTransactionChecks.Fixture fixture;
        private static int stage;
        private static int notifications;
        private static double started;
        private static double pausedRemaining;
        private static ShopPanel panel;

        internal static bool Step()
        {
            if (fixture == null)
            {
                fixture = new ShopTransactionChecks.Fixture(stock: 2, restockSeconds: 0.25f);
                panel = ShopPanel.Open(fixture.Shop, fixture.Inventory);
                Require(panel != null, "재입고 검사 거래창 열기");
                Require(fixture.Shop.TryBuy(fixture.Inventory, 0, 2, out _) && fixture.Shop.StockAt(0) == 0,
                    "재입고 전 품절");
                Require(Read("Stock/Content/Slot 0/Price") == "품절" && Read("Readiness") == "품절입니다.",
                    "품절 상품은 목록에 남고 거래 불가 안내");
                panel.Close();
                fixture.Shop.Changed += () => notifications++;
                fixture.Store.SetActive(false);
                started = Time.timeAsDouble;
                return false;
            }

            switch (stage)
            {
                case 0:
                    if (Time.timeAsDouble - started < 0.8)
                        return false;
                    Require(fixture.Shop.StockAt(0) == 0, "비활성 중 미갱신");
                    fixture.Store.SetActive(true);
                    Require(fixture.Shop.CanBuy(fixture.Inventory, 0, 1, out _) && fixture.Shop.StockAt(0) == 2 &&
                        notifications == 1 && fixture.Wallet.Gold == 990 && fixture.Inventory.GetSlot(0).Count == 2,
                        "여러 주기 경과 후 재접근 시 한 번 보충·가방/재화 보존");
                    double remaining = fixture.Shop.RestockRemaining;
                    panel = ShopPanel.Open(fixture.Shop, fixture.Inventory);
                    Require(panel != null && fixture.Shop.RestockRemaining <= remaining + 0.0001,
                        "거래창 재열기로 재입고 시각을 연장하지 않음");
                    Require(fixture.Shop.TrySell(fixture.Inventory, 0, 2, out _) && fixture.Shop.StockAt(0) == 4,
                        "플레이어 판매로 초기 재고 초과");
                    started = Time.timeAsDouble;
                    stage++;
                    break;
                case 1:
                    if (Time.timeAsDouble - started < 0.3)
                        return false;
                    Require(fixture.Shop.StockAt(0) == 4 && fixture.Wallet.Gold == 994 && fixture.Inventory.UsedSlots == 0,
                        "초기값을 넘는 재고는 재입고로 감소하지 않음");
                    Require(fixture.Shop.TryBuy(fixture.Inventory, 0, 4, out _), "일시정지 전 재품절");
                    pausedRemaining = fixture.Shop.RestockRemaining;
                    Time.timeScale = 0;
                    started = EditorApplication.timeSinceStartup;
                    stage++;
                    break;
                case 2:
                    if (EditorApplication.timeSinceStartup - started < 0.35)
                        return false;
                    Require(fixture.Shop.StockAt(0) == 0 && Math.Abs(pausedRemaining - fixture.Shop.RestockRemaining) < 0.0001,
                        "일시정지 중 재입고/타이머 정지");
                    Time.timeScale = 1;
                    panel = ShopPanel.Open(fixture.Shop, fixture.Inventory);
                    started = Time.timeAsDouble;
                    stage++;
                    break;
                case 3:
                    if (Time.timeAsDouble - started < 0.3)
                        return false;
                    Require(fixture.Shop.StockAt(0) == 2 && fixture.Wallet.Gold == 974 && fixture.Inventory.GetSlot(0).Count == 4,
                        "재개 후 부족분 보충·플레이어 소유 무변경");
                    Require(panel != null && Read("Stock/Content/Slot 0/Count") == "x2" &&
                        Read("Stock/Content/Slot 0/Price") == "구매 5 G", "열린 거래창에 재입고 수량·가격 즉시 갱신");
                    panel.Close();
                    fixture.Dispose();
                    fixture = new ShopTransactionChecks.Fixture(stock: 2, restockSeconds: 0);
                    Require(fixture.Shop.TryBuy(fixture.Inventory, 0, 2, out _), "자동 보충 없음 설정");
                    started = Time.timeAsDouble;
                    stage++;
                    break;
                case 4:
                    if (Time.timeAsDouble - started < 0.3)
                        return false;
                    Require(fixture.Shop.StockAt(0) == 0 && fixture.Shop.RestockRemaining == 0,
                        "주기 0이면 자동 재입고 없음");
                    Reset();
                    Debug.Log("SHOP_RESTOCK_PASS disabled_catchup=pass excess=pass pause=pass zero_interval=pass");
                    return true;
            }
            return false;
        }

        internal static void Reset()
        {
            Time.timeScale = 1;
            if (panel != null)
                panel.Close();
            panel = null;
            fixture?.Dispose();
            fixture = null;
            stage = notifications = 0;
        }

        private static void Require(bool condition, string label) => ShopTransactionChecks.Require(condition, label);
        private static string Read(string path) => panel.transform.Find("Shop Window/" + path).GetComponent<Text>().text;
    }
}
