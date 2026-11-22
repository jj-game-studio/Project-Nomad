using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using NUnit.Framework;

namespace Nomad.Tests
{
    public sealed class PlayerEquipmentTests : PlayModeFixture
    {
        private PlayerInventory inventory;
        private PlayerEquipment equipment;
        private Health health;
        private ItemDefinition sword;
        private ItemDefinition bow;
        private ItemDefinition helmet;
        private ItemDefinition armor;
        private int changes;

        [SetUp]
        public void SetUp()
        {
            sword = Item("sword", 1, EquipmentType.Weapon, attack: 10);
            bow = Item("bow", 1, EquipmentType.Weapon, attack: 8);
            helmet = Item("helmet", 1, EquipmentType.Head, defense: 3);
            armor = Item("armor", 1, EquipmentType.Body, defense: 4);
            ItemDefinition potion = Item("potion", 5);

            var owner = Inactive("Player");
            inventory = AddInventory(owner, sword, bow, helmet, armor, potion);
            equipment = owner.AddComponent<PlayerEquipment>();
            health = owner.AddComponent<Health>();
            owner.SetActive(true);

            changes = 0;
            equipment.Changed += () => changes++;
        }

        private bool EquipFromBag(string id, EquipmentSlot slot)
        {
            inventory.Add(id, 1);
            for (int index = 0; index < PlayerInventory.Capacity; index++)
            {
                if (inventory.GetSlot(index).Item?.ItemId == id)
                    return equipment.TryEquip(index, slot);
            }

            return false;
        }

        [TestCase(EquipmentType.Weapon, EquipmentSlot.Weapon1, true)]
        [TestCase(EquipmentType.Weapon, EquipmentSlot.Weapon2, true)]
        [TestCase(EquipmentType.Totem, EquipmentSlot.Totem2, true)]
        [TestCase(EquipmentType.Head, EquipmentSlot.Body, false)]
        [TestCase(EquipmentType.None, EquipmentSlot.Weapon1, false)]
        [TestCase(EquipmentType.Weapon, EquipmentSlot.Ring, false)]
        public void FitsMatchesTypeToSlot(EquipmentType type, EquipmentSlot slot, bool expected)
        {
            Assert.That(PlayerEquipment.Fits(type, slot), Is.EqualTo(expected));
        }

        [Test]
        public void EquipMovesItemOutOfInventory()
        {
            Assert.That(EquipFromBag("sword", EquipmentSlot.Weapon1), Is.True);

            Assert.That(equipment.GetItem(EquipmentSlot.Weapon1), Is.SameAs(sword));
            Assert.That(equipment.ActiveWeapon, Is.SameAs(sword));
            Assert.That(inventory.UsedSlots, Is.Zero);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void EquipRejectsWrongSlotAndStackableItem()
        {
            Assert.That(EquipFromBag("helmet", EquipmentSlot.Body), Is.False);
            Assert.That(EquipFromBag("potion", EquipmentSlot.Weapon1), Is.False);

            Assert.That(inventory.UsedSlots, Is.EqualTo(2));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void EquipSwapsPreviousItemBackIntoSameBagSlot()
        {
            EquipFromBag("sword", EquipmentSlot.Weapon1);

            Assert.That(EquipFromBag("bow", EquipmentSlot.Weapon1), Is.True);
            Assert.That(equipment.GetItem(EquipmentSlot.Weapon1), Is.SameAs(bow));
            Assert.That(inventory.GetSlot(0).Item, Is.SameAs(sword));
        }

        [Test]
        public void UnequipReturnsItemToFirstEmptyBagSlot()
        {
            EquipFromBag("helmet", EquipmentSlot.Head);
            inventory.Add("potion", 1);

            Assert.That(equipment.TryUnequip(EquipmentSlot.Head), Is.True);
            Assert.That(equipment.GetItem(EquipmentSlot.Head), Is.Null);
            Assert.That(inventory.GetSlot(1).Item, Is.SameAs(helmet));
        }

        [Test]
        public void UnequipFailsWhenBagIsFull()
        {
            EquipFromBag("helmet", EquipmentSlot.Head);
            inventory.Add("potion", PlayerInventory.Capacity * 5);

            Assert.That(equipment.TryUnequip(EquipmentSlot.Head), Is.False);
            Assert.That(equipment.GetItem(EquipmentSlot.Head), Is.SameAs(helmet));
        }

        [Test]
        public void SelectWeaponOnlyAcceptsWeaponSlots()
        {
            Assert.That(equipment.SelectWeapon(EquipmentSlot.Weapon2), Is.True);
            Assert.That(equipment.ActiveWeaponSlot, Is.EqualTo(EquipmentSlot.Weapon2));
            Assert.That(equipment.SelectWeapon(EquipmentSlot.Head), Is.False);
            Assert.That(equipment.ActiveWeaponSlot, Is.EqualTo(EquipmentSlot.Weapon2));
        }

        [Test]
        public void DefenseReducesCombatDamageToAtLeastOne()
        {
            EquipFromBag("helmet", EquipmentSlot.Head);
            EquipFromBag("armor", EquipmentSlot.Body);

            Assert.That(equipment.TotalDefense, Is.EqualTo(7));
            Assert.That(equipment.ModifyCombatDamage(10), Is.EqualTo(3));
            Assert.That(equipment.ModifyCombatDamage(5), Is.EqualTo(1));
        }

        [Test]
        public void HealthUsesDefenseOnlyForCombatDamage()
        {
            EquipFromBag("armor", EquipmentSlot.Body);

            health.TryApplyDamage(10);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 6));

            health.TryApplyDamage(10, DamageKind.Fall);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 16));
        }
    }
}
