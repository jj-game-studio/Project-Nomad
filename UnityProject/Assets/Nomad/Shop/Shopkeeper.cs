using System;
using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Player;
using UnityEngine;

namespace Nomad.Shop
{
    [DisallowMultipleComponent]
    public sealed class Shopkeeper : MonoBehaviour, IInteractable, IInteractionAction
    {
        [SerializeField] private string interactionId = "temporary-shop";
        [SerializeField] private ShopCatalog catalog;
        [SerializeField] private LayerMask obstacleLayers = Physics.DefaultRaycastLayers;

        private int[] stock = Array.Empty<int>();
        private bool valid;
        private bool trading;
        private double nextRestockAt;
        public string InteractionId => interactionId;
        public string InteractionAction => "거래";
        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.5f;
        public ShopCatalog Catalog => catalog;
        public double RestockRemaining => catalog != null && catalog.RestockSeconds > 0
            ? Math.Max(0, nextRestockAt - Time.timeAsDouble) : 0;
        public event Action Changed;

        private void Awake()
        {
            if (catalog == null || catalog.Count == 0)
                return;
            if (float.IsNaN(catalog.RestockSeconds) || float.IsInfinity(catalog.RestockSeconds) || catalog.RestockSeconds < 0)
            {
                Debug.LogError("상점 재입고 주기는 유한한 0 이상의 값이어야 합니다.", this);
                return;
            }

            stock = new int[catalog.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Count; index++)
            {
                ShopCatalog.Offer offer = catalog.GetOffer(index);
                if (offer == null || offer.Item == null || string.IsNullOrWhiteSpace(offer.Item.ItemId) ||
                    offer.Item.MaxStack < 1 || offer.BuyPrice < 1 || offer.SellPrice < 0 ||
                    offer.SellPrice > offer.BuyPrice || offer.InitialStock < 0 || !ids.Add(offer.Item.ItemId))
                {
                    Debug.LogError("상점 상품·가격·재고 또는 중복 ID를 확인해 주세요.", this);
                    return;
                }

                stock[index] = offer.InitialStock;
            }

            valid = true;
            nextRestockAt = Time.timeAsDouble + catalog.RestockSeconds;
        }

        private void Update() => RefreshStock();

        private void RefreshStock()
        {
            if (!valid || trading || catalog.RestockSeconds <= 0 || Time.timeScale <= 0 ||
                Time.timeAsDouble < nextRestockAt)
                return;

            // 지난 주기는 한 번에 건너뛰고 부족분만 채웁니다. 플레이어가 판 초과 재고는 보존합니다.
            double periods = Math.Floor((Time.timeAsDouble - nextRestockAt) / catalog.RestockSeconds) + 1;
            nextRestockAt += periods * catalog.RestockSeconds;
            for (int index = 0; index < stock.Length; index++)
                stock[index] = Math.Max(stock[index], catalog.GetOffer(index).InitialStock);
            Changed?.Invoke();
        }

        public int StockAt(int index) => stock[index];

        public int FindOffer(ItemDefinition item)
        {
            if (!valid || item == null)
                return -1;
            item = item.BaseItem != null ? item.BaseItem : item;

            for (int index = 0; index < catalog.Count; index++)
                if (catalog.GetOffer(index).Item == item)
                    return index;
            return -1;
        }

        public bool CanInteract(InteractionContext context) =>
            context.Inventory != null && !context.Inventory.BlocksTradeInput && CanAccess(context.Inventory);

        public void Interact(InteractionContext context)
        {
            if (CanInteract(context))
                ShopPanel.Open(this, context.Inventory);
        }

        public bool CanAccess(PlayerInventory inventory)
        {
            if (!valid || !isActiveAndEnabled || inventory == null || !inventory.isActiveAndEnabled ||
                Time.timeScale <= 0f || !inventory.TryGetComponent(out PlayerWallet wallet) ||
                !wallet.isActiveAndEnabled ||
                (inventory.TryGetComponent(out Health health) && health.IsDead) ||
                (inventory.TryGetComponent(out PlayerMovement movement) && (!movement.isActiveAndEnabled || movement.IsRolling)))
                return false;

            RefreshStock();

            Vector3 feet = inventory.transform.position;
            Vector3 body = feet + Vector3.up;
            if (inventory.TryGetComponent(out CharacterController controller))
            {
                if (!controller.enabled)
                    return false;

                body = inventory.transform.TransformPoint(controller.center);
                feet = inventory.transform.TransformPoint(controller.center - Vector3.up * controller.height * 0.5f);
            }

            if ((InteractionPoint - feet).sqrMagnitude > 4f)
                return false;

            Vector3 delta = InteractionPoint - body;
            foreach (RaycastHit hit in Physics.RaycastAll(body, delta.normalized, delta.magnitude,
                         obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(inventory.transform) && !hit.transform.IsChildOf(transform))
                    return false;
            }

            return true;
        }

        public bool TryBuy(PlayerInventory inventory, int offerIndex, int quantity, out string message) =>
            Trade(inventory, offerIndex, -1, quantity, true, out message);

