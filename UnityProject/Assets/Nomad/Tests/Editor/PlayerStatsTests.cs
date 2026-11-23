using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Player;
using NUnit.Framework;

namespace Nomad.Tests
{
    public sealed class PlayerStatsTests : PlayModeFixture
    {
        private PlayerInventory inventory;
        private PlayerEquipment equipment;
        private PlayerStats stats;
        private Health health;
        private PlayerMana mana;
        private ItemDefinition sword;

        [SetUp]
        public void SetUp()
        {
            sword = Item("sword", 1, EquipmentType.Weapon, attack: 10);
            ItemDefinition potion = Item("potion", 5);
            ItemDefinition bag = Gear("bag", EquipmentType.Bag, 0, new StatModifier(StatType.InventorySlots, 5));
            ItemDefinition armor = Gear("armor", EquipmentType.Body, 4, new StatModifier(StatType.MaxHealth, 30));
            ItemDefinition pants = Gear("pants", EquipmentType.Legs, 1, new StatModifier(StatType.Evasion, 10));
            ItemDefinition boots = Gear("boots", EquipmentType.Feet, 1, new StatModifier(StatType.MoveSpeed, 20));
            ItemDefinition pendant = Gear("pendant", EquipmentType.Accessory, 0, new StatModifier(StatType.MaxMana, 30));
            ItemDefinition earring = Gear("earring", EquipmentType.Accessory, 0, new StatModifier(StatType.CritChance, 100));

            var owner = Inactive("Player");
            inventory = AddInventory(owner, sword, potion, bag, armor, pants, boots, pendant, earring);
            health = owner.AddComponent<Health>();
            mana = owner.AddComponent<PlayerMana>();
            owner.AddComponent<UnityEngine.CharacterController>();
            owner.AddComponent<PlayerMovement>();
            equipment = owner.AddComponent<PlayerEquipment>();
            stats = owner.AddComponent<PlayerStats>();
            owner.SetActive(true);
            stats.RandomSource = () => 0.99f;
        }

        private ItemDefinition Gear(string id, EquipmentType type, int defense, params StatModifier[] modifiers)
        {
            ItemDefinition item = Item(id, 1, type, defense: defense);
            TestKit.SetField(item, "stats", modifiers);
            return item;
        }

        private bool Equip(string id, EquipmentSlot slot)
        {
            inventory.Add(id, 1);
            for (int index = 0; index < PlayerInventory.MaxCapacity; index++)
                if (inventory.GetSlot(index).Item?.ItemId == id)
                    return equipment.TryEquip(index, slot);
            return false;
        }

        [Test]
        public void BagExpandsInventoryAndBlocksUnequipWhileExtraSlotsAreUsed()
        {
            Assert.That(Equip("bag", EquipmentSlot.Bag), Is.True);
            Assert.That(inventory.SlotCount, Is.EqualTo(PlayerInventory.Capacity + 5));
            Assert.That(stats.Get(StatType.InventorySlots), Is.EqualTo(25));

            inventory.Add("potion", 25 * 5);
            Assert.That(inventory.UsedSlots, Is.EqualTo(25));
            Assert.That(equipment.TryUnequip(EquipmentSlot.Bag), Is.False);

            inventory.Remove("potion", 25 * 5);
            Assert.That(equipment.TryUnequip(EquipmentSlot.Bag), Is.True);
            Assert.That(inventory.SlotCount, Is.EqualTo(PlayerInventory.Capacity));
        }

        [Test]
        public void EquipmentAppliesMaximumsAndMoveSpeed()
        {
            Equip("armor", EquipmentSlot.Body);
            Equip("pendant", EquipmentSlot.Accessory2);
            Equip("boots", EquipmentSlot.Feet);

            Assert.That(health.MaxHealth, Is.EqualTo(130));
            Assert.That(health.CurrentHealth, Is.EqualTo(100));
            Assert.That(mana.Maximum, Is.EqualTo(130));
            Assert.That(stats.Get(StatType.MoveSpeed), Is.EqualTo(120));
            Assert.That(stats.GetComponent<PlayerMovement>().StatSpeedMultiplier, Is.EqualTo(1.2f).Within(0.001f));

            equipment.TryUnequip(EquipmentSlot.Body);
            Assert.That(health.MaxHealth, Is.EqualTo(100));
        }

        [Test]
        public void EvasionCancelsDamageAndDefenseAppliesOtherwise()
        {
            Equip("armor", EquipmentSlot.Body);
            Equip("pants", EquipmentSlot.Legs);
            Assert.That(stats.Get(StatType.Evasion), Is.EqualTo(10));

            stats.RandomSource = () => 0.05f;
            Assert.That(health.TryApplyDamage(20), Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(100));

            stats.RandomSource = () => 0.99f;
            Assert.That(health.TryApplyDamage(20), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(85));
        }

        [Test]
        public void CriticalChanceRaisesOutgoingDamage()
        {
            Assert.That(stats.RollOutgoingDamage(sword, out bool normal), Is.EqualTo(10));
            Assert.That(normal, Is.False);

            Equip("earring", EquipmentSlot.Accessory1);
            Assert.That(stats.RollOutgoingDamage(sword, out bool critical), Is.EqualTo(15));
            Assert.That(critical, Is.True);
        }
    }
}
