using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Shop;
using NUnit.Framework;

namespace Nomad.Tests
{
    public sealed class EquipmentEnhancementTests : PlayModeFixture
    {
        [Test]
        public void IdenticalMaterialIsConsumedAndEnhancementSurvivesTransfers()
        {
            ItemDefinition sword = Item("enhance-sword", 1, EquipmentType.Weapon, attack: 20);
            TestKit.SetField(sword, "attackMode", WeaponAttackMode.Melee);
            ItemDefinition other = Item("other", 1, EquipmentType.Weapon, attack: 30);
            var owner = Inactive("Enhancement player");
            PlayerInventory bag = AddInventory(owner, sword, other);
            PlayerEquipment gear = owner.AddComponent<PlayerEquipment>();
            PlayerQuickSlots quick = owner.AddComponent<PlayerQuickSlots>();
            owner.SetActive(true);
            bag.Add(sword.ItemId, 1);
            Assert.That(gear.TryEquipAuto(0), Is.True, "가방 무기를 퀵바 1번에 등록");
            Assert.That(gear.ActiveWeapon, Is.SameAs(sword));
            bag.Add(other.ItemId, 1);
            Assert.That(gear.TryEnhanceWeapon(), Is.False, "다른 무기를 재료로 소비하지 않음");
            Assert.That(bag.GetCount(other.ItemId), Is.EqualTo(1));

            bag.Add(sword.ItemId, 3);
            for (int level = 1; level <= 3; level++)
            {
                Assert.That(gear.TryEnhanceWeapon(), Is.True);
                ItemDefinition enhanced = gear.ActiveWeapon;
                Assert.That(enhanced.EnhancementLevel, Is.EqualTo(level));
                Assert.That(enhanced.BaseItem, Is.SameAs(sword));
                Assert.That(enhanced.UnlockedSkill, Is.Not.Empty);
                Assert.That(quick.GetItem(0), Is.SameAs(enhanced), "강화 후 퀵슬롯 연결 유지");
                Assert.That(enhanced.AttackPower, Is.GreaterThan(sword.AttackPower));
                Assert.That(bag.GetCount(sword.ItemId), Is.EqualTo(3 - level));
            }
            Assert.That(gear.TryEnhanceWeapon(), Is.False, "최대 +3");
            ItemDefinition upgraded = gear.ActiveWeapon;
            Assert.That(quick.Unregister(0), Is.True);
            Assert.That(gear.ActiveWeapon, Is.Null);
            int slot = -1;
            for (int index = 0; index < bag.SlotCount; index++) if (bag.GetSlot(index).Item == upgraded) slot = index;
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            int destination = slot == 10 ? 11 : 10;
            Assert.That(bag.MoveStack(slot, destination), Is.True);
            Assert.That(gear.EquipWeapon(destination, 1), Is.True);
            Assert.That(gear.ActiveWeapon, Is.SameAs(upgraded));
            Assert.That(gear.ActiveWeapon.EnhancementLevel, Is.EqualTo(3));
            Assert.That(sword.EnhancementLevel, Is.Zero, "공유 원본은 변경되지 않음");
            Assert.That(sword.AttackPower, Is.EqualTo(20));
        }

        [Test]
        public void ArmorEnhancesStatsButNotInventoryCapacity()
        {
            ItemDefinition armor = Item("enhance-armor", 1, EquipmentType.Body, defense: 20);
            var owner = Inactive("Armor enhancement player");
            PlayerInventory bag = AddInventory(owner, armor);
            PlayerEquipment gear = owner.AddComponent<PlayerEquipment>();
            owner.SetActive(true);
            bag.Add(armor.ItemId, 2);
            Assert.That(gear.TryEquipAuto(0), Is.True);
            Assert.That(gear.TryEnhance(EquipmentSlot.Body), Is.True);
            Assert.That(gear.TotalDefense, Is.EqualTo(23));
            Assert.That(armor.Defense, Is.EqualTo(20));
            Assert.That(bag.SlotCount, Is.EqualTo(PlayerInventory.Capacity));
        }

        [Test]
        public void EnhancedGearCanBeSoldAtBasePriceWithoutChangingTheCatalog()
        {
            ItemDefinition sword = Item("enhance-sale", 1, EquipmentType.Weapon, attack: 20);
            var owner = Inactive("Enhanced seller");
            PlayerInventory bag = AddInventory(owner, sword);
            PlayerEquipment gear = owner.AddComponent<PlayerEquipment>();
            PlayerWallet wallet = owner.AddComponent<PlayerWallet>();
            owner.AddComponent<PlayerQuickSlots>();
            owner.SetActive(true);
            bag.Add(sword.ItemId, 2);
            Assert.That(gear.TryEquipAuto(0), Is.True);
            Assert.That(gear.TryEnhanceWeapon(), Is.True);
            ItemDefinition enhanced = gear.ActiveWeapon;
            int slot = 0;
            while (bag.GetSlot(slot).Item != enhanced) slot++;

            var offer = new ShopCatalog.Offer();
            TestKit.SetField(offer, "item", sword);
            TestKit.SetField(offer, "buyPrice", 20);
            TestKit.SetField(offer, "sellPrice", 10);
            ShopCatalog catalog = Track(UnityEngine.ScriptableObject.CreateInstance<ShopCatalog>());
            TestKit.SetField(catalog, "offers", new[] { offer });
            var merchant = Inactive("Enhanced sale merchant");
            Shopkeeper shop = merchant.AddComponent<Shopkeeper>();
            TestKit.SetField(shop, "catalog", catalog);
            merchant.SetActive(true);
            int gold = wallet.Gold;
            Assert.That(shop.FindOffer(enhanced), Is.Zero);
            Assert.That(shop.TrySell(bag, slot, 1, out string message), Is.True, message);
            Assert.That(wallet.Gold, Is.EqualTo(gold + 10));
            Assert.That(bag.GetSlot(slot).Item, Is.Null);
            Assert.That(catalog.GetOffer(0).Item, Is.SameAs(sword));
            Assert.That(sword.EnhancementLevel, Is.Zero);
        }
    }
}
