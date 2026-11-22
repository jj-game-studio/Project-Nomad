using Nomad.Inventory;
using NUnit.Framework;

namespace Nomad.Tests
{
    public sealed class PlayerInventoryTests : PlayModeFixture
    {
        private PlayerInventory inventory;
        private ItemDefinition potion;
        private ItemDefinition ore;
        private int changes;

        [SetUp]
        public void SetUp()
        {
            potion = Item("potion", maxStack: 5);
            ore = Item("ore", maxStack: 99);

            var owner = Inactive("Inventory");
            inventory = AddInventory(owner, potion, ore);
            owner.SetActive(true);

            changes = 0;
            inventory.Changed += () => changes++;
        }

        private int Count(int slot) => inventory.GetSlot(slot).Count;

        [Test]
        public void AddFillsExistingStackBeforeNewSlot()
        {
            Assert.That(inventory.Add("potion", 3), Is.EqualTo(3));
            Assert.That(inventory.Add("potion", 4), Is.EqualTo(4));

            Assert.That(Count(0), Is.EqualTo(5));
            Assert.That(Count(1), Is.EqualTo(2));
            Assert.That(inventory.UsedSlots, Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void AddStopsAtCapacity()
        {
            int capacity = PlayerInventory.Capacity * potion.MaxStack;

            Assert.That(inventory.Add("potion", capacity + 3), Is.EqualTo(capacity));
            Assert.That(inventory.UsedSlots, Is.EqualTo(PlayerInventory.Capacity));
            Assert.That(inventory.Add("ore", 1), Is.Zero);
        }

        [TestCase("unknown", 1)]
        [TestCase(null, 1)]
        [TestCase("potion", 0)]
        [TestCase("potion", -1)]
        public void InvalidAddChangesNothing(string id, int count)
        {
            Assert.That(inventory.Add(id, count), Is.Zero);
            Assert.That(inventory.UsedSlots, Is.Zero);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void RemoveTakesFromNewestStackFirst()
        {
            inventory.Add("potion", 7);

            Assert.That(inventory.Remove("potion", 3), Is.EqualTo(3));
            Assert.That(Count(0), Is.EqualTo(4));
            Assert.That(inventory.GetSlot(1).Item, Is.Null);
            Assert.That(inventory.UsedSlots, Is.EqualTo(1));
        }

        [Test]
        public void RemoveReturnsOnlyOwnedCount()
        {
            inventory.Add("ore", 4);

            Assert.That(inventory.Remove("ore", 10), Is.EqualTo(4));
            Assert.That(inventory.UsedSlots, Is.Zero);
            Assert.That(inventory.Remove("ore", 1), Is.Zero);
        }

        [Test]
        public void MoveStackMergesSameItemUpToMaxStack()
        {
            inventory.Add("potion", 7);
            inventory.Remove("potion", 1);

            // 0번(5개)을 1번(1개)으로 옮기면 4개만 합쳐지고 1개가 남습니다.
            Assert.That(inventory.MoveStack(0, 1), Is.True);
            Assert.That(Count(0), Is.EqualTo(1));
            Assert.That(Count(1), Is.EqualTo(5));
            Assert.That(inventory.MoveStack(0, 1), Is.False);
        }

        [Test]
        public void MoveStackSwapsSlotsButKeepsAddOrder()
        {
            inventory.Add("potion", 1);
            inventory.Add("ore", 1);

            Assert.That(inventory.MoveStack(0, 1), Is.True);
            Assert.That(inventory.GetSlot(0).Item, Is.SameAs(ore));
            Assert.That(inventory.GetSlot(1).Item, Is.SameAs(potion));
            Assert.That(inventory.GetStack(0).Item, Is.SameAs(potion));
            Assert.That(inventory.GetStack(1).Item, Is.SameAs(ore));
        }

        [Test]
        public void MoveStackIntoEmptySlotMovesWholeStack()
        {
            inventory.Add("ore", 10);

            Assert.That(inventory.MoveStack(0, 7), Is.True);
            Assert.That(inventory.GetSlot(0).Item, Is.Null);
            Assert.That(Count(7), Is.EqualTo(10));
            Assert.That(inventory.GetStack(0).Count, Is.EqualTo(10));
        }

        [TestCase(0, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, PlayerInventory.Capacity)]
        [TestCase(5, 0)]
        public void MoveStackRejectsInvalidRequests(int from, int to)
        {
            inventory.Add("ore", 1);

            Assert.That(inventory.MoveStack(from, to), Is.False);
            Assert.That(Count(0), Is.EqualTo(1));
        }

        [Test]
        public void SplitMovesAmountIntoFirstEmptySlot()
        {
            inventory.Add("potion", 5);
            changes = 0;

            Assert.That(inventory.TrySplit(0, 2, potion, 5, out int destination, out string error), Is.True, error);
            Assert.That(destination, Is.EqualTo(1));
            Assert.That(Count(0), Is.EqualTo(3));
            Assert.That(Count(1), Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(5)]
        public void SplitRejectsAmountOutsideRange(int amount)
        {
            inventory.Add("potion", 5);

            Assert.That(inventory.TrySplit(0, amount, potion, 5, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(Count(0), Is.EqualTo(5));
        }

        [Test]
        public void SplitRejectsStaleDialog()
        {
            inventory.Add("potion", 5);

            Assert.That(inventory.TrySplit(0, 1, potion, 4, out _, out _), Is.False);
            Assert.That(inventory.TrySplit(0, 1, ore, 5, out _, out _), Is.False);
            Assert.That(inventory.UsedSlots, Is.EqualTo(1));
        }

        [Test]
        public void SplitNeedsEmptySlot()
        {
            inventory.Add("potion", PlayerInventory.Capacity * potion.MaxStack);

            Assert.That(inventory.TrySplit(0, 1, potion, 5, out int destination, out _), Is.False);
            Assert.That(destination, Is.EqualTo(-1));
            Assert.That(Count(0), Is.EqualTo(5));
        }
    }
}