        public int BuyLimit(PlayerInventory inventory, int offerIndex)
        {
            if (!CanAccess(inventory) || (uint)offerIndex >= stock.Length)
                return 0;

            ShopCatalog.Offer offer = catalog.GetOffer(offerIndex);
            if (offer.SellOnly || !inventory.TryGetItem(offer.Item.ItemId, out ItemDefinition registered) ||
                registered != offer.Item)
                return 0;

            long space = 0;
            for (int index = 0; index < PlayerInventory.MaxCapacity; index++)
                space += inventory.SpaceInSlot(index, offer.Item);
            return (int)Math.Min(space, Math.Min(stock[offerIndex], inventory.GetComponent<PlayerWallet>().Gold / offer.BuyPrice));
        }

        public bool CanBuy(PlayerInventory inventory, int offerIndex, int quantity, out string message) =>
            ValidateTrade(inventory, offerIndex, -1, quantity, true, out message);

        public bool CanSell(PlayerInventory inventory, int slotIndex, int quantity, out string message)
        {
            int offer = inventory != null && (uint)slotIndex < PlayerInventory.MaxCapacity
                ? FindOffer(inventory.GetSlot(slotIndex).Item) : -1;
            return ValidateTrade(inventory, offer, slotIndex, quantity, false, out message);
        }

        public bool TrySell(PlayerInventory inventory, int slotIndex, int quantity, out string message)
        {
            int offerIndex = inventory != null && (uint)slotIndex < PlayerInventory.MaxCapacity
                ? FindOffer(inventory.GetSlot(slotIndex).Item) : -1;
            return Trade(inventory, offerIndex, slotIndex, quantity, false, out message);
        }

        private bool ValidateTrade(PlayerInventory inventory, int offerIndex, int slotIndex, int quantity,
            bool buying, out string message)
        {
            message = "지금은 거래할 수 없습니다.";
            if (!CanAccess(inventory) || quantity <= 0 || (uint)offerIndex >= stock.Length)
                return false;

            PlayerWallet wallet = inventory.GetComponent<PlayerWallet>();
            if (wallet.Trading || trading)
                return false;

            ShopCatalog.Offer offer = catalog.GetOffer(offerIndex);
            if (!buying && wallet.IsSaleProtected(offer.Item))
            {
                message = "판매가 잠겨 있습니다. 잠금을 해제한 뒤 판매하세요.";
                return false;
            }
            if (buying && offer.SellOnly)
            {
                message = "판매만 받는 물건입니다.";
                return false;
            }

            long total = (long)(buying ? offer.BuyPrice : offer.SellPrice) * quantity;
            if (buying && (stock[offerIndex] < quantity || total > wallet.Gold))
            {
                message = stock[offerIndex] < quantity ? "재고가 부족합니다." : "골드가 부족합니다.";
                return false;
            }

            if (buying)
            {
                if (!inventory.TryGetItem(offer.Item.ItemId, out ItemDefinition registered) || registered != offer.Item)
                {
                    message = "가방에 등록되지 않은 아이템입니다.";
                    return false;
                }

                long space = 0;
                for (int index = 0; index < PlayerInventory.MaxCapacity; index++)
                    space += inventory.SpaceInSlot(index, offer.Item);
                if (space < quantity)
                {
                    message = "가방에 빈 공간이 부족합니다.";
                    return false;
                }
            }
            else if ((uint)slotIndex >= PlayerInventory.MaxCapacity ||
                     (inventory.GetSlot(slotIndex).Item?.BaseItem ?? inventory.GetSlot(slotIndex).Item) != offer.Item || inventory.GetSlot(slotIndex).Count < quantity ||
                     offer.SellPrice == 0 || total + wallet.Gold > int.MaxValue ||
                     (long)stock[offerIndex] + quantity > int.MaxValue)
            {
                message = "이 아이템이나 수량은 판매할 수 없습니다.";
                return false;
            }

            message = "거래할 수 있습니다.";
            return true;
        }

        private bool Trade(PlayerInventory inventory, int offerIndex, int slotIndex, int quantity,
            bool buying, out string message)
        {
            if (!ValidateTrade(inventory, offerIndex, slotIndex, quantity, buying, out message))
                return false;

            PlayerWallet wallet = inventory.GetComponent<PlayerWallet>();
            ShopCatalog.Offer offer = catalog.GetOffer(offerIndex);
            long total = (long)(buying ? offer.BuyPrice : offer.SellPrice) * quantity;

            // 모두 검사한 뒤 돈·재고·가방을 함께 반영합니다. 알림 중 재진입 거래도 막습니다.
            wallet.BeginTrade();
            trading = true;
            try
            {
                wallet.SetGold((int)(wallet.Gold + (buying ? -total : total)));
                if (!offer.SellOnly)
                    stock[offerIndex] += buying ? -quantity : quantity;
                if (buying)
                {
                    int remaining = quantity;
                    // 기존 중첩을 먼저 채워 불필요한 새 슬롯을 만들지 않습니다.
                    for (int pass = 0; pass < 2 && remaining > 0; pass++)
                        for (int index = 0; index < PlayerInventory.MaxCapacity && remaining > 0; index++)
                            if ((inventory.GetSlot(index).Item == offer.Item) == (pass == 0))
                                remaining -= inventory.AddToSlotForTransfer(index, offer.Item, remaining);
                }
                else
                    inventory.RemoveFromSlotForTransfer(slotIndex, quantity);

                inventory.NotifyChanged();
                wallet.NotifyChanged();
                Changed?.Invoke();
            }
            finally
            {
                wallet.EndTrade();
                trading = false;
            }

            message = $"{(buying ? "구매 완료" : "판매 완료")} · {offer.DisplayName} {quantity:N0}개 / {total:N0} 골드";
            return true;
        }
    }
}
