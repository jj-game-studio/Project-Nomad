using System;
using System.Collections.Generic;
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
                   context.Inventory.isActiveAndEnabled && Array.Exists(contents, stack => stack.Count > 0);
        }

        public void Interact(InteractionContext context)
        {
            // 거리·시야·구르기는 A의 PlayerInteraction에서 검사합니다.
            if (CanInteract(context))
                TryLoot(context.Inventory);
        }

        // A의 공용 계약/거리/시야/구르기 검증 후 호출할 B 소유 수량 전송입니다.
        public int TryLoot(PlayerInventory inventory)
        {
            if (busy || inventory == null || !isActiveAndEnabled)
                return 0;

            busy = true;
            int total = 0;
            try
            {
                for (int index = 0; index < contents.Length; index++)
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
                Status = Array.Exists(contents, stack => stack.Count > 0) ? LootStatus.Remaining : LootStatus.Empty;
                Message = Status == LootStatus.Empty ? "Empty" : "Items remain - inventory full or unknown item";
                RefreshVisuals();
                try { Changed?.Invoke(); }
                finally { busy = false; }
            }
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
