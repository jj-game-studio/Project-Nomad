using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Nomad.Tests
{
    public sealed class WeaponSkillTests
    {
        [UnityTest]
        public IEnumerator EnhancedWeaponsUnlockDistinctSkillsAndShareCooldown()
        {
            yield return new EnterPlayMode();
            var created = new List<Object>();
            var muted = new List<InputDevice>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int capture = Time.captureFramerate;
            try
            {
                foreach (InputDevice device in InputSystem.devices.Where(device => device != mouse && device != keyboard && (device is Mouse || device is Keyboard)).ToArray())
                { InputSystem.DisableDevice(device); muted.Add(device); }
                mouse.MakeCurrent();
                keyboard.MakeCurrent();
                Time.captureFramerate = 60;
                GameObject rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab"));
                created.Add(rig);
                var combat = rig.GetComponentInChildren<PlayerEquipmentCombat>();
                var gear = combat.GetComponent<PlayerEquipment>();
                var bag = combat.GetComponent<PlayerInventory>();
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(floor);
                floor.transform.position = Vector3.down * 0.5f;
                floor.transform.localScale = new Vector3(80, 1, 80);
                foreach (Camera old in rig.GetComponentsInChildren<Camera>()) old.enabled = false;
                var view = new GameObject("Skill test camera");
                created.Add(view);
                Camera camera = view.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 14;
                camera.transform.position = new Vector3(0, 16, -12);
                camera.transform.LookAt(Vector3.zero);
                TestKit.SetField(combat, "viewCamera", camera);
                bag.Add(PlayerEquipmentCombat.ArrowItemId, 20);
                yield return null;

                foreach (string id in new[] { "weapon-shortbow", "weapon-apprentice-staff", "demo-sword" })
                {
                    bag.Add(id, 2);
                    int slot = Enumerable.Range(0, bag.SlotCount).First(index => bag.GetSlot(index).Item?.ItemId == id && bag.GetSlot(index).Item.EnhancementLevel == 0);
                    Assert.That(gear.EquipWeapon(slot, 0), Is.True);
                    Vector2 pointer = camera.WorldToScreenPoint(new Vector3(0, 1, 9));
                    Assert.That(combat.TryUseSkill(pointer), Is.False, "미강화 무기는 스킬 없음");
                    Assert.That(gear.TryEnhanceWeapon(), Is.True);
                    combat.GetComponent<PlayerMana>().RestoreFull();
                    float ready = Time.time + combat.CurrentWeapon.AttackInterval + 0.2f;
                    while (Time.time < ready) yield return null;
                    Assert.That(combat.CurrentWeapon.UnlockedSkill, Is.Not.Empty);

                    Health first = Target(new Vector3(0, 0, id == "demo-sword" ? 3 : 8), created);
                    Health second = Target(id == "weapon-shortbow" ? new Vector3(0, 0, 11) :
                        id == "demo-sword" ? new Vector3(1, 0, 4) : new Vector3(2, 0, 8), created);
                    int arrows = combat.ArrowCount;
                    float mana = combat.GetComponent<PlayerMana>().Current;
                    Physics.SyncTransforms();
                    Assert.That(combat.TryUseSkill(pointer), Is.True, id);
                    Assert.That(combat.TryUseSkill(pointer), Is.False, "연속 사용 차단");
                    Assert.That(combat.SkillRemaining, Is.GreaterThan(7.9f));
                    float flight = Time.time + 1;
                    while (Time.time < flight) yield return null;
                    Assert.That(first.CurrentHealth, Is.LessThan(first.MaxHealth), id + " 첫 대상 피해");
                    Assert.That(second.CurrentHealth, Is.LessThan(second.MaxHealth), id + " 관통/폭발/부채꼴 두 번째 피해");
                    if (id == "weapon-shortbow") Assert.That(combat.ArrowCount, Is.EqualTo(arrows - 1));
                    if (id.Contains("staff")) Assert.That(combat.GetComponent<PlayerMana>().Current, Is.LessThan(mana));
                    Object.DestroyImmediate(first.gameObject);
                    Object.DestroyImmediate(second.gameObject);
                    float cooldown = Time.time + combat.SkillRemaining + 0.1f;
                    while (Time.time < cooldown) yield return null;
                }
            }
            finally
            {
                foreach (Object item in created) if (item != null) Object.DestroyImmediate(item);
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                foreach (InputDevice device in muted) InputSystem.EnableDevice(device);
                Time.captureFramerate = capture;
            }
            yield return new ExitPlayMode();
        }

        private static Health Target(Vector3 point, List<Object> created)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            created.Add(go);
            go.transform.position = point;
            go.GetComponent<CapsuleCollider>().center = Vector3.up;
            go.SetActive(false);
            Health health = go.AddComponent<Health>();
            JsonUtility.FromJsonOverwrite("{\"maxHealth\":2000}", health);
            go.SetActive(true);
            return health;
        }
    }
}
