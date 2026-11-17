using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        public const int Capacity = 20;

        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();

        private readonly Dictionary<string, ItemDefinition> catalog = new(StringComparer.Ordinal);
        private readonly ItemStack[] slots = new ItemStack[Capacity];
        private readonly List<int> stackOrder = new(Capacity);

        public int UsedSlots => stackOrder.Count;
        internal InventoryPanel Panel { get; set; }
        public event Action Changed;

        private void Awake()
        {
            foreach (ItemDefinition item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || item.MaxStack < 1)
                {
                    Debug.LogError("인벤토리 아이템 정의에 유효한 ID와 최대 중첩 수가 필요합니다.", this);
                    continue;
                }

                if (!catalog.TryAdd(item.ItemId, item))
                    Debug.LogError($"인벤토리에 중복된 아이템 ID가 있습니다. {item.ItemId}", this);
            }
        }

        public ItemStack GetStack(int index)
        {
            // 가방에서 자리를 옮겨도 기존 호출자는 스택이 추가된 순서로 읽습니다.
            return slots[stackOrder[index]];
        }

        public ItemStack GetSlot(int index)
        {
            return slots[index];
        }

        internal int SpaceInSlot(int index, ItemDefinition item)
        {
            ItemStack slot = slots[index];
            return slot.Item == null || slot.Item == item ? item.MaxStack - slot.Count : 0;
        }

        // 전송은 양쪽 수량을 먼저 반영한 뒤 알림을 보냅니다.
        internal int AddToSlotForTransfer(int index, ItemDefinition item, int requestedCount)
        {
            int added = Mathf.Min(requestedCount, SpaceInSlot(index, item));
            if (added <= 0)
                return 0;

            ItemStack slot = slots[index];
            if (slot.Item == null)
                stackOrder.Add(index);
            slots[index] = new ItemStack(item, slot.Count + added);
            return added;
        }

        internal void RemoveFromSlotForTransfer(int index, int count)
        {
            ItemStack stack = slots[index];
            int remaining = stack.Count - count;
            slots[index] = remaining == 0 ? default : new ItemStack(stack.Item, remaining);
            if (remaining == 0)
                stackOrder.Remove(index);
        }

        internal void NotifyChanged()
        {
            Changed?.Invoke();
        }

        internal bool TryGetItem(string itemId, out ItemDefinition item)
        {
            item = null;
            return itemId != null && catalog.TryGetValue(itemId, out item);
        }

        public bool MoveStack(int from, int to)
        {
            if ((uint)from >= Capacity || (uint)to >= Capacity || from == to)
                return false;

            ItemStack source = slots[from];
            if (source.Item == null)
                return false;

            ItemStack destination = slots[to];
            if (destination.Item == source.Item)
            {
                int moved = Mathf.Min(source.Count, source.Item.MaxStack - destination.Count);
                if (moved == 0)
                    return false;

                slots[to] = new ItemStack(source.Item, destination.Count + moved);
                slots[from] = moved == source.Count ? default : new ItemStack(source.Item, source.Count - moved);
                if (moved == source.Count)
                    stackOrder.Remove(from);
            }
            else
            {
                slots[from] = destination;
                slots[to] = source;
                int sourceOrder = stackOrder.IndexOf(from);
                int destinationOrder = stackOrder.IndexOf(to);
                stackOrder[sourceOrder] = to;
                if (destinationOrder >= 0)
                    stackOrder[destinationOrder] = from;
            }

            NotifyChanged();
            return true;
        }

        public int Add(string itemId, int requestedCount)
        {
            if (requestedCount <= 0 || itemId == null || !catalog.TryGetValue(itemId, out ItemDefinition item))
                return 0;

            int remaining = requestedCount;
            // 같은 아이템의 빈 공간을 먼저 채운 뒤 새 슬롯을 사용합니다.
            foreach (int index in stackOrder)
            {
                if (remaining == 0)
                    break;

                ItemStack stack = slots[index];
                if (stack.Item != item)
                    continue;

                int added = Mathf.Min(remaining, item.MaxStack - stack.Count);
                slots[index] = new ItemStack(item, stack.Count + added);
                remaining -= added;
            }

            for (int index = 0; index < slots.Length && UsedSlots < Capacity && remaining > 0; index++)
            {
                if (slots[index].Item != null)
                    continue;

                int added = Mathf.Min(remaining, item.MaxStack);
                slots[index] = new ItemStack(item, added);
                stackOrder.Add(index);
                remaining -= added;
            }

            return NotifyChange(requestedCount - remaining);
        }

        public int Remove(string itemId, int requestedCount)
        {
            if (requestedCount <= 0 || itemId == null || !catalog.TryGetValue(itemId, out ItemDefinition item))
                return 0;

            int remaining = requestedCount;
            for (int orderIndex = stackOrder.Count - 1; orderIndex >= 0 && remaining > 0; orderIndex--)
            {
                int index = stackOrder[orderIndex];
                ItemStack stack = slots[index];
                if (stack.Item != item)
                    continue;

                int removed = Mathf.Min(remaining, stack.Count);
                int count = stack.Count - removed;
                slots[index] = count == 0 ? default : new ItemStack(item, count);
                if (count == 0)
                    stackOrder.RemoveAt(orderIndex);

                remaining -= removed;
            }

            return NotifyChange(requestedCount - remaining);
        }

        private int NotifyChange(int changedCount)
        {
            if (changedCount > 0)
                NotifyChanged();

            return changedCount;
        }

        public readonly struct ItemStack
        {
            public ItemDefinition Item { get; }
            public int Count { get; }

            internal ItemStack(ItemDefinition item, int count)
            {
                Item = item;
                Count = count;
            }
        }
    }
}
