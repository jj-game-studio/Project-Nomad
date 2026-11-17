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
    public sealed class Shopkeeper : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactionId = "temporary-shop";
        [SerializeField] private ShopCatalog catalog;
        [SerializeField] private LayerMask obstacleLayers = Physics.DefaultRaycastLayers;

        private int[] stock = Array.Empty<int>();
        private bool valid;
        public string InteractionId => interactionId;
        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.5f;
        public ShopCatalog Catalog => catalog;
        public event Action Changed;

        private void Awake()
        {
            if (catalog == null || catalog.Count == 0)
                return;

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
        }

        public int StockAt(int index) => stock[index];

        public int FindOffer(ItemDefinition item)
        {
            if (!valid || item == null)
                return -1;

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

        public bool TrySell(PlayerInventory inventory, int slotIndex, int quantity, out string message)
        {
            int offerIndex = inventory != null && (uint)slotIndex < PlayerInventory.Capacity
                ? FindOffer(inventory.GetSlot(slotIndex).Item) : -1;
            return Trade(inventory, offerIndex, slotIndex, quantity, false, out message);
        }

        private bool Trade(PlayerInventory inventory, int offerIndex, int slotIndex, int quantity,
            bool buying, out string message)
        {
            message = "Shop unavailable.";
            if (!CanAccess(inventory) || quantity <= 0 || (uint)offerIndex >= stock.Length)
                return false;

            PlayerWallet wallet = inventory.GetComponent<PlayerWallet>();
            if (wallet.Trading)
                return false;

            ShopCatalog.Offer offer = catalog.GetOffer(offerIndex);
            long total = (long)(buying ? offer.BuyPrice : offer.SellPrice) * quantity;
            if (buying && (stock[offerIndex] < quantity || total > wallet.Gold))
            {
                message = stock[offerIndex] < quantity ? "Not enough stock." : "Not enough gold.";
                return false;
            }

            if (buying)
            {
                if (!inventory.TryGetItem(offer.Item.ItemId, out ItemDefinition registered) || registered != offer.Item)
                    return false;

                long space = 0;
                for (int index = 0; index < PlayerInventory.Capacity; index++)
                    space += inventory.SpaceInSlot(index, offer.Item);
                if (space < quantity)
                {
                    message = "Backpack is full.";
                    return false;
                }
            }
            else if ((uint)slotIndex >= PlayerInventory.Capacity ||
                     inventory.GetSlot(slotIndex).Item != offer.Item || inventory.GetSlot(slotIndex).Count < quantity ||
                     offer.SellPrice == 0 || total + wallet.Gold > int.MaxValue ||
                     (long)stock[offerIndex] + quantity > int.MaxValue)
            {
                message = "Cannot sell this quantity or item.";
                return false;
            }

            // 모두 검사한 뒤 돈·재고·가방을 함께 반영합니다. 알림 중 재진입 거래도 막습니다.
            wallet.BeginTrade();
            try
            {
                wallet.SetGold((int)(wallet.Gold + (buying ? -total : total)));
                stock[offerIndex] += buying ? -quantity : quantity;
                if (buying)
                {
                    int remaining = quantity;
                    // 기존 중첩을 먼저 채워 불필요한 새 슬롯을 만들지 않습니다.
                    for (int pass = 0; pass < 2 && remaining > 0; pass++)
                        for (int index = 0; index < PlayerInventory.Capacity && remaining > 0; index++)
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
            }

            message = $"{(buying ? "Bought" : "Sold")} {offer.Item.DisplayName} x{quantity} / {total:N0} G";
            return true;
        }
    }
}
