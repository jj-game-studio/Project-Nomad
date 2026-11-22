using System;
using Nomad.Inventory;
using Nomad.Loot;
using NUnit.Framework;

namespace Nomad.Tests
{
    public sealed class LootChestTests : PlayModeFixture
    {
        private PlayerInventory inventory;
        private LootChest chest;

        [SetUp]
        public void SetUp()
        {
            var player = Inactive("Player");
            inventory = AddInventory(player, Item("potion", 5), Item("ore", 99));
            player.SetActive(true);

            var chestObject = Inactive("Chest");
            chest = chestObject.AddComponent<LootChest>();
            chestObject.SetActive(true);
        }

        private void Fill(params LootStack[] stacks) => chest.Initialize("chest-1", stacks);

        private int Owned(string id)
        {
            int count = 0;
            for (int index = 0; index < inventory.UsedSlots; index++)
            {
                if (inventory.GetStack(index).Item.ItemId == id)
                    count += inventory.GetStack(index).Count;
            }

            return count;
        }

        [Test]
        public void TakeAllMovesEveryStackAndEmptiesChest()
        {
            Fill(new LootStack("potion", 3), new LootStack("ore", 10));

            Assert.That(chest.TryLoot(inventory), Is.EqualTo(13));
            Assert.That(Owned("potion"), Is.EqualTo(3));
            Assert.That(Owned("ore"), Is.EqualTo(10));
            Assert.That(chest.Status, Is.EqualTo(LootStatus.Empty));
            Assert.That(chest.Message, Is.EqualTo("Empty"));
        }

        [Test]
        public void FullBagLeavesItemsInChest()
        {
            inventory.Add("ore", PlayerInventory.Capacity * 99);
            Fill(new LootStack("potion", 3));

            Assert.That(chest.TryLoot(inventory), Is.Zero);
            Assert.That(chest.Status, Is.EqualTo(LootStatus.Remaining));
            Assert.That(chest.GetStack(0).Count, Is.EqualTo(3));
        }

        [Test]
        public void UnknownItemStaysInChest()
        {
            Fill(new LootStack("unknown", 2), new LootStack("potion", 1));

            Assert.That(chest.TryLoot(inventory), Is.EqualTo(1));
            Assert.That(chest.GetStack(0).Count, Is.EqualTo(2));
            Assert.That(chest.Status, Is.EqualTo(LootStatus.Remaining));
        }

        [Test]
        public void LootSingleStackOnlyTouchesThatStack()
        {
            Fill(new LootStack("potion", 3), new LootStack("ore", 10));

            Assert.That(chest.TryLootStack(inventory, 1), Is.EqualTo(10));
            Assert.That(chest.GetStack(0).Count, Is.EqualTo(3));
            Assert.That(chest.GetStack(1).Count, Is.Zero);
            Assert.That(chest.TryLootStack(inventory, 5), Is.Zero);
        }

        [Test]
        public void LootStackToSlotRespectsSlotSpace()
        {
            inventory.Add("potion", 4);
            Fill(new LootStack("potion", 3));

            // 0번 칸에는 1개만 들어갑니다.
            Assert.That(chest.TryLootStackToSlot(inventory, 0, 0), Is.EqualTo(1));
            Assert.That(chest.GetStack(0).Count, Is.EqualTo(2));
            Assert.That(chest.TryLootStackToSlot(inventory, 0, 6), Is.EqualTo(2));
            Assert.That(inventory.GetSlot(6).Count, Is.EqualTo(2));
            Assert.That(chest.Status, Is.EqualTo(LootStatus.Empty));
        }

        [Test]
        public void StoreMergesIntoChestStacksThenAppends()
        {
            inventory.Add("potion", 4);
            Fill(new LootStack("potion", 3));

            Assert.That(chest.TryStoreStack(inventory, 0), Is.EqualTo(4));
            Assert.That(inventory.UsedSlots, Is.Zero);
            Assert.That(chest.StackCount, Is.EqualTo(2));
            Assert.That(chest.GetStack(0).Count, Is.EqualTo(5));
            Assert.That(chest.GetStack(1).Count, Is.EqualTo(2));
        }

        [Test]
        public void StorePartialCountKeepsRestInBag()
        {
            inventory.Add("ore", 10);
            Fill();

            Assert.That(chest.TryStoreStack(inventory, 0, 4), Is.EqualTo(4));
            Assert.That(inventory.GetSlot(0).Count, Is.EqualTo(6));
            Assert.That(chest.GetStack(0).Count, Is.EqualTo(4));
            Assert.That(chest.TryStoreStack(inventory, 3), Is.Zero);
        }

        [Test]
        public void InitializeValidatesInput()
        {
            Assert.Throws<ArgumentException>(() => chest.Initialize(" ", Array.Empty<LootStack>()));
            Assert.Throws<ArgumentException>(() => chest.Initialize("chest", null));
            Assert.Throws<ArgumentException>(() => chest.Initialize("chest", new[] { new LootStack("potion", 0) }));
            Assert.Throws<ArgumentException>(() => chest.Initialize("chest", new[] { new LootStack("", 1) }));
        }

        [Test]
        public void InitializeIsRejectedAfterOpening()
        {
            Fill(new LootStack("potion", 1));
            chest.TryLoot(inventory);

            Assert.Throws<InvalidOperationException>(() => Fill(new LootStack("potion", 1)));
        }
    }
}
