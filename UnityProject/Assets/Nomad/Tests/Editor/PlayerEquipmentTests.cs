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
        private PlayerQuickSlots quick;
        private Health health;
        private ItemDefinition sword;
        private ItemDefinition bow;
        private ItemDefinition helmet;
        private ItemDefinition armor;
        private ItemDefinition plate;
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
            plate = Item("plate", 1, EquipmentType.Body, defense: 6);

            var owner = Inactive("Player");
            inventory = AddInventory(owner, sword, bow, helmet, armor, potion, axe, ring, plate);
            equipment = owner.AddComponent<PlayerEquipment>();
            quick = owner.AddComponent<PlayerQuickSlots>();
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
        public void AutoEquipWeaponRegistersQuickSlotsAndKeepsItInBag()
        {
            Assert.That(AutoEquip("sword"), Is.True);
            Assert.That(AutoEquip("bow"), Is.True);
            Assert.That(quick.GetItem(0), Is.SameAs(sword));
            Assert.That(quick.GetItem(1), Is.SameAs(bow));
            Assert.That(inventory.GetCount("sword"), Is.EqualTo(1), "무기는 가방에 남음");

            // 두 칸이 모두 차 있으면 현재 선택한 퀵바 무기 칸을 바꿉니다.
            Assert.That(equipment.SelectWeapon(1), Is.True);
            Assert.That(AutoEquip("axe"), Is.True);
            Assert.That(quick.GetItem(0), Is.SameAs(sword));
            Assert.That(equipment.ActiveWeapon.ItemId, Is.EqualTo("axe"));
        }

        [Test]
        public void QuickBarAloneDecidesActiveWeapon()
        {
            inventory.Add("sword", 1);
            inventory.Add("bow", 1);
            Assert.That(equipment.EquipWeapon(BagSlot("sword"), 0), Is.True);
            Assert.That(quick.Register(BagSlot("bow"), 1), Is.True);
            Assert.That(equipment.ActiveWeapon, Is.SameAs(sword));

            changes = 0;
            Assert.That(quick.TryUse(1), Is.True);
            Assert.That(equipment.ActiveWeapon, Is.SameAs(bow));
            Assert.That(changes, Is.EqualTo(1));

            // 등록 무기가 가방에서 빠지면 퀵바 등록도 빠지고 맨손이 됩니다.
            inventory.Remove("bow", 1);
            Assert.That(quick.GetItem(1), Is.Null);
            Assert.That(equipment.ActiveWeapon, Is.Null);
            Assert.That(changes, Is.EqualTo(2));
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

        [TestCase(EquipmentType.Accessory, EquipmentSlot.Accessory3, true)]
        [TestCase(EquipmentType.Legs, EquipmentSlot.Legs, true)]
        [TestCase(EquipmentType.Feet, EquipmentSlot.Head, false)]
        [TestCase(EquipmentType.Head, EquipmentSlot.Body, false)]
        [TestCase(EquipmentType.None, EquipmentSlot.Head, false)]
        [TestCase(EquipmentType.Weapon, EquipmentSlot.Accessory1, false)]
        [TestCase(EquipmentType.Weapon, EquipmentSlot.Head, false)]
        public void FitsMatchesTypeToSlot(EquipmentType type, EquipmentSlot slot, bool expected)
        {
            Assert.That(PlayerEquipment.Fits(type, slot), Is.EqualTo(expected));
        }

        [Test]
        public void EquipMovesItemOutOfInventory()
        {
            Assert.That(EquipFromBag("helmet", EquipmentSlot.Head), Is.True);

            Assert.That(equipment.GetItem(EquipmentSlot.Head), Is.SameAs(helmet));
            Assert.That(inventory.UsedSlots, Is.Zero);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void EquipRejectsWrongSlotAndStackableItem()
        {
            Assert.That(EquipFromBag("helmet", EquipmentSlot.Body), Is.False);
            Assert.That(EquipFromBag("potion", EquipmentSlot.Head), Is.False);

            Assert.That(inventory.UsedSlots, Is.EqualTo(2));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void EquipSwapsPreviousItemBackIntoSameBagSlot()
        {
            EquipFromBag("armor", EquipmentSlot.Body);

            Assert.That(EquipFromBag("plate", EquipmentSlot.Body), Is.True);
            Assert.That(equipment.GetItem(EquipmentSlot.Body), Is.SameAs(plate));
            Assert.That(inventory.GetSlot(0).Item, Is.SameAs(armor));
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
        public void SelectWeaponOnlyAcceptsQuickWeaponSlots()
        {
            Assert.That(equipment.SelectWeapon(1), Is.True);
            Assert.That(equipment.ActiveWeaponIndex, Is.EqualTo(1));
            Assert.That(equipment.SelectWeapon(2), Is.False);
            Assert.That(equipment.SelectWeapon(-1), Is.False);
            Assert.That(equipment.ActiveWeaponIndex, Is.EqualTo(1));
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
