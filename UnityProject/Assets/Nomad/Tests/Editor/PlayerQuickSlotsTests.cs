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
            potion = Item("potion", 5);
            ItemDefinition ore = Item("ore", 99);
            sword = Item("sword", 1, EquipmentType.Weapon);
            bow = Item("bow", 1, EquipmentType.Weapon);
            ItemDefinition healing = Item(PlayerHealing.PotionId, 99);

            var owner = Inactive("Player");
            inventory = AddInventory(owner, potion, ore, sword, bow, healing);
            equipment = owner.AddComponent<PlayerEquipment>();
            health = owner.AddComponent<Health>();
            owner.AddComponent<PlayerHealing>();
            quickSlots = owner.AddComponent<PlayerQuickSlots>();
            owner.SetActive(true);
        }

        [Test]
        public void RegisterCountsItemAcrossAllStacks()
        {
            inventory.Add("potion", 7);

            Assert.That(quickSlots.Register(0, 2), Is.True);
            Assert.That(quickSlots.GetItem(2), Is.SameAs(potion));
            Assert.That(quickSlots.GetCount(2), Is.EqualTo(7));
        }

        [Test]
        public void RegisteringSameItemAgainMovesIt()
        {
            inventory.Add("potion", 1);
            quickSlots.Register(0, 0);

            Assert.That(quickSlots.Register(0, 3), Is.True);
            Assert.That(quickSlots.GetItem(0), Is.Null);
            Assert.That(quickSlots.GetItem(3), Is.SameAs(potion));
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
            inventory.Add("potion", 2);
            quickSlots.Register(0, 1);

            inventory.MoveStack(0, 9);
            Assert.That(quickSlots.GetItem(1), Is.SameAs(potion));
            Assert.That(quickSlots.GetCount(1), Is.EqualTo(2));
        }

        [Test]
        public void RegistrationClearsWhenItemRunsOut()
        {
            inventory.Add("potion", 2);
            quickSlots.Register(0, 1);

            inventory.Remove("potion", 2);
            Assert.That(quickSlots.GetItem(1), Is.Null);
        }

        [Test]
        public void UnregisterClearsSlotOnce()
        {
            inventory.Add("potion", 1);
            quickSlots.Register(0, 0);

            Assert.That(quickSlots.Unregister(0), Is.True);
            Assert.That(quickSlots.Unregister(0), Is.False);
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
        public void UseRejectsPauseTradeAndUnsupportedItems()
        {
            inventory.Add("ore", 1);
            quickSlots.Register(0, 0);
            Assert.That(quickSlots.TryUse(0), Is.False);
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
