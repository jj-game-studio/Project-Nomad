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
            ItemDefinition axe = Item("axe", 1, EquipmentType.Weapon, attack: 12);
            ItemDefinition ring = Item("ring", 1, EquipmentType.Accessory);

            var owner = Inactive("Player");
            inventory = AddInventory(owner, sword, bow, helmet, armor, potion, axe, ring);
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

        private int BagSlot(string id)
        {
            for (int index = 0; index < inventory.SlotCount; index++)
                if (inventory.GetSlot(index).Item?.ItemId == id)
                    return index;
            return -1;
        }

        // 더블클릭 장착: 가방에 넣은 뒤 자동으로 알맞은 장비칸을 고릅니다.
        private bool AutoEquip(string id)
        {
            inventory.Add(id, 1);
            return equipment.TryEquipAuto(BagSlot(id));
        }

        [Test]
        public void AutoEquipWeaponFillsEmptySlotsThenReplacesActiveWeapon()
        {
            Assert.That(AutoEquip("sword"), Is.True);
            Assert.That(AutoEquip("bow"), Is.True);
            Assert.That(equipment.GetItem(EquipmentSlot.Weapon1), Is.SameAs(sword));
            Assert.That(equipment.GetItem(EquipmentSlot.Weapon2), Is.SameAs(bow));

            // 두 칸이 모두 차 있으면 현재 선택한 무기칸과 교체하고, 이전 무기는 같은 가방 칸으로 돌아옵니다.
            equipment.SelectWeapon(EquipmentSlot.Weapon2);
            inventory.Add("axe", 1);
            int slot = BagSlot("axe");
            Assert.That(equipment.TryEquipAuto(slot), Is.True);
            Assert.That(equipment.GetItem(EquipmentSlot.Weapon1), Is.SameAs(sword));
            Assert.That(equipment.GetItem(EquipmentSlot.Weapon2).ItemId, Is.EqualTo("axe"));
            Assert.That(inventory.GetSlot(slot).Item, Is.SameAs(bow));
        }

        [Test]
        public void AutoEquipWeaponUsesSecondSlotWhenOnlyFirstIsEmpty()
        {
            EquipFromBag("bow", EquipmentSlot.Weapon2);

            Assert.That(AutoEquip("sword"), Is.True);
            Assert.That(equipment.GetItem(EquipmentSlot.Weapon1), Is.SameAs(sword));
            Assert.That(equipment.GetItem(EquipmentSlot.Weapon2), Is.SameAs(bow));
        }

        [Test]
        public void AutoEquipAccessoryFillsInOrderThenReplacesFirst()
        {
            for (int count = 0; count < 3; count++)
                Assert.That(AutoEquip("ring"), Is.True);
            Assert.That(equipment.GetItem(EquipmentSlot.Accessory1), Is.Not.Null);
            Assert.That(equipment.GetItem(EquipmentSlot.Accessory2), Is.Not.Null);
            Assert.That(equipment.GetItem(EquipmentSlot.Accessory3), Is.Not.Null);

            Assert.That(equipment.TryGetAutoSlot(EquipmentType.Accessory, out EquipmentSlot target), Is.True);
            Assert.That(target, Is.EqualTo(EquipmentSlot.Accessory1));
            Assert.That(AutoEquip("ring"), Is.True);
            Assert.That(inventory.UsedSlots, Is.EqualTo(1));

            // 가운데 칸을 비우면 다음 장신구는 그 칸으로 갑니다.
            Assert.That(equipment.TryUnequip(EquipmentSlot.Accessory2), Is.True);
            Assert.That(equipment.TryGetAutoSlot(EquipmentType.Accessory, out target), Is.True);
            Assert.That(target, Is.EqualTo(EquipmentSlot.Accessory2));
        }

        [Test]
        public void AutoEquipPutsArmorInItsSlotAndRejectsOtherItems()
        {
            Assert.That(AutoEquip("helmet"), Is.True);
            Assert.That(equipment.GetItem(EquipmentSlot.Head), Is.SameAs(helmet));

            Assert.That(AutoEquip("potion"), Is.False);
            Assert.That(equipment.TryEquipAuto(-1), Is.False);
            Assert.That(equipment.TryEquipAuto(inventory.SlotCount), Is.False);
            Assert.That(inventory.GetSlot(BagSlot("potion")).Count, Is.EqualTo(1));
        }

        [TestCase(EquipmentType.Weapon, EquipmentSlot.Weapon1, true)]
        [TestCase(EquipmentType.Weapon, EquipmentSlot.Weapon2, true)]
        [TestCase(EquipmentType.Accessory, EquipmentSlot.Accessory3, true)]
        [TestCase(EquipmentType.Legs, EquipmentSlot.Legs, true)]
        [TestCase(EquipmentType.Feet, EquipmentSlot.Head, false)]
        [TestCase(EquipmentType.Head, EquipmentSlot.Body, false)]
        [TestCase(EquipmentType.None, EquipmentSlot.Weapon1, false)]
        [TestCase(EquipmentType.Weapon, EquipmentSlot.Accessory1, false)]
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
