using Nomad.Inventory;
using NUnit.Framework;

namespace Nomad.Tests
{
    public sealed class PlayerQuickSlotsTests : PlayModeFixture
    {
        private PlayerInventory inventory;
        private PlayerQuickSlots quickSlots;
        private ItemDefinition potion;

        [SetUp]
        public void SetUp()
        {
            potion = Item("potion", 5);
            ItemDefinition ore = Item("ore", 99);

            var owner = Inactive("Player");
            inventory = AddInventory(owner, potion, ore);
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
    }
}
