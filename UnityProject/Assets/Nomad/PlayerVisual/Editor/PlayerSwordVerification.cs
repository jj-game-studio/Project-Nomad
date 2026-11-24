using System;
using System.Collections;
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
        private static IEnumerator scenario;
        private static Key[] keys = Array.Empty<Key>();
        private static int frame, attacks, impacts, fired, beforeHits;
        private static float waitUntil, testTime, requestAt, strikeAt, strikePose;
        private static double deadline;
        private static string step;
        private static bool loopStarted;

        public static void Run()
        {
            EditorSceneManager.OpenScene(PlayerSwordAuthoring.ScenePath);
            // 과거 Variant의 중복 외형 대신 실제 메인 PlayerRig로 검증합니다. 테스트 바닥·벽·표적은 유지합니다.
            var previous = Object.FindAnyObjectByType<PlayerAnimation>();
            Object.DestroyImmediate(previous.transform.root.gameObject);
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVisualAuthoring.BaseRigPath));
            SessionState.SetBool(Session, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Session, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Bootstrap;
            EditorApplication.update += Bootstrap;
        }

        private static void Bootstrap()
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Debug.LogError($"SWORD_PLAY_FAIL timeout: {step}");
                Finish(1);
                return;
            }
            if (!Application.isPlaying || loopStarted) return;
            var loop = new GameObject("Sword Verification Loop").AddComponent<PlayerSwordVerificationLoop>();
            loop.Input = () =>
            {
                if (keyboard != null) InputState.Change(keyboard, new KeyboardState(keys), InputUpdateType.Dynamic);
            };
            loop.Tick = Tick;
            loopStarted = true;
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, $"Timeout: {step}");
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
                    combat.Attacked += _ =>
                    {
                        attacks++;
                        strikeAt = Time.time;
                        strikePose = sword.GetComponent<PlayerAnimation>().Animator.GetCurrentAnimatorStateInfo(1).normalizedTime;
                    };
                    combat.WeaponImpacted += (_, _) => impacts++;
                    combat.Fired += (_, _) => fired++;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    scenario = Verify();
                }

                // captureFramerate의 게임 시간과 실제 벽시계 시간을 섞지 않습니다.
                testTime += Time.timeScale > 0f ? Time.deltaTime : 1f / 60f;
                if (testTime < waitUntil) return;
                if (!scenario.MoveNext())
                {
                    Debug.Log("SWORD_PLAY_PASS: delayed hit/VFX once, normalized strike pose, cooldown, moving attack, pause, switch, UI/trade, roll, disable, wall/range recheck, bow/unarmed fallback, death");
                    Finish(0);
                    return;
                }
                waitUntil = testTime + (float)scenario.Current;
            }
            catch (Exception error)
            {
                Debug.LogError($"SWORD_PLAY_FAIL step={step}: {error}");
                Finish(1);
            }
        }

        private static IEnumerator Verify()
        {
            yield return 0.5f;
            var animator = sword.GetComponent<PlayerAnimation>().Animator;
            Check(combat.CurrentWeapon?.ItemId == PlayerSwordAnimation.SwordId, "Starter sword missing");
            Check(sword.Socket?.parent == animator.GetBoneTransform(HumanBodyBones.RightHand), "Right hand socket missing");
            Check(visuals.WeaponModel?.transform.parent == sword.Socket, "Sword not attached to hand");
            Check(visuals.WeaponModel.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Equipped collider enabled");
            Capture("sword-idle.png");

            Begin("delayed strike");
            Check(!Attack(), "Duplicate request accepted during preparation");
            Check(target.CurrentHealth == 100 && attacks == beforeHits && SlashCount() == 0, "Hit/VFX occurred before windup");
            Quaternion hand = sword.Socket.parent.rotation;
            yield return 0.08f;
            Check(target.CurrentHealth == 100 && SlashCount() == 0, "Early windup caused damage/VFX");
            Check(sword.IsSwinging && Quaternion.Angle(hand, sword.Socket.parent.rotation) > 5f, "Retargeted preparation pose missing");
            Capture("sword-windup.png");
            yield return 0.17f;
            Check(target.CurrentHealth == 75 && attacks == beforeHits + 1 && impacts == 1 && SlashCount() == 1, $"Expected one delayed damage/VFX: hp={target.CurrentHealth}, attacks={attacks}/{beforeHits}, impacts={impacts}, slash={SlashCount()}, pending={combat.IsMeleePending}, time={Time.time-requestAt:F3}, pose={animator.GetCurrentAnimatorStateInfo(1).normalizedTime:F3}");
            Check(Mathf.Abs((strikeAt - requestAt) - 0.192f) < 0.035f && Mathf.Abs(strikePose - PlayerSwordAnimation.StrikeNormalizedTime) < 0.07f,
                $"Timing mismatch: delay={strikeAt-requestAt:F3}, normalizedPose={strikePose:F3}");
            Debug.Log($"SWORD_STRIKE delay={strikeAt-requestAt:F3}, normalizedPose={strikePose:F3}");
            Capture("sword-attack.png");
            yield return 0.5f;
            Check(target.CurrentHealth == 75 && attacks == beforeHits + 1 && !sword.IsSwinging, "Duplicate hit or unfinished recovery");

            Begin("moving attack");
            Vector3 position = sword.transform.position;
            keys = new[] { Key.A };
            yield return 0.25f;
            Check(Vector3.Distance(position, sword.transform.position) > 0.2f, "Attack blocked movement");
            Check(sword.GetComponent<PlayerAnimation>().CurrentState == "Run" && Mathf.Abs(animator.speed - 1f) < 0.001f,
                "Locomotion changed attack playback speed");
            Check(attacks == beforeHits + 1, "Moving attack did not strike");
            Capture("sword-moving.png");
            keys = Array.Empty<Key>();
            yield return 0.5f;
            MoveTo(Vector3.zero);
            yield return 0.1f;

            Begin("pause freezes windup");
            yield return 0.05f;
            Time.timeScale = 0f;
            yield return 0.3f;
            Check(combat.IsMeleePending && target.CurrentHealth == 100 && attacks == beforeHits, "Paused preparation struck");
            Time.timeScale = 1f;
            yield return 0.25f;
            Check(attacks == beforeHits + 1 && target.CurrentHealth == 75, "Resume did not strike once");
            yield return 0.5f;

            Begin("switch away and back cancels");
            equipment.SelectWeapon(EquipmentSlot.Weapon2);
            equipment.SelectWeapon(EquipmentSlot.Weapon1);
            yield return 0.3f;
            NoHit();
            yield return 0.4f;

            Begin("public input gate cancels");
            combat.InputBlocked = true;
            combat.InputBlocked = false;
            yield return 0.3f;
            NoHit();
            yield return 0.4f;

            Begin("equipment UI cancels");
            typeof(PlayerEquipmentCombat).GetProperty("EquipmentUiOpen").SetValue(combat, true);
            yield return 0.3f;
            NoHit();
            typeof(PlayerEquipmentCombat).GetProperty("EquipmentUiOpen").SetValue(combat, false);
            yield return 0.4f;

            Begin("trade cancels");
            var trading = typeof(PlayerInventory).GetMethod("SetTrading", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            trading.Invoke(sword.GetComponent<PlayerInventory>(), new object[] { true });
            yield return 0.3f;
            NoHit();
            trading.Invoke(sword.GetComponent<PlayerInventory>(), new object[] { false });
            yield return 0.4f;

            Begin("roll cancels");
            keys = new[] { Key.LeftShift, Key.A };
            yield return 0.1f;
            Check(sword.GetComponent<PlayerMovement>().IsRolling, $"Roll input did not start: pos={sword.transform.position}, pose={sword.GetComponent<PlayerAnimation>().CurrentState}, shift={Keyboard.current?.leftShiftKey.isPressed}, grounded={sword.GetComponent<CharacterController>().isGrounded}");
            keys = Array.Empty<Key>();
            yield return 0.25f;
            NoHit();
            yield return 0.7f;
            MoveTo(Vector3.zero);
            yield return 0.1f;

            Begin("module disable cancels");
            sword.enabled = false;
            yield return 0.3f;
            NoHit();
            Check(visuals.WeaponSocket == null, "Disabled module retained socket");
            sword.enabled = true;
            yield return 0.4f;

            Begin("combat disable cancels");
            combat.enabled = false;
            yield return 0.3f;
            NoHit();
            combat.enabled = true;
            yield return 0.4f;

            Begin("wall rechecked at strike");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, 0.7f);
            wall.transform.localScale = new Vector3(3f, 2f, 0.15f);
            Physics.SyncTransforms();
            yield return 0.3f;
            Check(target.CurrentHealth == 100 && attacks == beforeHits + 1, "Windup used stale wall test");
            Object.Destroy(wall);
            yield return 0.4f;

            Begin("range rechecked at strike");
            target.transform.position = new Vector3(0f, 0.9f, 8f);
            Physics.SyncTransforms();
            yield return 0.3f;
            Check(target.CurrentHealth == 100 && attacks == beforeHits + 1, "Windup used stale range test");
            target.transform.position = new Vector3(0f, 0.9f, 1.5f);
            Physics.SyncTransforms();
            yield return 0.4f;

            step = "module absent keeps immediate sword";
            sword.enabled = false;
            target.RestoreFull();
            beforeHits = attacks;
            Check(Attack() && !combat.IsMeleePending && target.CurrentHealth == 75 && attacks == beforeHits + 1, "Legacy immediate melee changed");
            sword.enabled = true;
            yield return 0.7f;

            step = "bow stays immediate";
            equipment.SelectWeapon(EquipmentSlot.Weapon2);
            int arrows = combat.ArrowCount;
            int shots = fired;
            float bowInterval = combat.CurrentWeapon.AttackInterval;
            beforeHits = attacks;
            Check(Attack() && !combat.IsMeleePending && combat.ArrowCount == arrows - 1 && fired == shots + 1 && attacks == beforeHits + 1,
                "Bow gained windup or ammo changed");
            equipment.SelectWeapon(EquipmentSlot.Weapon1);
            yield return bowInterval + 0.1f;

            step = "unarmed stays immediate";
            Check(equipment.TryUnequip(EquipmentSlot.Weapon1), "Could not unequip sword");
            target.RestoreFull();
            target.transform.position = new Vector3(0f, 0.9f, 0.8f);
            Physics.SyncTransforms();
            beforeHits = attacks;
            Check(Attack() && !combat.IsMeleePending && attacks == beforeHits + 1 && target.CurrentHealth < 100, $"Unarmed regression: weapon={combat.CurrentWeapon?.ItemId}, pending={combat.IsMeleePending}, attacks={attacks}/{beforeHits}, hp={target.CurrentHealth}");
            var inventory = sword.GetComponent<PlayerInventory>();
            int slot = Enumerable.Range(0, PlayerInventory.Capacity).First(i => inventory.GetSlot(i).Item?.ItemId == PlayerSwordAnimation.SwordId);
            Check(equipment.TryEquip(slot, EquipmentSlot.Weapon1), "Could not restore sword");
            target.transform.position = new Vector3(0f, 0.9f, 1.5f);
            Physics.SyncTransforms();
            yield return 0.7f;

            Begin("death cancels");
            sword.GetComponent<Health>().TryApplyDamage(10000);
            yield return 0.3f;
            NoHit();
            Check(!Attack() && !sword.IsSwinging && animator.GetLayerWeight(1) == 0f, "Dead player attacked or kept pose");
        }

        private static int SlashCount() => Object.FindObjectsByType<SwordSlashVfx>(FindObjectsSortMode.None).Length;
        private static bool Attack() => combat.TryAttack(Camera.main.WorldToScreenPoint(target.transform.position));
        private static void Begin(string name)
        {
            step = name;
            Debug.Log($"SWORD_CASE {name}");
            target.RestoreFull();
            beforeHits = attacks;
            requestAt = Time.time;
            Check(Attack() && combat.IsMeleePending, $"Preparation not started: {name}");
        }
        private static void NoHit() => Check(!combat.IsMeleePending && attacks == beforeHits && target.CurrentHealth == 100 && !sword.IsSwinging,
            $"Cancelled attack still hit or kept pose: {step}");
        private static void Check(bool condition, string message) => PlayerVisualAuthoring.Require(condition, message);
        private static void MoveTo(Vector3 position)
        {
            var controller = sword.GetComponent<CharacterController>();
            controller.enabled = false;
            sword.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

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
            Time.timeScale = 1f;
            SessionState.SetBool(Session, false);
            EditorApplication.update -= Bootstrap;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
