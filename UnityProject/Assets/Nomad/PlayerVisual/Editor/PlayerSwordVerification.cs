using System;
using System.IO;
using System.Linq;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.PlayerVisual.Editor
{
    internal static class PlayerSwordVerification
    {
        private const string Session = "Nomad.SwordVerification";
        private static PlayerSwordAnimation sword;
        private static PlayerEquipmentCombat combat;
        private static PlayerEquipment equipment;
        private static EquipmentCombatVisuals visuals;
        private static Health target;
        private static Keyboard keyboard;
        private static int stage, frame, attacks, impacts;
        private static float started;
        private static double deadline;
        private static Quaternion hand;
        private static Vector3 position;

        public static void Run()
        {
            EditorSceneManager.OpenScene(PlayerSwordAuthoring.ScenePath);
            SessionState.SetBool(Session, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Session, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, $"Timeout stage={stage}");
                if (!Application.isPlaying || frame == Time.frameCount) return;
                frame = Time.frameCount;
                if (sword == null)
                {
                    sword = Object.FindAnyObjectByType<PlayerSwordAnimation>();
                    if (sword == null || Camera.main == null) return;
                    Time.captureFramerate = 60;
                    Application.runInBackground = true;
                    combat = sword.GetComponent<PlayerEquipmentCombat>();
                    equipment = sword.GetComponent<PlayerEquipment>();
                    visuals = sword.GetComponent<EquipmentCombatVisuals>();
                    target = GameObject.Find("Sword Target").GetComponent<Health>();
                    combat.Attacked += _ => attacks++;
                    combat.WeaponImpacted += (_, _) => impacts++;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    Next();
                }

                if (stage == 4)
                {
                    InputState.Change(keyboard, new KeyboardState(Key.A), InputUpdateType.Dynamic);
                }
                float elapsed = Time.time - started;
                var animator = sword.GetComponent<PlayerAnimation>().Animator;
                if (stage == 1 && elapsed > 0.5f)
                {
                    Check(combat.CurrentWeapon?.ItemId == PlayerSwordAnimation.SwordId, "Starter sword missing");
                    Check(sword.Socket?.parent == animator.GetBoneTransform(HumanBodyBones.RightHand), "Right hand socket missing");
                    Check(visuals.WeaponModel != null && visuals.WeaponModel.transform.parent == sword.Socket, "Sword not attached to hand");
                    Check(visuals.WeaponModel.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Equipped collider enabled");
                    hand = sword.Socket.parent.rotation;
                    Capture("sword-idle.png");
                    Check(Attack(), "First attack rejected");
                    Check(!Attack(), "Cooldown allowed second attack");
                    Check(target.CurrentHealth == 75 && attacks == 1 && impacts == 1, "Expected one immediate 25-damage hit");
                    Next();
                }
                else if (stage == 2 && elapsed > 0.1f)
                {
                    Check(sword.IsSwinging && animator.GetLayerWeight(1) > 0.9f, "Sword animation missing");
                    Check(Quaternion.Angle(hand, sword.Socket.parent.rotation) > 5f, "Humanoid hand did not animate");
                    Check(visuals.WeaponModel.transform.parent == sword.Socket, "Weapon detached while attacking");
                    Capture("sword-attack.png");
                    Next();
                }
                else if (stage == 3 && elapsed > 0.7f)
                {
                    Check(!sword.IsSwinging && animator.GetLayerWeight(1) < 0.01f, "Attack did not finish");
                    Check(target.CurrentHealth == 75 && attacks == 1 && impacts == 1, "Duplicate hit after animation");
                    combat.InputBlocked = true;
                    Check(!Attack(), "Blocked input attacked");
                    combat.InputBlocked = false;
                    position = sword.transform.position;
                    InputState.Change(keyboard, new KeyboardState(Key.A), InputUpdateType.Dynamic);
                    Check(Attack(), "Second attack rejected");
                    Next();
                }
                else if (stage == 4 && elapsed > 0.15f)
                {
                    Check(Vector3.Distance(position, sword.transform.position) > 0.2f,
                        $"Attack prevented movement: before={position} after={sword.transform.position}, keyboard={Keyboard.current?.deviceId}/{keyboard.deviceId}, A={Keyboard.current?.aKey.isPressed}, dt={Time.deltaTime}");
                    Check(sword.GetComponent<PlayerAnimation>().CurrentState == "Run", "Leg locomotion missing during attack");
                    Check(Mathf.Abs(animator.speed - 1f) < 0.001f, "Run changed attack playback speed");
                    Capture("sword-moving.png");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    equipment.SelectWeapon(EquipmentSlot.Weapon2);
                    Next();
                }
                else if (stage == 5 && elapsed > 0.15f)
                {
                    Check(!sword.IsSwinging && animator.GetLayerWeight(1) == 0f, "Weapon switch did not cancel pose");
                    Check(visuals.WeaponModel == null || visuals.WeaponModel.transform.parent != sword.Socket, "Bow attached to sword socket");
                    equipment.SelectWeapon(EquipmentSlot.Weapon1);
                    Next();
                }
                else if (stage == 6 && elapsed > 0.65f)
                {
                    Check(Attack(), "Third attack rejected");
                    sword.enabled = false;
                    Check(visuals.WeaponSocket == null && animator.GetLayerWeight(1) == 0f, "Disable did not restore attachment/pose");
                    sword.enabled = true;
                    Check(visuals.WeaponSocket == sword.Socket, "Re-enable did not bind socket");
                    sword.GetComponent<Health>().TryApplyDamage(10000);
                    Check(!Attack(), "Dead player attacked");
                    Next();
                }
                else if (stage == 7 && elapsed > 0.15f)
                {
                    Check(!sword.IsSwinging && animator.GetLayerWeight(1) == 0f, "Death did not cancel pose");
                    Debug.Log("SWORD_PLAY_PASS: hand attachment, retargeting, immediate damage once, cooldown, UI block, moving attack, weapon switch, disable/re-enable, death");
                    Finish(0);
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"SWORD_PLAY_FAIL stage={stage}: {error}");
                Finish(1);
            }
        }

        private static bool Attack() => combat.TryAttack(Camera.main.WorldToScreenPoint(target.transform.position));
        private static void Next() { stage++; started = Time.time; }
        private static void Check(bool condition, string message) => PlayerVisualAuthoring.Require(condition, message);

        private static void Capture(string name)
        {
            var go = new GameObject("Sword Capture");
            var camera = go.AddComponent<Camera>();
            camera.transform.position = sword.transform.position + new Vector3(3f, 2f, 3f);
            camera.transform.LookAt(sword.transform.position + Vector3.up * 0.9f);
            var instructions = GameObject.Find("Instructions");
            if (instructions != null) instructions.SetActive(false);
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            var render = new RenderTexture(960, 720, 24);
            var image = new Texture2D(960, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Artifacts/sword");
                File.WriteAllBytes("Artifacts/sword/" + name, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (instructions != null) instructions.SetActive(true);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(render);
            }
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Session, false);
            EditorApplication.update -= Tick;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
