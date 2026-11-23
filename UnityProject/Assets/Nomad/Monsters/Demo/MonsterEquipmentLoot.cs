using System;
using System.Collections;
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

        public void Configure(PlayerInventory inventory)
        {
            recipient = inventory;
            table = Resources.Load<MonsterEquipmentDropTable>("MonsterEquipmentDrops");
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

            if (contents.Length == 0) return;
            drop.name = "Dropped equipment - " + drop.SourceRank;
            StartCoroutine(Activate(drop, contents));
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

        private static IEnumerator Activate(PlaceholderEquipmentDrop drop, LootStack[] contents)
        {
            while (drop != null && !drop.Landed) yield return null;
            if (drop == null) yield break;
            var trigger = drop.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 0.25f;
            trigger.size = new Vector3(0.8f, 0.5f, 0.8f);
            LootChest chest = drop.gameObject.AddComponent<LootChest>();
            chest.Initialize("monster-drop-" + drop.GetEntityId().GetHashCode(), contents);
            chest.ConfigureVisuals(drop.transform, null, null, null);
        }
    }
}
