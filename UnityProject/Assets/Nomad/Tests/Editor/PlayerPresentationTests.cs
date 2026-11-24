using System.Collections;
using System.IO;
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
    public sealed class PlayerPresentationTests : PlayModeFixture
    {
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

        [UnitySetUp]
        public IEnumerator CreatePlayer()
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

        [TearDown]
        public void RestoreInput()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            previousKeyboard?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Application.runInBackground = background;
        }

        [UnityTest]
        public IEnumerator MovingLandingAndRollDoNotKeepRecoveryWhileSliding()
        {
            var body = movement.GetComponent<CharacterController>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            float deadline = Time.time + 3f;
            while (body.isGrounded && Time.time < deadline) yield return null;
            Assert.That(body.isGrounded, Is.False, "실제 점프 시작");
            while (!body.isGrounded && Time.time < deadline) yield return null;
            Assert.That(body.isGrounded, Is.True, "실제 착지");
            yield return null;
            yield return null;
            Assert.That(animation.CurrentState, Is.EqualTo("Run"), "이동 중 착지 뒤 Land 고정 없음");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            while (!movement.IsRolling && Time.time < deadline) yield return null;
            Assert.That(animation.CurrentState, Is.EqualTo("Roll"));
            Capture("roll");
            while (movement.IsRolling && Time.time < deadline) yield return null;
            yield return null;
            yield return null;
            Assert.That(animation.CurrentState, Is.EqualTo("Run"), "구르기 종료 후 남는 슬라이딩 모션 없음");
            Assert.That(PlayerAnimation.RollMotionDuration, Is.EqualTo(PlayerMovement.RollDuration));
        }

        [UnityTest]
        public IEnumerator MeleeBowAndStaffAnimateAndKeepWeaponInHand()
        {
            movement.enabled = false;
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
                yield return new WaitForSeconds(0.08f);
                string expected = id.Contains("bow") ? "BowAttack" : id.Contains("staff") ? "StaffAttack" : "SwordAttack";
                Assert.That(presenter.CurrentAttackState, Is.EqualTo(expected), id);
                Assert.That(Quaternion.Angle(before, hand.rotation), Is.GreaterThan(1f), "실제 손 뼈가 공격 모션으로 움직임: " + id);
                Transform model = animation.GetComponent<EquipmentCombatVisuals>().WeaponModel.transform;
                Assert.That(model.parent.name, Is.EqualTo(id.Contains("bow") ? "BowSocket" : "WeaponSocket"));
                Capture(id);
                yield return new WaitForSeconds(combat.CurrentWeapon.AttackInterval + 0.05f);
            }
        }

        [Test]
        public void ProvidedSpritesAreAssignedToActualItems()
        {
            foreach (string id in new[] { "weapon-dagger", "weapon-shortbow", "weapon-apprentice-staff", "health-potion" })
            {
                ItemDefinition item = inventory.Catalog.FirstOrDefault(candidate => candidate.ItemId == id);
                Assert.That(item, Is.Not.Null);
                Assert.That(item.Icon, Is.Not.Null, id);
                Assert.That(AssetDatabase.GetAssetPath(item.Icon), Does.Contain("Inventory/Resources/ItemUi/"));
                Assert.That(item.Icon.rect.width, Is.GreaterThan(60));
            }
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
