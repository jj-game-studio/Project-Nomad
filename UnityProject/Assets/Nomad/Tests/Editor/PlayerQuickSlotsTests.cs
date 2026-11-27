using Nomad.Inventory;
using Nomad.Combat;
using Nomad.Consumables;
using Nomad.Equipment;
using NUnit.Framework;
using UnityEngine;

namespace Nomad.Tests
{
    public sealed class PlayerQuickSlotsTests : PlayModeFixture
    {
        private PlayerInventory inventory;
        private PlayerQuickSlots quickSlots;
        private ItemDefinition potion;
        private PlayerEquipment equipment;
        private Health health;
        private ItemDefinition sword;
        private ItemDefinition bow;

        [SetUp]
        public void SetUp()
        {
            // 소모품 칸은 전투 중 사용 가능한 회복약만 받으므로 회복약 ID로 만듭니다.
            potion = Item(PlayerHealing.PotionId, 5);
            ItemDefinition ore = Item("ore", 99);
            sword = Item("sword", 1, EquipmentType.Weapon);
            bow = Item("bow", 1, EquipmentType.Weapon);

            var owner = Inactive("Player");
            inventory = AddInventory(owner, potion, ore, sword, bow);
            equipment = owner.AddComponent<PlayerEquipment>();
            health = owner.AddComponent<Health>();
            owner.AddComponent<PlayerHealing>();
            quickSlots = owner.AddComponent<PlayerQuickSlots>();
            owner.SetActive(true);
        }

        [Test]
        public void RegisterCountsItemAcrossAllStacks()
        {
            inventory.Add(PlayerHealing.PotionId, 7);

            Assert.That(quickSlots.Register(0, 2), Is.True);
            Assert.That(quickSlots.GetItem(2), Is.SameAs(potion));
            Assert.That(quickSlots.GetCount(2), Is.EqualTo(7));
        }

        [Test]
        public void RegisteringSameItemAgainMovesIt()
        {
            inventory.Add(PlayerHealing.PotionId, 1);
            quickSlots.Register(0, 2);

            Assert.That(quickSlots.Register(0, 5), Is.True);
            Assert.That(quickSlots.GetItem(2), Is.Null);
            Assert.That(quickSlots.GetItem(5), Is.SameAs(potion));
        }

        [TestCase(0, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, PlayerQuickSlots.Capacity)]
        public void RegisterRejectsEmptyOrInvalidSlot(int inventorySlot, int quickSlot)
        {
            Assert.That(quickSlots.Register(inventorySlot, quickSlot), Is.False);
        }

        [Test]
        public void RegistrationFollowsItemWhenBagSlotMoves()
        {
            inventory.Add(PlayerHealing.PotionId, 2);
            quickSlots.Register(0, 3);

            inventory.MoveStack(0, 9);
            Assert.That(quickSlots.GetItem(3), Is.SameAs(potion));
            Assert.That(quickSlots.GetCount(3), Is.EqualTo(2));
        }

        [Test]
        public void RegistrationClearsWhenItemRunsOut()
        {
            inventory.Add(PlayerHealing.PotionId, 2);
            quickSlots.Register(0, 3);

            inventory.Remove(PlayerHealing.PotionId, 2);
            Assert.That(quickSlots.GetItem(3), Is.Null);
        }

        [Test]
        public void UnregisterClearsSlotOnce()
        {
            inventory.Add(PlayerHealing.PotionId, 1);
            quickSlots.Register(0, 2);

            Assert.That(quickSlots.Unregister(2), Is.True);
            Assert.That(quickSlots.Unregister(2), Is.False);
        }

        [Test]
        public void MoveToEmptySlotAndSwapPreserveBothItemsAndCounts()
        {
            inventory.Add(PlayerHealing.PotionId, 2);
            inventory.Add("sword", 1);
            quickSlots.Register(0, 2);
            quickSlots.Register(1, 0);
            int changes = 0;
            quickSlots.Changed += () => changes++;

            Assert.That(quickSlots.Move(2, 3), Is.True);
            Assert.That(quickSlots.GetItem(2), Is.Null);
            Assert.That(quickSlots.GetItem(3), Is.SameAs(potion));
            Assert.That(quickSlots.GetCount(3), Is.EqualTo(2));
            Assert.That(quickSlots.Move(0, 1), Is.True);
            Assert.That(quickSlots.GetItem(0), Is.Null);
            Assert.That(quickSlots.GetItem(1), Is.SameAs(sword));
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(inventory.GetCount(PlayerHealing.PotionId), Is.EqualTo(2));
            Assert.That(quickSlots.TryUse(1), Is.True, "교환된 번호로 장착");
            Assert.That(equipment.ActiveWeapon, Is.SameAs(sword));
        }

