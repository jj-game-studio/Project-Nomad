using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Player;
using Nomad.PlayerVisual;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Nomad.Tests
{
    public sealed class RangedAimAlignmentTests
    {
        [UnityTest]
        public IEnumerator MovingAndTurningKeepBodyPoseEffectsAndProjectileOnCursor()
        {
            yield return new EnterPlayMode();
            var created = new List<Object>();
            var muted = new List<InputDevice>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int capture = Time.captureFramerate;
            bool background = Application.runInBackground;
            var behavior = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            try
            {
                foreach (InputDevice device in InputSystem.devices.Where(device => device != mouse && device != keyboard && (device is Mouse || device is Keyboard)).ToArray())
                {
                    InputSystem.DisableDevice(device);
                    muted.Add(device);
                }
                mouse.MakeCurrent();
                keyboard.MakeCurrent();
                Time.captureFramerate = 60;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(floor);
                floor.transform.position = Vector3.down * 0.5f;
                floor.transform.localScale = new Vector3(100, 1, 100);

                GameObject rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab"));
                created.Add(rig);
                var combat = rig.GetComponentInChildren<PlayerEquipmentCombat>();
                var movement = combat.GetComponent<PlayerMovement>();
                movement.enabled = true;
                var inventory = combat.GetComponent<PlayerInventory>();
                var equipment = combat.GetComponent<PlayerEquipment>();
                var visuals = combat.GetComponent<EquipmentCombatVisuals>();
                Transform visual = combat.transform.Find("Visual");

                foreach (Camera old in rig.GetComponentsInChildren<Camera>()) old.enabled = false;
                var view = new GameObject("Aim alignment camera");
                created.Add(view);
                Camera camera = view.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 12;
                camera.transform.position = new Vector3(0, 16, -12);
                camera.transform.LookAt(Vector3.zero);

                TestKit.SetField(combat, "viewCamera", camera);
                TestKit.SetField(movement, "viewCamera", camera);
                inventory.Add(PlayerEquipmentCombat.ArrowItemId, 30);
                yield return null;

                foreach (string id in new[] { "weapon-shortbow", "weapon-apprentice-staff" })
                {
                    inventory.Add(id, 1);
                    int slot = Enumerable.Range(0, PlayerInventory.Capacity).First(index => inventory.GetSlot(index).Item?.ItemId == id);
                    Assert.That(equipment.TryEquip(slot, EquipmentSlot.Weapon1), Is.True);
                    equipment.SelectWeapon(EquipmentSlot.Weapon1);
                    combat.GetComponent<PlayerMana>().RestoreFull();
                    for (int frame = 0; frame < 12; frame++) yield return null;
                    if (id.Contains("staff")) AssertStaffGrip(combat, visuals, false);

                    GameObject obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    created.Add(obstruction);
                    obstruction.transform.position = combat.ProjectileOrigin;
                    obstruction.transform.localScale = Vector3.one * 0.3f;
                    Physics.SyncTransforms();
                    Assert.That(combat.TryAttack(camera.WorldToScreenPoint(combat.transform.position + Vector3.forward * 7)), Is.False, "막힌 발사구에서 발사 거절");
                    Assert.That(movement.AimFacing.HasValue, Is.False, "발사 실패가 이동 방향을 잠그지 않음");
                    Object.DestroyImmediate(obstruction);

                    int fired = 0;

                    System.Action<Projectile, ItemDefinition> onFire = (shot, _) =>
                    {
                        fired++;
                        Vector3 expected = combat.AimPoint.Value;
                        Assert.That(Vector3.Angle(shot.transform.forward, expected - shot.transform.position), Is.LessThan(0.1f), "커서 조준 지점으로 실제 발사");
                        Assert.That(Vector3.Distance(shot.transform.position, combat.ProjectileOrigin), Is.LessThan(0.001f), "현재 프레임 무기 위치에서 발사");
                        Assert.That(Vector3.Angle(combat.transform.forward, Flat(shot.transform.forward)), Is.LessThan(0.2f), "발사 시 몸도 커서 방향");
                    };
                    combat.Fired += onFire;

                    Vector2 pointer = camera.WorldToScreenPoint(new Vector3(4, 1, 8));
                    int arrows = combat.ArrowCount;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Right));
                    for (int frame = 0; frame < 4; frame++) yield return null;
                    Assert.That(fired, Is.Zero, "우클릭 공격 없음");
                    Assert.That(combat.ArrowCount, Is.EqualTo(arrows));

                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Left));
                    for (int frame = 0; frame < 6; frame++) yield return null;
                    Assert.That(combat.IsCharging, Is.True);
                    foreach (Key key in new[] { Key.W, Key.D, Key.S, Key.A })
                    {
                        Vector3 start = movement.transform.position;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                        for (int frame = 0; frame < 12; frame++) yield return null;
                        Assert.That(Flat(movement.transform.position - start).magnitude, Is.GreaterThan(0.1f), "실제 방향키 이동 " + key);
                        AssertAligned(combat, visual, visuals);
                        if (id.Contains("staff")) AssertStaffGrip(combat, visuals, true);
                    }

                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                    foreach (Vector3 direction in new[] { Vector3.left, Vector3.back, Vector3.right, Vector3.forward })
                    {
                        pointer = camera.WorldToScreenPoint(combat.transform.position + direction * 7 + Vector3.up);
                        InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Left));
                        for (int frame = 0; frame < 12; frame++) yield return null;
                        AssertAligned(combat, visual, visuals);
                    }
                    Nomad.Minimap.Editor.MinimapVerification.Capture("issue210-aligned-" + id + ".png");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                    for (int frame = 0; frame < 3; frame++) yield return null;
                    Assert.That(fired, Is.EqualTo(1), "이동 중 좌클릭 놓기에 한 발");
                    Assert.That(combat.GetComponentInChildren<ChargeGatherVfx>().ParticleCount, Is.Zero);

                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    float releasedAt = Time.time;
                    while (Time.time < releasedAt + combat.CurrentWeapon.AttackInterval + 0.2f) yield return null;
                    Assert.That(movement.AimFacing.HasValue, Is.False, "공격 후 조준 회전 잠금 해제");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    for (int frame = 0; frame < 10; frame++) yield return null;
                    Assert.That(Vector3.Angle(movement.transform.forward, Flat(camera.transform.forward)), Is.LessThan(1f),
                        "공격 후 일반 이동 회전 복원");
                    if (id.Contains("staff")) AssertStaffGrip(combat, visuals, false);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    pointer = camera.WorldToScreenPoint(combat.transform.position + Vector3.up);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Left));
                    for (int frame = 0; frame < 15; frame++) yield return null;
                    Assert.That(combat.IsCharging, Is.True, "몸통 근처 커서도 차징 가능");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                    for (int frame = 0; frame < 3; frame++) yield return null;
                    Assert.That(fired, Is.EqualTo(2), "몸통 근처에서도 현재 정면으로 발사");
                    combat.Fired -= onFire;
                    float ready = Time.time + combat.CurrentWeapon.AttackInterval + 0.2f;
                    while (Time.time < ready) yield return null;
                    Debug.Log("RANGED_AIM_ALIGNMENT_PASS " + id + " movement=4 cursor=4 nearBody=pass staffGrip=pass rightClick=none");
                }
            }
            finally
            {
                foreach (Object item in created) if (item != null) Object.DestroyImmediate(item);
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                foreach (InputDevice device in muted) InputSystem.EnableDevice(device);
                Time.captureFramerate = capture;
                Application.runInBackground = background;
                InputSystem.settings.backgroundBehavior = behavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }

        private static Vector3 Flat(Vector3 value) => Vector3.ProjectOnPlane(value, Vector3.up);

        private static void AssertStaffGrip(PlayerEquipmentCombat combat, EquipmentCombatVisuals visuals, bool casting)
        {
            Transform hand = combat.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            Assert.That(Vector3.Distance(visuals.WeaponModel.transform.position, hand.position), Is.LessThan(0.06f), "손잡이가 실제 오른손에 붙음");
            Vector3 shaft = combat.transform.rotation * Quaternion.Euler(casting ? 65 : 15, 0, 0) * Vector3.up;
            Assert.That(Vector3.Angle(visuals.WeaponModel.transform.up, shaft), Is.LessThan(8), "손목과 연결된 지팡이 기울기");
        }

        private static void AssertAligned(PlayerEquipmentCombat combat, Transform visual, EquipmentCombatVisuals visuals)
        {
            Assert.That(combat.IsCharging, Is.True);
            Vector3 aim = Flat(combat.AimPoint.Value - combat.ProjectileOrigin);
            Assert.That(Vector3.Angle(combat.transform.forward, aim), Is.LessThan(1f), "키보드 이동과 무관하게 몸과 발사구 조준 일치");
            Assert.That(Vector3.Angle(visual.forward, combat.transform.forward), Is.LessThan(2f), "몸과 상체 방향 일치");
            Assert.That(Vector3.Distance(combat.GetComponentInChildren<ChargeGatherVfx>().transform.position, combat.ProjectileOrigin), Is.LessThan(0.15f), "차징 입자는 현재 무기를 따라감");
            string name = combat.CurrentWeapon.AttackMode == WeaponAttackMode.Bow ? "Bow Muzzle" : "Staff Tip Muzzle";
            Transform muzzle = visuals.WeaponModel.GetComponentsInChildren<Transform>().Single(node => node.name == name);
            Assert.That(Vector3.Distance(combat.ProjectileOrigin, muzzle.position), Is.LessThan(0.001f), "이전 위치 캐시 없이 현재 무기 위치 사용");
        }
    }
}
