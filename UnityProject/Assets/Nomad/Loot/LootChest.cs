using System;
using Nomad.Inventory;
using Nomad.Interaction;
using UnityEngine;

namespace Nomad.Loot
{
    public enum LootStatus { Closed, Remaining, Empty }

    [Serializable]
    public struct LootStack
    {
        public string ItemId;
        public int Count;

        public LootStack(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    [DisallowMultipleComponent]
    public sealed class LootChest : MonoBehaviour, IInteractable
    {
        private const string RemainingMessage = "Items remain - inventory full or unknown item";

        [SerializeField] private string chestId;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private LootStack[] contents = Array.Empty<LootStack>();
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private GameObject openVisual;
        [SerializeField] private GameObject emptyVisual;

        private bool busy;
        public string ChestId => chestId;
        public string InteractionId => chestId;
        public Vector3 InteractionPoint => interactionPoint == null ? transform.position : interactionPoint.position;
        public LootStatus Status { get; private set; }
        public string Message { get; private set; } = "Closed";
        public int StackCount => contents.Length;
        public LootStack GetStack(int index) => contents[index];
        public event Action Changed;

        internal long CountItems()
        {
            long count = 0;
            foreach (LootStack stack in contents)
                count += stack.Count;

            return count;
        }

        private void Awake()
        {
            RefreshVisuals();
        }

        // 생성 시 한 번 초기화합니다. 진행 중인 루팅에서 보유 목록을 교체하지 않습니다.
        public void Initialize(string id, LootStack[] stacks)
        {
            if (busy || Status != LootStatus.Closed)
                throw new InvalidOperationException("열린 상자는 다시 초기화할 수 없습니다.");
            if (string.IsNullOrWhiteSpace(id) || stacks == null)
                throw new ArgumentException("고유 ID와 보유 목록이 필요합니다.");

            foreach (LootStack stack in stacks)
            {
                if (string.IsNullOrWhiteSpace(stack.ItemId) || stack.Count <= 0)
                    throw new ArgumentException("상자 항목에는 ID와 양의 수량이 필요합니다.");
            }

            chestId = id;
            contents = (LootStack[])stacks.Clone();
        }

        public bool CanInteract(InteractionContext context)
        {
            return isActiveAndEnabled && !busy && !string.IsNullOrWhiteSpace(chestId) &&
                   context.Player != null && context.Inventory != null &&
                   context.Inventory.isActiveAndEnabled;
        }

        public void Interact(InteractionContext context)
        {
            // 거리·시야·구르기는 A의 PlayerInteraction에서 검사합니다.
            if (CanInteract(context) && HasLoot())
                TryLoot(context.Inventory);
        }

        internal void Inspect()
        {
            if (Status != LootStatus.Closed)
                return;

            Status = HasLoot() ? LootStatus.Remaining : LootStatus.Empty;
            Message = Status == LootStatus.Empty ? "Empty" : "Opened";
            RefreshVisuals();
            Changed?.Invoke();
        }

        // A의 공용 계약/거리/시야/구르기 검증 후 호출할 B 소유 수량 전송입니다.
        public int TryLoot(PlayerInventory inventory)
        {
            return Transfer(inventory, 0, contents.Length);
        }

        public int TryLootStack(PlayerInventory inventory, int index)
        {
            if ((uint)index >= (uint)contents.Length || contents[index].Count <= 0)
                return 0;

            return Transfer(inventory, index, index + 1);
        }

        public int TryLootStackToSlot(PlayerInventory inventory, int index, int slotIndex)
        {
            if (!CanTransfer(inventory) || (uint)index >= (uint)contents.Length ||
                (uint)slotIndex >= PlayerInventory.Capacity)
                return 0;

            LootStack stack = contents[index];
            if (stack.Count <= 0 || !inventory.TryGetItem(stack.ItemId, out ItemDefinition item))
                return 0;

            busy = true;
            try
            {
                int moved = inventory.AddToSlotForTransfer(slotIndex, item, stack.Count);
                if (moved == 0)
                    return 0;

                contents[index].Count -= moved;
                PublishTransfer(RemainingMessage, inventory);
                return moved;
            }
            finally
            {
                busy = false;
            }
        }

        public int TryStoreStack(PlayerInventory inventory, int slotIndex)
        {
            if (!CanTransfer(inventory) || (uint)slotIndex >= PlayerInventory.Capacity)
                return 0;

            PlayerInventory.ItemStack stack = inventory.GetSlot(slotIndex);
            if (stack.Item == null)
                return 0;

            busy = true;
            try
            {
                LootStack[] updated = BuildStoredContents(stack);
                // 재진입하는 구독자에게도 가방과 상자 양쪽이 반영된 상태를 보여 줍니다.
                inventory.RemoveSlotForTransfer(slotIndex);
                contents = updated;
                PublishTransfer("Items stored", inventory);
                return stack.Count;
            }
            finally
            {
                busy = false;
            }
        }

        private LootStack[] BuildStoredContents(PlayerInventory.ItemStack stack)
        {
            var updated = (LootStack[])contents.Clone();
            int remaining = stack.Count;
            int empty = -1;
            for (int i = 0; i < updated.Length && remaining > 0; i++)
            {
                LootStack stored = updated[i];
                if (stored.Count <= 0)
                {
                    if (empty < 0)
                        empty = i;
                    continue;
                }

                if (stored.ItemId != stack.Item.ItemId || stored.Count >= stack.Item.MaxStack)
                    continue;

                int added = Mathf.Min(remaining, stack.Item.MaxStack - stored.Count);
                updated[i].Count += added;
                remaining -= added;
            }

            if (remaining > 0)
            {
                if (empty < 0)
                {
                    empty = updated.Length;
                    Array.Resize(ref updated, empty + 1);
                }

                updated[empty] = new LootStack(stack.Item.ItemId, remaining);
            }

            return updated;
        }

        private bool CanTransfer(PlayerInventory inventory)
        {
            return !busy && inventory != null && inventory.isActiveAndEnabled && isActiveAndEnabled;
        }

        private bool HasLoot()
        {
            return Array.Exists(contents, stack => stack.Count > 0);
        }

        private int Transfer(PlayerInventory inventory, int start, int end)
        {
            if (!CanTransfer(inventory))
                return 0;

            busy = true;
            int total = 0;
            try
            {
                for (int index = start; index < end; index++)
                {
                    LootStack stack = contents[index];
                    if (stack.Count <= 0)
                        continue;

                    int before = Count(inventory, stack.ItemId);
                    try
                    {
                        int added = inventory.Add(stack.ItemId, stack.Count);
                        stack.Count -= added;
                        total += added;
                    }
                    catch
                    {
                        // Add가 수량을 반영한 뒤 외부 Changed 구독자가 예외를 던져도 복제하지 않습니다.
                        stack.Count -= Mathf.Clamp(Count(inventory, stack.ItemId) - before, 0, stack.Count);
                        throw;
                    }
                    finally
                    {
                        contents[index] = stack;
                    }
                }

                return total;
            }
            finally
            {
                try { PublishTransfer(RemainingMessage); }
                finally { busy = false; }
            }
        }

        private void PublishTransfer(string remainingMessage, PlayerInventory inventory = null)
        {
            Status = HasLoot() ? LootStatus.Remaining : LootStatus.Empty;
            Message = Status == LootStatus.Empty ? "Empty" : remainingMessage;
            RefreshVisuals();
            try { inventory?.NotifyChanged(); }
            finally { Changed?.Invoke(); }
        }

        private static int Count(PlayerInventory inventory, string id)
        {
            int count = 0;
            for (int index = 0; index < inventory.UsedSlots; index++)
            {
                PlayerInventory.ItemStack stack = inventory.GetStack(index);
                if (stack.Item.ItemId == id)
                    count += stack.Count;
            }
            return count;
        }

        private void RefreshVisuals()
        {
            if (closedVisual != null) closedVisual.SetActive(Status == LootStatus.Closed);
            if (openVisual != null) openVisual.SetActive(Status == LootStatus.Remaining);
            if (emptyVisual != null) emptyVisual.SetActive(Status == LootStatus.Empty);
        }
    }
}
