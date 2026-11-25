using System;
using System.Collections.Generic;
using System.Linq;
using Nomad.Game;
using Nomad.Inventory;
using Nomad.Loot;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [RequireComponent(typeof(MonsterDeathDrop))]
    public sealed class MonsterEquipmentLoot : MonoBehaviour
    {
        private MonsterDeathDrop death;
        private PlayerInventory recipient;
        private MonsterEquipmentDropTable table;
        private Vector3 spawnPosition;

        public void Configure(PlayerInventory inventory)
        {
            recipient = inventory;
            table = Resources.Load<MonsterEquipmentDropTable>("MonsterEquipmentDrops");
            spawnPosition = transform.position;
        }

        private void Awake() => death = GetComponent<MonsterDeathDrop>();
        private void OnEnable() => death.DropCreated += OnDrop;
        private void OnDisable() => death.DropCreated -= OnDrop;

        private void OnDrop(PlaceholderEquipmentDrop drop)
        {
            if (recipient == null || table == null) return;
            int seed = GetEntityId().GetHashCode() ^ Time.frameCount;
            LootStack[] contents = table.Roll(drop.SourceRank, recipient, seed);

            // 프리셋 필드의 엘리트는 확정 티어 이상 장비 1개를 더 떨어뜨립니다(#194).
            FieldPreset preset = GameManager.Instance != null ? GameManager.Instance.SelectedField : null;
            if (drop.SourceRank == MonsterRank.Elite && preset != null)
            {
                ItemDefinition bonus = PickHighTierGear(recipient.Catalog, preset, new System.Random(seed));
                if (bonus != null)
                    contents = contents.Append(new LootStack(bonus.ItemId, 1)).ToArray();
            }

            if (drop.SourceRank == MonsterRank.Boss)
                ChestSpawner.CreateBossReward(transform.parent, spawnPosition, "boss-reward-" + GetEntityId().GetHashCode(), contents);
            else
            {
                // 시체 자체가 루팅 대상입니다. 낙하 상자는 숨기고 사망한 몸의 충돌을 상호작용 트리거로 바꿉니다.
                foreach (Collider collider in GetComponents<Collider>()) collider.enabled = false;
                var trigger = gameObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = Vector3.up * 0.3f;
                trigger.size = new Vector3(1.1f, 0.6f, 1.1f);
                LootChest chest = gameObject.AddComponent<LootChest>();
                chest.Initialize("corpse-" + GetEntityId().GetHashCode(), contents);
                chest.ConfigureVisuals(transform, null, null, null);
            }
            drop.gameObject.SetActive(false);
            Destroy(drop.gameObject);
        }

        // 프리셋 무기 비율로 무기·방어구 중 하나를 정하고, 확정 티어 이상 장비 중 균등하게 고릅니다.
        // 해당 후보가 없으면 반대쪽, 그래도 없으면 가장 높은 티어 장비를 씁니다.
        private static ItemDefinition PickHighTierGear(IEnumerable<ItemDefinition> catalog, FieldPreset preset, System.Random random)
        {
            List<ItemDefinition> gear = catalog.Where(item => item != null && item.Tier != ItemTier.None &&
                (item.ItemId.StartsWith("gear-", StringComparison.Ordinal) ||
                 item.ItemId.StartsWith("weapon-", StringComparison.Ordinal)) && item.EquipmentType != EquipmentType.None)
                .OrderBy(item => item.ItemId, StringComparer.Ordinal).ToList();
            if (gear.Count == 0)
                return null;

            var minimum = (ItemTier)(preset.GuaranteedTier + 1);
            bool weapon = random.NextDouble() < preset.WeaponShare;
            List<ItemDefinition> high = gear.Where(item => item.Tier >= minimum).ToList();
            List<ItemDefinition> side = high.Where(item => item.ItemId.StartsWith("weapon-", StringComparison.Ordinal) == weapon).ToList();

            List<ItemDefinition> pool = side.Count > 0 ? side : high;
            if (pool.Count == 0)
                pool = gear.Where(item => item.Tier == gear.Max(other => other.Tier)).ToList();

            return pool[random.Next(pool.Count)];
        }

    }
}
