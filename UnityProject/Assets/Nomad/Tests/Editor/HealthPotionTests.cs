using System.Collections;
using Nomad.Combat;
using Nomad.Consumables;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Shop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    public sealed class HealthPotionTests
    {
        [UnityTest]
        public IEnumerator ActualPlayerUsesQWithoutWastingFullHealthAndShopGearEquips()
        {
            yield return new EnterPlayMode();
            GameObject root = null;
            Keyboard keyboard = null;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            try
            {
                root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab"));
                var inventory = root.GetComponentInChildren<PlayerInventory>();
                inventory.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
                yield return null;
                Health health = inventory.GetComponent<Health>();
                PlayerHealing healing = inventory.GetComponent<PlayerHealing>();
                PlayerEquipmentCombat combat = inventory.GetComponent<PlayerEquipmentCombat>();
                Assert.That(healing.Count, Is.EqualTo(3), "시작 지급3개");
                health.RestoreFull();
                Assert.That(healing.TryUse(), Is.False, "체력이 가득 차면 낭비하지 않음");
                health.TryApplyDamage(60, DamageKind.Fall);
                int before = health.CurrentHealth;
                combat.InputBlocked = true;
                Assert.That(healing.TryUse(), Is.False, "UI/전환 중 사용 차단");
                combat.InputBlocked = false;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(health.CurrentHealth, Is.EqualTo(before + 40));
                Assert.That(healing.Count, Is.EqualTo(2), "실제 Q 입력으로1개 소모");
                Assert.That(healing.TryUse(), Is.False, "같은 순간 중복 사용 방지");
                float waitUntil = Time.time + 0.7f;
                while (Time.time < waitUntil) yield return null;
                Assert.That(healing.TryUse(), Is.True);
                Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth), "최대 체력 초과 없음");
                Assert.That(healing.Count, Is.EqualTo(1));
                health.TryApplyDamage(int.MaxValue, DamageKind.Fall);
                Assert.That(healing.TryUse(), Is.False, "죽은 플레이어 부활 금지");
                Assert.That(healing.Count, Is.EqualTo(1));

                var equipment = inventory.GetComponent<PlayerEquipment>();
                foreach (string id in new[] { "weapon-dagger", "gear-padded-armor" })
                {
                    inventory.Add(id, 1);
                    int slot = -1;
                    for (int index = 0; index < inventory.SlotCount; index++)
                        if (inventory.GetSlot(index).Item?.ItemId == id) slot = index;
                    Assert.That(slot, Is.GreaterThanOrEqualTo(0), "실제 상점 장비 등록 " + id);
                    Assert.That(equipment.TryEquipAuto(slot), Is.True, "삭제된 테스트 장비를 대체하는 실제 장비 장착 " + id);
                }
                ShopCatalog shop = AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Nomad/Shop/Data/VillageMarket.asset");
                bool stocked = false;
                for (int index = 0; index < shop.Count; index++)
                    if (shop.GetOffer(index).Item.ItemId == PlayerHealing.PotionId) stocked = shop.GetOffer(index).InitialStock > 0;
                Assert.That(stocked, Is.True, "마을 상점 보충 가능");
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditor;
                if (root != null) Object.DestroyImmediate(root);
            }
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }
    }
}
