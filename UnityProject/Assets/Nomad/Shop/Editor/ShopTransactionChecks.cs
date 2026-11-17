using System;
using Nomad.Inventory;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Shop.Editor
{
    internal static class ShopTransactionChecks
    {
        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Player;
            internal readonly PlayerInventory Inventory;
            internal readonly PlayerWallet Wallet;
            internal readonly GameObject Store;
            internal readonly Shopkeeper Shop;
            private readonly ShopCatalog catalog;

            internal Fixture(int gold = 1000, int stock = 30, int buy = 5, int sell = 2)
            {
                ItemDefinition scrap = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Nomad/Inventory/Demo/scrap.asset");
                Player = new GameObject("Trade fixture");
                Player.SetActive(false);
                Player.transform.position = new Vector3(1000, 0, 1000);
                Inventory = Player.AddComponent<PlayerInventory>();
                var items = new SerializedObject(Inventory);
                items.FindProperty("items").arraySize = 1;
                items.FindProperty("items").GetArrayElementAtIndex(0).objectReferenceValue = scrap;
                items.ApplyModifiedPropertiesWithoutUndo();
                Wallet = Player.AddComponent<PlayerWallet>();
                SetInt(Wallet, "startingGold", gold);

                catalog = ScriptableObject.CreateInstance<ShopCatalog>();
                var data = new SerializedObject(catalog);
                data.FindProperty("offers").arraySize = 1;
                SerializedProperty entry = data.FindProperty("offers").GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("item").objectReferenceValue = scrap;
                entry.FindPropertyRelative("buyPrice").intValue = buy;
                entry.FindPropertyRelative("sellPrice").intValue = sell;
                entry.FindPropertyRelative("initialStock").intValue = stock;
                data.ApplyModifiedPropertiesWithoutUndo();
                Store = new GameObject("Store fixture");
                Store.SetActive(false);
                Store.transform.position = Player.transform.position;
                Shop = Store.AddComponent<Shopkeeper>();
                ShopAuthoring.SetReference(Shop, "catalog", catalog);
                Player.SetActive(true);
                Store.SetActive(true);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Store);
                Object.DestroyImmediate(Player);
                Object.DestroyImmediate(catalog);
            }
        }

        internal static int Run()
        {
            int checks = 0;
            using (var f = new Fixture())
            {
                int inventoryEvents = 0, walletEvents = 0, stockEvents = 0;
                bool nestedAccepted = false;
                f.Inventory.Changed += () =>
                {
                    inventoryEvents++;
                    Require(f.Wallet.Gold == 985 && f.Shop.StockAt(0) == 27 && f.Inventory.GetSlot(0).Count == 3,
                        "알림 시 돈·재고·가방이 모두 반영됨");
                    nestedAccepted = f.Shop.TryBuy(f.Inventory, 0, 1, out _);
                };
                f.Wallet.Changed += () => walletEvents++;
                f.Shop.Changed += () => stockEvents++;
                Require(f.Shop.TryBuy(f.Inventory, 0, 3, out _) && !nestedAccepted && inventoryEvents == 1 &&
                    walletEvents == 1 && stockEvents == 1, "구매·단일 알림·재진입 차단");
                checks++;
            }

            using (var f = new Fixture())
            {
                f.Inventory.Add("scrap", 98);
                Require(f.Shop.TryBuy(f.Inventory, 0, 3, out _) && f.Inventory.GetSlot(0).Count == 99 &&
                    f.Inventory.GetSlot(1).Count == 2 && f.Inventory.UsedSlots == 2, "기존 중첩 우선 구매");
                Require(f.Shop.TrySell(f.Inventory, 1, 2, out _) && f.Inventory.UsedSlots == 1 && f.Wallet.Gold == 989 &&
                    f.Shop.StockAt(0) == 29, "선택 슬롯 판매·재고 회수");
                Require(!f.Shop.TrySell(f.Inventory, 1, 1, out _) && f.Wallet.Gold == 989, "빈 슬롯 중복 판매 차단");
                checks += 3;
            }

            using (var f = new Fixture(4))
            {
                Require(!f.Shop.TryBuy(f.Inventory, 0, 1, out _) && f.Wallet.Gold == 4 && f.Shop.StockAt(0) == 30 &&
                    f.Inventory.UsedSlots == 0, "재화 부족 무변경");
                checks++;
            }
            using (var f = new Fixture(stock: 0))
            {
                Require(!f.Shop.TryBuy(f.Inventory, 0, 1, out _) && f.Wallet.Gold == 1000 &&
                    f.Inventory.UsedSlots == 0, "품절 무변경");
                checks++;
            }
            using (var f = new Fixture())
            {
                f.Inventory.Add("scrap", PlayerInventory.Capacity * 99);
                Require(!f.Shop.TryBuy(f.Inventory, 0, 1, out _) && f.Wallet.Gold == 1000 &&
                    f.Shop.StockAt(0) == 30 && f.Inventory.GetSlot(19).Count == 99, "가방 포화 무변경");
                f.Inventory.Remove("scrap", 1);
                Require(!f.Shop.TryBuy(f.Inventory, 0, 2, out _) && f.Inventory.GetSlot(19).Count == 98 &&
                    f.Wallet.Gold == 1000, "일부 공간만 있을 때 부분 구매 금지");
                Require(f.Shop.TryBuy(f.Inventory, 0, 1, out _) && f.Inventory.GetSlot(19).Count == 99,
                    "빈 슬롯 없이 남은 중첩 공간 구매");
                checks += 3;
            }
            using (var f = new Fixture())
            {
                f.Inventory.Add("scrap", 2);
                Require(!f.Shop.TryBuy(f.Inventory, -1, 1, out _) && !f.Shop.TryBuy(f.Inventory, int.MaxValue, 1, out _) &&
                    !f.Shop.TryBuy(f.Inventory, 0, 0, out _) && !f.Shop.TryBuy(f.Inventory, 0, -1, out _) &&
                    !f.Shop.TrySell(f.Inventory, -1, 1, out _) && !f.Shop.TrySell(f.Inventory, 0, 3, out _) &&
                    f.Inventory.GetSlot(0).Count == 2 && f.Wallet.Gold == 1000 && f.Shop.StockAt(0) == 30,
                    "잘못된 인덱스·수량·초과 판매 무변경");
                f.Player.transform.position += Vector3.forward * 3;
                Require(!f.Shop.TryBuy(f.Inventory, 0, 1, out _) && !f.Shop.TrySell(f.Inventory, 0, 1, out _),
                    "범위 밖 거래 차단");
                checks += 2;
            }
            using (var f = new Fixture(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue))
            {
                f.Inventory.Add("scrap", 2);
                Require(!f.Shop.TryBuy(f.Inventory, 0, 2, out _) && !f.Shop.TrySell(f.Inventory, 0, 1, out _) &&
                    f.Wallet.Gold == int.MaxValue && f.Inventory.GetSlot(0).Count == 2 && f.Shop.StockAt(0) == int.MaxValue,
                    "가격 곱셈·재화·재고 오버플로 차단");
                Require(!f.Wallet.TryAddGold(1) && !f.Wallet.TryAddGold(-1), "재화 지급 오버플로·음수 차단");
                checks += 2;
            }
            using (var f = new Fixture(0, int.MaxValue))
            {
                f.Inventory.Add("scrap", 1);
                Require(!f.Shop.TrySell(f.Inventory, 0, 1, out _) && f.Wallet.Gold == 0 &&
                    f.Inventory.GetSlot(0).Count == 1, "재고 오버플로 단독 검사");
                checks++;
            }
            using (var f = new Fixture(sell: 0))
            {
                f.Inventory.Add("scrap", 1);
                Require(!f.Shop.TrySell(f.Inventory, 0, 1, out _) && f.Wallet.Gold == 1000 &&
                    f.Inventory.GetSlot(0).Count == 1, "매입 불가 상품 판매 차단");
                checks++;
            }

            return checks;
        }

        private static void SetInt(Object target, string field, int value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).intValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Require(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException(label);
        }
    }
}
