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
        private readonly List<ItemStack> stacks = new(Capacity);

        public int UsedSlots => stacks.Count;
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
            return stacks[index];
        }

        public int Add(string itemId, int requestedCount)
        {
            if (requestedCount <= 0 || itemId == null || !catalog.TryGetValue(itemId, out ItemDefinition item))
                return 0;

            int remaining = requestedCount;
            // 같은 아이템의 빈 공간을 먼저 채운 뒤 새 슬롯을 사용합니다.
            for (int index = 0; index < stacks.Count && remaining > 0; index++)
            {
                ItemStack stack = stacks[index];
                if (stack.Item != item)
                    continue;

                int added = Mathf.Min(remaining, item.MaxStack - stack.Count);
                stacks[index] = new ItemStack(item, stack.Count + added);
                remaining -= added;
            }

            while (remaining > 0 && stacks.Count < Capacity)
            {
                int added = Mathf.Min(remaining, item.MaxStack);
                stacks.Add(new ItemStack(item, added));
                remaining -= added;
            }

            return NotifyChange(requestedCount - remaining);
        }

        public int Remove(string itemId, int requestedCount)
        {
            if (requestedCount <= 0 || itemId == null || !catalog.TryGetValue(itemId, out ItemDefinition item))
                return 0;

            int remaining = requestedCount;
            for (int index = stacks.Count - 1; index >= 0 && remaining > 0; index--)
            {
                ItemStack stack = stacks[index];
                if (stack.Item != item)
                    continue;

                int removed = Mathf.Min(remaining, stack.Count);
                int count = stack.Count - removed;
                if (count == 0)
                    stacks.RemoveAt(index);
                else
                    stacks[index] = new ItemStack(item, count);

                remaining -= removed;
            }

            return NotifyChange(requestedCount - remaining);
        }

        private int NotifyChange(int changedCount)
        {
            if (changedCount > 0)
                Changed?.Invoke();

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