        [TestCase(-1, 2)]
        [TestCase(2, PlayerQuickSlots.Capacity)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        [TestCase(2, 0)]
        public void MoveRejectsInvalidEmptySameOrWrongKindSlot(int source, int target)
        {
            inventory.Add(PlayerHealing.PotionId, 1);
            quickSlots.Register(0, 2);
            Assert.That(quickSlots.Move(source, target), Is.False);
            Assert.That(quickSlots.GetItem(2), Is.SameAs(potion));
        }

        [Test]
        public void WeaponSlotsAcceptOnlyWeaponsAndOtherSlotsOnlyCombatConsumables()
        {
            inventory.Add(PlayerHealing.PotionId, 1);
            inventory.Add("sword", 1);
            inventory.Add("ore", 1);

            for (int quick = 0; quick < PlayerQuickSlots.WeaponSlotCount; quick++)
                Assert.That(quickSlots.Register(0, quick), Is.False, "무기 칸에 포션 금지");
            for (int quick = PlayerQuickSlots.WeaponSlotCount; quick < PlayerQuickSlots.Capacity; quick++)
            {
                Assert.That(quickSlots.Register(1, quick), Is.False, "소모품 칸에 무기 금지");
                Assert.That(quickSlots.Register(2, quick), Is.False, "사용 불가 재료 금지");
            }

            Assert.That(quickSlots.Register(1, 1), Is.True);
            Assert.That(quickSlots.Register(0, 5), Is.True);
            Assert.That(quickSlots.Move(1, 5), Is.False, "무기와 포션 교환 금지");
            Assert.That(quickSlots.GetItem(1), Is.SameAs(sword));
            Assert.That(quickSlots.GetItem(5), Is.SameAs(potion));
        }

        [Test]
        public void UseEquipsAndSelectsWeaponAndKeepsRegistration()
        {
            inventory.Add("sword", 1);
            quickSlots.Register(0, 0);
            Assert.That(quickSlots.TryUse(0), Is.True);
            Assert.That(equipment.ActiveWeapon, Is.SameAs(sword));
            Assert.That(quickSlots.GetItem(0), Is.SameAs(sword));
            Assert.That(quickSlots.GetCount(0), Is.EqualTo(1));

            inventory.Add("bow", 1);
            quickSlots.Register(0, 1);
            Assert.That(quickSlots.TryUse(1), Is.True);
            Assert.That(equipment.ActiveWeapon, Is.SameAs(bow));
            Assert.That(quickSlots.TryUse(0), Is.True);
            Assert.That(equipment.ActiveWeapon, Is.SameAs(sword));
            Assert.That(equipment.TryUnequip(EquipmentSlot.Weapon1), Is.True);
            Assert.That(quickSlots.GetItem(0), Is.SameAs(sword));
            Assert.That(quickSlots.TryUse(0), Is.True);
        }

        [Test]
        public void UsePotionRespectsFullHealthCooldownAndDeath()
        {
            inventory.Add(PlayerHealing.PotionId, 2);
            quickSlots.Register(0, 2);
            Assert.That(quickSlots.TryUse(2), Is.False, "만피에는 소모하지 않음");
            health.TryApplyDamage(60);
            Assert.That(quickSlots.TryUse(2), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(80));
            Assert.That(quickSlots.GetCount(2), Is.EqualTo(1));
            Assert.That(quickSlots.TryUse(2), Is.False, "연속 입력은 재사용 대기 적용");
            health.TryApplyDamage(1000);
            Assert.That(quickSlots.TryUse(2), Is.False);
            Assert.That(inventory.GetCount(PlayerHealing.PotionId), Is.EqualTo(1));
        }

        [Test]
        public void UseRejectsPauseAndUnsupportedItems()
        {
            inventory.Add("ore", 1);
            Assert.That(quickSlots.Register(0, 2), Is.False);
            Assert.That(quickSlots.TryUse(2), Is.False);
            inventory.Add("sword", 1);
            quickSlots.Register(1, 1);
            float previous = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                Assert.That(quickSlots.TryUse(1), Is.False);
            }
            finally { Time.timeScale = previous; }
            Assert.That(equipment.ActiveWeapon, Is.Null);
        }
    }
}
