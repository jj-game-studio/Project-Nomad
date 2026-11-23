using System;
using System.Collections.Generic;
using Nomad.Inventory;
using Nomad.Loot;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [CreateAssetMenu(menuName = "Nomad/Monsters/Equipment Drop Table")]
    public sealed class MonsterEquipmentDropTable : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] normal = Array.Empty<ItemDefinition>();
        [SerializeField] private ItemDefinition[] elite = Array.Empty<ItemDefinition>();
        [SerializeField] private ItemDefinition[] boss = Array.Empty<ItemDefinition>();
        [SerializeField, Min(0)] private int normalCount = 1;
        [SerializeField, Min(0)] private int eliteCount = 1;
        [SerializeField, Min(0)] private int bossCount = 2;

        public LootStack[] Roll(MonsterRank rank, PlayerInventory inventory, int seed)
        {
            ItemDefinition[] pool = rank == MonsterRank.Boss ? boss : rank == MonsterRank.Elite ? elite : normal;
            int count = rank == MonsterRank.Boss ? bossCount : rank == MonsterRank.Elite ? eliteCount : normalCount;
            var valid = new List<ItemDefinition>();
            foreach (ItemDefinition item in pool)
                if (item != null && item.EquipmentType != EquipmentType.None &&
                    inventory.TryGetItem(item.ItemId, out _) && !valid.Contains(item)) valid.Add(item);
            if (valid.Count == 0 || count == 0) return Array.Empty<LootStack>();

            var random = new System.Random(seed);
            var result = new List<LootStack>();
            for (int index = 0; index < count; index++)
            {
                int selected = random.Next(valid.Count);
                result.Add(new LootStack(valid[selected].ItemId, 1));
                // 보스 보상이 같은 장비만 반복되지 않도록 가능한 동안은 중복을 피합니다.
                if (valid.Count > 1) valid.RemoveAt(selected);
            }
            return result.ToArray();
        }
    }
}
