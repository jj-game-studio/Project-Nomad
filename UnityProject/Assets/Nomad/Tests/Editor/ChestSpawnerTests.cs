using System.Collections.Generic;
using System.Linq;
using Nomad.Inventory;
using Nomad.Loot;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nomad.Tests
{
    // 상자 내용물이 카탈로그의 티어별 후보에서 뽑히고, 높은 티어일수록 드물게 나오는지 확인합니다(#120).
    public sealed class ChestSpawnerTests
    {
        private const int Rolls = 5000;
        private GameObject host;
        private ChestSpawner spawner;
        private Dictionary<string, ItemDefinition> items;

        [SetUp]
        public void SetUp()
        {
            items = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Nomad/Equipment/Items" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToDictionary(item => item.ItemId);
            host = new GameObject("Chest spawner test");
            spawner = host.AddComponent<ChestSpawner>();
            spawner.SetCatalog(items.Values);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void SameSeedRollsSameContents()
        {
            for (int seed = 0; seed < 50; seed++)
                Assert.That(Describe(spawner.RollContents(seed)), Is.EqualTo(Describe(spawner.RollContents(seed))));

            // 카탈로그 순서가 달라도 같은 seed면 같은 내용물입니다.
            string first = Describe(spawner.RollContents(7));
            spawner.SetCatalog(items.Values.Reverse());
            Assert.That(Describe(spawner.RollContents(7)), Is.EqualTo(first));
        }

        [Test]
        public void ContentsComeFromTieredCatalog()
        {
            for (int seed = 0; seed < Rolls; seed++)
            {
                LootStack[] stacks = spawner.RollContents(seed);
                Assert.That(stacks.Select(stack => stack.ItemId), Is.Unique, "같은 아이템은 한 스택으로 합침");
                Assert.That(stacks.Count(stack => IsSell(items[stack.ItemId])), Is.GreaterThan(0), "판매 아이템은 항상 포함");
                Assert.That(stacks.Count(stack => IsGear(items[stack.ItemId])), Is.LessThanOrEqualTo(1), "장비는 최대 1개");
                foreach (LootStack stack in stacks)
                {
                    ItemDefinition item = items[stack.ItemId];
                    Assert.That(stack.Count, Is.InRange(1, item.MaxStack), stack.ItemId);
                    Assert.That(IsSell(item) || IsGear(item) || spawner.Extras.Any(extra => extra.ItemId == item.ItemId),
                        Is.True, stack.ItemId);
                }
            }
        }

        [Test]
        public void HigherTiersAreRarer()
        {
            var sell = new Dictionary<ItemTier, int>();
            var gear = new Dictionary<ItemTier, int>();
            int gearTotal = 0;
            for (int seed = 0; seed < Rolls; seed++)
            {
                foreach (LootStack stack in spawner.RollContents(seed))
                {
                    ItemDefinition item = items[stack.ItemId];
                    if (IsSell(item))
                        sell[item.Tier] = sell.GetValueOrDefault(item.Tier) + stack.Count;
                    else if (IsGear(item))
                    {
                        gear[item.Tier] = gear.GetValueOrDefault(item.Tier) + 1;
                        gearTotal++;
                    }
                }
            }

            ItemTier[] order = { ItemTier.C, ItemTier.B, ItemTier.A, ItemTier.S, ItemTier.SS, ItemTier.SSS };
            for (int i = 1; i < order.Length; i++)
            {
                Assert.That(sell.GetValueOrDefault(order[i]), Is.LessThan(sell.GetValueOrDefault(order[i - 1])), "판매 " + order[i]);
                Assert.That(gear.GetValueOrDefault(order[i]), Is.LessThan(gear.GetValueOrDefault(order[i - 1])), "장비 " + order[i]);
            }

            // 기본 가중치 C 50%, SSS 1%에 맞게 나오는지 여유 있게 확인합니다.
            Assert.That(gear[ItemTier.C] / (float)gearTotal, Is.InRange(0.44f, 0.56f), "C 장비 비율");
            Assert.That(gear.GetValueOrDefault(ItemTier.SSS) / (float)gearTotal, Is.LessThan(0.025f), "SSS 장비 비율");
        }

        [Test]
        public void EmptyCatalogRollsOnlyExtras()
        {
            spawner.SetCatalog(null);
            for (int seed = 0; seed < 100; seed++)
                Assert.That(spawner.RollContents(seed).All(stack => spawner.Extras.Any(extra => extra.ItemId == stack.ItemId)), Is.True);
        }

        private static bool IsSell(ItemDefinition item) => item.EquipmentType == EquipmentType.None && item.Tier != ItemTier.None;

        private static bool IsGear(ItemDefinition item) => item.EquipmentType != EquipmentType.None && item.Tier != ItemTier.None &&
            (item.ItemId.StartsWith("gear-") || item.ItemId.StartsWith("weapon-"));

        private static string Describe(LootStack[] stacks) => string.Join(",", stacks.Select(stack => $"{stack.ItemId}x{stack.Count}"));
    }
}
