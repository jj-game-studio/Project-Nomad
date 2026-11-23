using System.Collections.Generic;
using Nomad.Inventory;
using Nomad.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Loot
{
    // 저장된 지형 통합 검토 씬에서 Ready 결과의 상자 지점에 상자를 배치하고 파밍 검토 키를 제공합니다.
    public sealed class TerrainLootIntegration : MonoBehaviour
    {
        // F 채우기용 아이템입니다. 시작 장비(단검·누비 갑옷)와 겹치지 않게 사슬 갑옷을 씁니다.
        private static readonly string[] ReviewItems = { "scrap", "gear-chain-mail" };

        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private UnityEngine.UI.Text status;
        [SerializeField] private bool useScatteredCrateSlots;
        private ChestSpawner spawner;
        private LootChest lastChest;

        public string Summary => status == null ? string.Empty : status.text;
        public ChestSpawner Spawner
        {
            get
            {
                if (spawner == null && !TryGetComponent(out spawner))
                    spawner = gameObject.AddComponent<ChestSpawner>();
                return spawner;
            }
        }
        public IReadOnlyList<LootChest> Chests => Spawner.Chests;
        public bool UseScatteredCrateSlots => useScatteredCrateSlots;

        // 재생성 시작에는 null로 이전 상자를 정리하고, Ready 결과가 나오면 새 상자를 배치합니다.
        public void Refresh(FieldGenerationResult field)
        {
            lastChest = null;
            // 상자 내용물 후보는 플레이어 가방 카탈로그에서 고릅니다. 게임 필드는 인벤토리 참조가 비어 있어 찾아 씁니다.
            PlayerInventory owner = inventory != null ? inventory : FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            Spawner.SetCatalog(owner != null ? owner.Catalog : null);
            foreach (LootChest chest in Spawner.Spawn(field, useScatteredCrateSlots))
                chest.Changed += () => lastChest = chest;
        }

        private void Update()
        {
            if (inventory == null || !inventory.isActiveAndEnabled) return;
            Keyboard keyboard = Keyboard.current;

            // F: 가방을 가득 채워 공간 부족을 확인합니다. V: 검토용 아이템을 모두 비웁니다.
            if (keyboard?.fKey.wasPressedThisFrame == true)
            {
                ClearReviewItems();
                inventory.Add("scrap", 96);
                inventory.Add("gear-chain-mail", 19);
            }
            if (keyboard?.vKey.wasPressedThisFrame == true)
                ClearReviewItems();
        }

        private void ClearReviewItems()
        {
            foreach (string id in ReviewItems)
                inventory.Remove(id, int.MaxValue);
            // 상자에서 받은 티어별 판매 아이템도 함께 비웁니다.
            foreach (ItemDefinition item in inventory.Catalog)
                if (item.EquipmentType == EquipmentType.None && item.Tier != ItemTier.None)
                    inventory.Remove(item.ItemId, int.MaxValue);
        }

        private void LateUpdate()
        {
            if (status == null || inventory == null) return;
            int scrap = 0;
            for (int i = 0; i < inventory.UsedSlots; i++)
                if (inventory.GetStack(i).Item.ItemId.StartsWith("scrap")) scrap += inventory.GetStack(i).Count;
            string result = lastChest == null ? "Approach a chest and press E"
                : $"Last chest: {lastChest.CountItems()} items  ·  {lastChest.Status}";
            status.text = $"E Open chest | F Fill inventory | V Clear test loot | Chests {Chests.Count}\n"
                + $"Scrap {scrap} | Slots {inventory.UsedSlots}/20 | {result}";
        }
    }
}
