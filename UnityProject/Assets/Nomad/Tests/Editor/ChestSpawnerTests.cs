using System.Collections.Generic;
using System.Linq;
using Nomad.Inventory;
using Nomad.Loot;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nomad.Tests
{
    // 상자 내용물 티어 테이블이 실제 아이템과 맞고, 높은 티어일수록 드물게 나오는지 확인합니다(#120).
    public sealed class ChestSpawnerTests
    {
        private const string ItemFolder = "Assets/Nomad/Equipment/Items";
        private GameObject host;
        private ChestSpawner spawner;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Chest spawner test");
            spawner = host.AddComponent<ChestSpawner>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void TableIdsMatchRealItemsAndTiers()
        {
            foreach (ChestSpawner.TierLoot tier in spawner.Tiers)
            {
                Assert.That(Load(tier.ScrapId).Tier.ToString(), Is.EqualTo(tier.Tier), tier.ScrapId);
                Assert.That(Load(tier.ScrapId).EquipmentType, Is.EqualTo(EquipmentType.None), "고철은 판매 전용");
                foreach (string id in tier.GearIds)
                    Assert.That(Load(id).Tier.ToString(), Is.EqualTo(tier.Tier), id);
            }

            foreach (ChestSpawner.LootEntry entry in spawner.Extras)
                Assert.That(Load(entry.ItemId), Is.Not.Null, entry.ItemId);
        }

        [Test]
        public void EveryTieredEquipmentCanDrop()
        {
            var pool = new HashSet<string>(spawner.Tiers.SelectMany(tier => tier.GearIds));
            string[] missing = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item.EquipmentType != EquipmentType.None && item.Tier != ItemTier.None && !pool.Contains(item.ItemId))
                .Select(item => item.ItemId).ToArray();
            Assert.That(missing, Is.Empty, "티어가 있는 장비는 모두 상자에서 나올 수 있어야 합니다.");
        }

        [Test]
        public void HigherTiersAreRarer()
        {
            Dictionary<string, string> tierOf = spawner.Tiers
                .SelectMany(tier => tier.GearIds.Append(tier.ScrapId).Select(id => (id, tier.Tier)))
                .ToDictionary(pair => pair.id, pair => pair.Tier);
            var scrap = new Dictionary<string, int>();
            var gear = new Dictionary<string, int>();
            const int rolls = 5000;
            for (int seed = 0; seed < rolls; seed++)
            {
                LootStack[] stacks = spawner.RollContents(seed);
                Assert.That(stacks.Count(stack => stack.ItemId.StartsWith("scrap")), Is.EqualTo(1), "상자마다 고철 1스택");
                foreach (LootStack stack in stacks)
                {
                    Assert.That(stack.Count, Is.GreaterThan(0));
                    if (!tierOf.TryGetValue(stack.ItemId, out string tier))
                        continue;

                    Dictionary<string, int> counts = stack.ItemId.StartsWith("scrap") ? scrap : gear;
                    counts[tier] = counts.TryGetValue(tier, out int count) ? count + 1 : 1;
                }
            }

            string[] order = spawner.Tiers.Select(tier => tier.Tier).ToArray();
            for (int i = 1; i < order.Length; i++)
            {
                Assert.That(scrap.GetValueOrDefault(order[i]), Is.LessThan(scrap.GetValueOrDefault(order[i - 1])), "고철 " + order[i]);
                Assert.That(gear.GetValueOrDefault(order[i]), Is.LessThan(gear.GetValueOrDefault(order[i - 1])), "장비 " + order[i]);
            }

            Assert.That(gear.GetValueOrDefault("SSS"), Is.LessThan(rolls / 100), "SSS 장비는 매우 드묾");
        }

        private static ItemDefinition Load(string id) =>
            AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{id}.asset");
    }
}
