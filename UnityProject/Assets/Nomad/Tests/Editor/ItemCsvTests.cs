using System;
using System.Collections.Generic;
using System.IO;
using Nomad.Inventory;
using Nomad.Inventory.Editor;
using NUnit.Framework;

namespace Nomad.Tests
{
    public sealed class ItemCsvTests
    {
        private const string Basic = "item_id,display_name,description,max_stack\n";
        private const string Equipment = "item_id,display_name,description,max_stack,equipment_type,attack_power,defense,attack_mode,attack_range,attack_interval\n";

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
    }
}
