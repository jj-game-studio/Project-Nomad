using System.Collections;
using System.IO;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Loot;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nomad.Tests
{
    public sealed class PlaytestPolishTests
    {
        [Test]
        public void ItemSheetHasTransparentBackground()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes("Assets/Nomad/Inventory/Resources/ItemUi/StyleSheet.png"));
                foreach (Vector2Int point in new[] { new Vector2Int(42, 237), new Vector2Int(1100, 68), new Vector2Int(1298, 74), new Vector2Int(965, 410) })
                    Assert.That(texture.GetPixel(point.x, texture.height - point.y - 1).a, Is.LessThan(0.02f), "아이콘 영역 모서리는 투명(윤곽 안티앨리어싱 허용)");
                Assert.That(texture.GetPixels32().Any(pixel => pixel.a >= 250), Is.True, "아이콘 본체 보존");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [UnityTest]
        public IEnumerator ChargeSpendsOnlyOnReleaseAndDeathCreatesRankSpecificLoot()
        {
            yield return new EnterPlayMode();
            GameObject rig = null;
            GameObject cameraRoot = null;
            GameObject monsters = null;
            Material monsterMaterial = null;
            try
            {
                rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab"));
                var combat = rig.GetComponentInChildren<PlayerEquipmentCombat>();
                var inventory = combat.GetComponent<PlayerInventory>();
                var equipment = combat.GetComponent<PlayerEquipment>();
                var mana = combat.GetComponent<PlayerMana>();
                combat.GetComponent<PlayerMovement>().enabled = false;
                foreach (Camera existing in rig.GetComponentsInChildren<Camera>()) existing.enabled = false;
                cameraRoot = new GameObject("Charge test camera");
                Camera camera = cameraRoot.AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 12, -8);
                camera.transform.LookAt(Vector3.zero);
                TestKit.SetField(combat, "viewCamera", camera);
                yield return null;
                Vector2 pointer = camera.WorldToScreenPoint(new Vector3(0, 1, 6));

                foreach (string id in new[] { "weapon-shortbow", "weapon-apprentice-staff" })
                {
                    Assert.That(inventory.Add(id, 1), Is.EqualTo(1));
                    int slot = Enumerable.Range(0, PlayerInventory.Capacity).First(index => inventory.GetSlot(index).Item?.ItemId == id);
                    Assert.That(equipment.TryEquip(slot, EquipmentSlot.Weapon1), Is.True);
                    equipment.SelectWeapon(EquipmentSlot.Weapon1);
                    inventory.Add(PlayerEquipmentCombat.ArrowItemId, 10);
                    mana.RestoreFull();
                    TestKit.SetField(combat, "nextAttackTime", 0f);
                    int arrows = combat.ArrowCount;
                    float beforeMana = mana.Current;
                    Assert.That(combat.TryBeginCharge(pointer), Is.True, id);
                    combat.CancelCharge();
                    Assert.That(combat.ArrowCount, Is.EqualTo(arrows));
                    Assert.That(mana.Current, Is.EqualTo(beforeMana));
                    Assert.That(combat.TryBeginCharge(pointer), Is.True);
                    TestKit.SetField(combat, "chargeStarted", Time.time - PlayerEquipmentCombat.FullChargeSeconds);
                    Assert.That(combat.TryReleaseCharge(pointer), Is.True);
                    Assert.That(combat.LastShotPower, Is.EqualTo(2.5f));
                    Assert.That(combat.ArrowCount, Is.EqualTo(arrows - (combat.CurrentWeapon.AttackMode == WeaponAttackMode.Bow ? 1 : 0)));
                    Assert.That(mana.Current, Is.EqualTo(beforeMana - (combat.CurrentWeapon.AttackMode == WeaponAttackMode.Staff ? combat.CurrentWeapon.ManaCost : 0)));
                    TestKit.SetField(combat, "nextAttackTime", 0f);
                    Assert.That(combat.TryBeginCharge(pointer), Is.True);
                    combat.InputBlocked = true;
                    Assert.That(combat.IsCharging, Is.False, "UI/퍼즈 입력 차단 시 차징 취소");
                    combat.InputBlocked = false;
                }

                monsters = new GameObject("Loot rank test");
                var factory = monsters.AddComponent<MonsterActorFactory>();
                monsterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                factory.ConfigureMaterials(monsterMaterial, monsterMaterial, monsterMaterial, monsterMaterial);
                foreach (MonsterRank rank in new[] { MonsterRank.Normal, MonsterRank.Elite, MonsterRank.Boss })
                {
                    Vector3 spawn = new Vector3((int)rank * 8, 0, 10);
                    Health actor = factory.CreateRanked(combat.GetComponent<Health>(), new MonsterTestSettings(), rank,
                        spawn, Quaternion.identity, rank.ToString(), monsters.transform);
                    actor.transform.position += Vector3.forward * 5;
                    Assert.That(actor.TryApplyDamage(10000, DamageKind.Fall), Is.True);
                    if (rank == MonsterRank.Boss)
                    {
                        LootChest reward = monsters.GetComponentsInChildren<LootChest>().Single(chest => chest.ChestId.StartsWith("boss-reward-"));
                        Assert.That(reward.transform.position, Is.EqualTo(spawn), "보스의 최초 스폰 중앙");
                        Assert.That(actor.GetComponent<LootChest>(), Is.Null);
                    }
                    else
                        Assert.That(actor.GetComponent<LootChest>()?.ChestId, Does.StartWith("corpse-"), "시체 자체에서 파밍");
                }
                Debug.Log("PLAYTEST_POLISH_PASS charge=bow,staff power=2.5 resources=release-only normal,elite=corpse boss=spawn-center");
            }
            finally
            {
                if (monsters != null) Object.DestroyImmediate(monsters);
                if (monsterMaterial != null) Object.DestroyImmediate(monsterMaterial);
                if (rig != null) Object.DestroyImmediate(rig);
                if (cameraRoot != null) Object.DestroyImmediate(cameraRoot);
            }
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }
    }
}
