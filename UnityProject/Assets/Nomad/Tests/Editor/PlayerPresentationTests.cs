using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
    public sealed class PlayerPresentationTests
    {
        private readonly List<Object> created = new List<Object>();
        private GameObject rig;
        private PlayerAnimation animation;
        private PlayerMovement movement;
        private PlayerEquipment equipment;
        private PlayerInventory inventory;
        private PlayerEquipmentCombat combat;
        private Keyboard keyboard;
        private Keyboard previousKeyboard;
        private Camera camera;
        private bool background;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        [UnityTest]
        public IEnumerator MainRigLandingRollAndWeaponPresentation()
        {
            yield return new EnterPlayMode();
            try
            {
                yield return CreatePlayer();
                yield return MovingLandingAndRollDoNotKeepRecoveryWhileSliding();
                yield return MeleeBowAndStaffAnimateAndKeepWeaponInHand();
            }
            finally
            {
                RestoreInput();
                foreach (Object target in created) if (target != null) Object.DestroyImmediate(target);
                created.Clear();
            }
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }

        private T Track<T>(T target) where T : Object
        {
            created.Add(target);
            return target;
        }

        private IEnumerator CreatePlayer()
        {
            background = Application.runInBackground;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousKeyboard = Keyboard.current;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();

            GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = Vector3.down * 0.5f;
            floor.transform.localScale = new Vector3(80, 1, 80);
            rig = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab")));
            animation = rig.GetComponentInChildren<PlayerAnimation>();
            movement = animation.GetComponent<PlayerMovement>();
            equipment = animation.GetComponent<PlayerEquipment>();
            inventory = animation.GetComponent<PlayerInventory>();
            combat = animation.GetComponent<PlayerEquipmentCombat>();
            camera = Track(new GameObject("Presentation camera")).AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 12, -8);
            camera.transform.LookAt(Vector3.zero);
            foreach (Camera existing in rig.GetComponentsInChildren<Camera>()) existing.enabled = false;
            TestKit.SetField(combat, "viewCamera", camera);
            for (int frame = 0; frame < 10; frame++) yield return null;
        }

        private void RestoreInput()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            previousKeyboard?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Application.runInBackground = background;
        }

        private IEnumerator MovingLandingAndRollDoNotKeepRecoveryWhileSliding()
        {
            var body = movement.GetComponent<CharacterController>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            float deadline = Time.realtimeSinceStartup + 5f;
            while (body.isGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(body.isGrounded, Is.False, "실제 점프 시작");
            while (!body.isGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(body.isGrounded, Is.True, "실제 착지");
            yield return null;
            yield return null;
            Assert.That(animation.CurrentState, Is.EqualTo("Run"), "이동 중 착지 뒤 Land 고정 없음");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            while (!movement.IsRolling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(animation.CurrentState, Is.EqualTo("Roll"));
            yield return Wait(0.12f);
            Capture("roll");
            while (movement.IsRolling && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            yield return null;
            Assert.That(animation.CurrentState, Is.EqualTo("Run"), "구르기 종료 후 남는 슬라이딩 모션 없음");
            Assert.That(PlayerAnimation.RollMotionDuration, Is.EqualTo(PlayerMovement.RollDuration));
        }

        private IEnumerator MeleeBowAndStaffAnimateAndKeepWeaponInHand()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            movement.enabled = false;
            var body = movement.GetComponent<CharacterController>();
            body.enabled = false;
            movement.transform.SetPositionAndRotation(Vector3.up * 0.05f, Quaternion.identity);
            body.enabled = true;
            inventory.Add("weapon-arrow", 20);
            var presenter = animation.GetComponent<PlayerSwordAnimation>();
            foreach (string id in new[] { "weapon-dagger", "weapon-greatsword", "weapon-shortbow", "weapon-apprentice-staff" })
            {
                inventory.Add(id, 1);
                int slot = -1;
                for (int index = 0; index < inventory.SlotCount; index++)
                    if (inventory.GetSlot(index).Item?.ItemId == id) slot = index;
                Assert.That(equipment.TryEquip(slot, EquipmentSlot.Weapon1), Is.True);
                yield return null;
                Transform hand = animation.Animator.GetBoneTransform(HumanBodyBones.RightHand);
                Quaternion before = hand.rotation;
                Assert.That(combat.TryAttack(camera.WorldToScreenPoint(Vector3.forward * 6)), Is.True, id);
                yield return Wait(0.08f);
                string expected = id.Contains("bow") ? "BowAttack" : id.Contains("staff") ? "StaffAttack" : "SwordAttack";
                Assert.That(presenter.CurrentAttackState, Is.EqualTo(expected), id);
                Assert.That(Quaternion.Angle(before, hand.rotation), Is.GreaterThan(1f), "실제 손 뼈가 공격 모션으로 움직임: " + id);
                Transform model = animation.GetComponent<EquipmentCombatVisuals>().WeaponModel.transform;
                Assert.That(model.parent.name, Is.EqualTo(id.Contains("bow") ? "BowSocket" : "WeaponSocket"));
                if (id.Contains("bow"))
                {
                    Vector3 handOffset = animation.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position - movement.transform.position;
                    Assert.That(Vector3.Dot(handOffset, presenter.AttackFacing * Vector3.forward), Is.GreaterThan(0.3f), "활을 쥔 손을 앞으로 뻗음");
                }
                Capture(id);
                yield return Wait(combat.CurrentWeapon.AttackInterval + 0.05f);
            }
        }

        [Test]
        public void ProvidedSpritesAreAssignedToActualItems()
        {
            var inventory = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nomad/Game/Prefabs/PlayerRig.prefab")
                .GetComponentInChildren<PlayerInventory>();
            foreach (string id in new[] { "weapon-dagger", "weapon-shortbow", "weapon-apprentice-staff", "health-potion" })
            {
                ItemDefinition item = inventory.Catalog.FirstOrDefault(candidate => candidate.ItemId == id);
                Assert.That(item, Is.Not.Null);
                Assert.That(item.Icon, Is.Not.Null, id);
                Assert.That(AssetDatabase.GetAssetPath(item.Icon), Does.Contain("Inventory/Resources/ItemUi/"));
                Assert.That(item.Icon.rect.width, Is.GreaterThan(60));
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Time.time, Is.GreaterThanOrEqualTo(until), "모션 검증 프레임 진행");
        }

        private void Capture(string name)
        {
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            var target = new RenderTexture(640, 640, 24);
            var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.transform.position = movement.transform.position + new Vector3(2.6f, 2, 3);
                camera.transform.LookAt(movement.transform.position + Vector3.up * 0.9f);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts");
                File.WriteAllBytes("Artifacts/presentation-" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                camera.transform.SetPositionAndRotation(position, rotation);
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
