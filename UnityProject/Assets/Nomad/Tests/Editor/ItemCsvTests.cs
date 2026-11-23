using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nomad.Equipment.Editor;
using Nomad.Inventory;
using Nomad.Inventory.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nomad.Tests
{
    public sealed class ItemCsvTests
    {
        private const string Basic = "item_id,display_name,description,max_stack\n";
        private const string Equipment = "item_id,display_name,description,max_stack,equipment_type,attack_power,defense,attack_mode,attack_range,attack_interval\n";
        private static readonly string Weapon = Equipment.TrimEnd() + ",mana_cost\n";

        [Test]
        public void ParsesBomQuotesCommasAndNewlines()
        {
            var rows = ItemCsv.Parse("﻿" + Basic + "herb,약초,\"쉼표, \"\"인용\"\"\n둘째 줄\",20\r\nore,광석,,99");

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].Id, Is.EqualTo("herb"));
            Assert.That(rows[0].Description, Is.EqualTo("쉼표, \"인용\"\n둘째 줄"));
            Assert.That(rows[0].MaxStack, Is.EqualTo(20));
            Assert.That(rows[0].Equipment, Is.Null);
            Assert.That(rows[1].Description, Is.Empty);
        }

        [Test]
        public void ParsesEquipmentColumns()
        {
            var rows = ItemCsv.Parse(Equipment +
                "bow,활,,1,Weapon,12,0,Ranged,8.5,0.75\n" +
                "cap,모자,,1,Head,0,3,None,0,0\n" +
                "herb,약초,,20,None,0,0,None,0,0");

            Assert.That(rows[0].Equipment, Is.EqualTo(EquipmentType.Weapon));
            Assert.That(rows[0].AttackPower, Is.EqualTo(12));
            Assert.That(rows[0].AttackMode, Is.EqualTo(WeaponAttackMode.Ranged));
            Assert.That(rows[0].AttackRange, Is.EqualTo(8.5f));
            Assert.That(rows[0].AttackInterval, Is.EqualTo(0.75f));
            Assert.That(rows[1].Defense, Is.EqualTo(3));
            Assert.That(rows[2].Equipment, Is.EqualTo(EquipmentType.None));
        }

        private static IEnumerable<string> InvalidCsv()
        {
            // 헤더·수량·ID·인용부호 규칙
            yield return Basic;
            yield return "bad\na,검,,1";
            yield return Basic + "a,검,,0";
            yield return Basic + "a,검,,2147483648";
            yield return Basic + "../a,검,,1";
            yield return Basic + "a,,,1";
            yield return Basic + "a,검,,1\nA,검,,1";
            yield return Basic + "a,\"검,,1";
            yield return Basic + "a,검\",,1";
            yield return Basic + "a,\"검\"x,,1";
            yield return Basic + "a,검,1";
            yield return Basic + "a,검,,1,extra";

            // 장비 분류·수치 규칙
            yield return Equipment + "a,검,,2,Weapon,5,0,Melee,1,1";
            yield return Equipment + "a,검,,1,Weapon,0,0,Melee,1,1";
            yield return Equipment + "a,검,,1,Weapon,5,1,Melee,1,1";
            yield return Equipment + "a,검,,1,Weapon,5,0,None,1,1";
            yield return Equipment + "a,검,,1,Weapon,5,0,Melee,0,1";
            yield return Equipment + "a,모자,,1,Head,2,1,None,0,0";
            yield return Equipment + "a,모자,,1,Head,0,1,Melee,0,0";
            yield return Equipment + "a,약초,,5,None,0,3,None,0,0";
            yield return Equipment + "a,검,,1,weapon,5,0,Melee,1,1";
            yield return Equipment + "a,검,,1,Sword,5,0,Melee,1,1";
            yield return Equipment + "a,지팡이,,1,Weapon,5,0,Staff,1,1";
            yield return Weapon + "a,지팡이,,1,Weapon,5,0,Staff,1,1,0";
            yield return Weapon + "a,활,,1,Weapon,5,0,Bow,1,1,1";
        }

        [TestCaseSource(nameof(InvalidCsv))]
        public void RejectsInvalidCsv(string text)
        {
            Assert.Throws<FormatException>(() => ItemCsv.Parse(text));
        }

        [TestCase("Assets/Nomad/Equipment/equipment.csv")]
        [TestCase("Assets/Nomad/Inventory/Data/items-example.csv")]
        public void ProjectCsvFilesAreValid(string path)
        {
            var rows = ItemCsv.Parse(File.ReadAllText(path));

            Assert.That(rows, Is.Not.Empty);
        }

        [Test]
        public void WeaponExamplesMatchTheirImportedAssets()
        {
            var rows = ItemCsv.Parse(File.ReadAllText(EquipmentAuthoring.CsvPath));
            var weapons = rows.Where(row => row.Equipment == EquipmentType.Weapon && row.Id != "demo-unarmed").ToArray();
            foreach (WeaponAttackMode mode in new[] { WeaponAttackMode.Melee, WeaponAttackMode.Bow, WeaponAttackMode.Staff })
                Assert.That(weapons.Count(row => row.AttackMode == mode), Is.EqualTo(6), mode.ToString());

            foreach (ItemCsv.Row weapon in weapons)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{WeaponContentAuthoring.ItemFolder}/{weapon.Id}.asset");
                Assert.That(asset, Is.Not.Null, weapon.Id);
                Assert.That(asset.AttackMode, Is.EqualTo(weapon.AttackMode));
                Assert.That(asset.AttackPower, Is.EqualTo(weapon.AttackPower));
                Assert.That(asset.AttackRange, Is.EqualTo(weapon.AttackRange));
                Assert.That(asset.AttackInterval, Is.EqualTo(weapon.AttackInterval));
                Assert.That(asset.ManaCost, Is.EqualTo(weapon.ManaCost));
                Assert.That(asset.EquippedVisual, Is.Not.Null);
                Assert.That(asset.EquippedVisual.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            }

            // 기본 근접 3종(C~A)은 사거리·공격력·간격이 함께 커집니다.
            var melee = weapons.Where(row => row.AttackMode == WeaponAttackMode.Melee && row.Tier <= ItemTier.A)
                .OrderBy(row => row.AttackRange).ToArray();
            Assert.That(melee.Select(row => row.Id), Is.EqualTo(new[] { "weapon-dagger", "demo-sword", "weapon-greatsword" }));
            Assert.That(melee.Select(row => row.AttackPower), Is.Ordered);
            Assert.That(melee.Select(row => row.AttackInterval), Is.Ordered);
        }

        [Test]
        public void GearRowsCoverEverySlotAndMatchTheirAssets()
        {
            var gear = ItemCsv.Parse(File.ReadAllText(EquipmentAuthoring.CsvPath))
                .Where(row => row.Id.StartsWith("gear-", StringComparison.Ordinal)).ToArray();
            foreach (EquipmentType type in new[] { EquipmentType.Head, EquipmentType.Body, EquipmentType.Legs,
                         EquipmentType.Feet, EquipmentType.Bag, EquipmentType.Accessory })
                Assert.That(gear.Any(row => row.Equipment == type), Is.True, type.ToString());

            foreach (ItemCsv.Row row in gear)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{WeaponContentAuthoring.ItemFolder}/{row.Id}.asset");
                Assert.That(asset, Is.Not.Null, row.Id);
                Assert.That(asset.Icon, Is.Not.Null, row.Id);
                Assert.That(asset.Stats, Is.EqualTo(row.Stats), row.Id);
                Assert.That(asset.Tier, Is.EqualTo(row.Tier), row.Id);
            }
        }

        [Test]
        public void WeaponsAndGearHaveTiersAndArmorStatsGrowWithTier()
        {
            var rows = ItemCsv.Parse(File.ReadAllText(EquipmentAuthoring.CsvPath))
                .Where(row => row.Equipment.HasValue && row.Equipment != EquipmentType.None && row.Id != "demo-unarmed").ToArray();
            foreach (ItemCsv.Row row in rows)
                Assert.That(row.Tier, Is.Not.Null.And.Not.EqualTo(ItemTier.None), row.Id);

            // 같은 부위 방어구는 등급마다 하나씩 있고, 등급이 높을수록 주 스탯이 큽니다.
            foreach (EquipmentType type in new[] { EquipmentType.Head, EquipmentType.Body, EquipmentType.Legs, EquipmentType.Feet })
            {
                var slot = rows.Where(row => row.Equipment == type).OrderBy(row => row.Tier).ToArray();
                Assert.That(slot.Select(row => row.Tier), Is.EquivalentTo(Enum.GetValues(typeof(ItemTier)).Cast<ItemTier?>().Skip(1)), type.ToString());
                Assert.That(slot.Select(row => row.Stats[0].Value), Is.Ordered.Ascending, type.ToString());
                Assert.That(slot.Select(row => row.Defense), Is.Ordered.Ascending, type.ToString());
            }

            Assert.That(rows.Count(row => row.Equipment == EquipmentType.Bag), Is.GreaterThanOrEqualTo(3));
            Assert.That(rows.Count(row => row.Equipment == EquipmentType.Accessory), Is.InRange(10, 20));
        }

        [Test]
        public void TierColumnAcceptsOnlyKnownTiers()
        {
            const string header = "item_id,display_name,description,max_stack,equipment_type,attack_power,defense,attack_mode,attack_range,attack_interval,mana_cost,stats,tier\n";
            Assert.That(ItemCsv.Parse(header + "ring,반지,,1,Accessory,0,0,None,0,0,0,,SS")[0].Tier, Is.EqualTo(ItemTier.SS));
            Assert.That(ItemCsv.Parse(header + "ore,광석,,99,None,0,0,None,0,0,0,,")[0].Tier, Is.EqualTo(ItemTier.None));
            foreach (string tier in new[] { "None", "D", "s", "SSSS" })
                Assert.Throws<FormatException>(() => ItemCsv.Parse(header + "ring,반지,,1,Accessory,0,0,None,0,0,0,," + tier), tier);
        }

        [Test]
        public void StatsColumnRejectsUnknownOrDuplicateStats()
        {
            const string header = "item_id,display_name,description,max_stack,equipment_type,attack_power,defense,attack_mode,attack_range,attack_interval,mana_cost,stats\n";
            var row = ItemCsv.Parse(header + "ring,반지,,1,Accessory,0,0,None,0,0,0,CritChance=8;MaxMana=20")[0];
            Assert.That(row.Stats, Is.EqualTo(new[] { new StatModifier(StatType.CritChance, 8), new StatModifier(StatType.MaxMana, 20) }));

            foreach (string stats in new[] { "Luck=1", "CritChance=8;CritChance=1", "Attack=3", "CritChance" })
                Assert.Throws<FormatException>(() => ItemCsv.Parse(header + "ring,반지,,1,Accessory,0,0,None,0,0,0," + stats), stats);
        }
    }
}
