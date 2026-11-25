using System;
using System.Collections.Generic;
using System.Linq;
using Nomad.Game;
using Nomad.Hud;
using Nomad.Inventory;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Equipment.Editor
{
    // 실제 Game 씬의 카메라·HUD·무기에서 약공격/차징/강공격을 캡처합니다.
    public static class ChargeFeedbackVerification
    {
        private const string Key = "Nomad.ChargeFeedbackVerify";
        private static readonly string[] Weapons = { "weapon-shortbow", "weapon-apprentice-staff" };
        private static PlayerEquipmentCombat combat;
        private static PlayerInventory inventory;
        private static PlayerEquipment equipment;
        private static PlayerHud hud;
        private static Mouse mouse;

        private static int weaponIndex;
        private static int stage;
        private static int changedFrame;
        private static int frame;
        private static float firedAt;
        private static bool weakCaptured;
        private static double deadline;

        private static string unexpectedError;
        private static readonly List<InputDevice> mutedDevices = new();

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("배치 검증 전용입니다.");
            EditorSceneManager.OpenScene("Assets/Nomad/Game/Game.unity");
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, $"차징 화면 검증 시간 초과 stage={stage} charging={combat?.IsCharging} progress={combat?.ChargeProgress} gauge={hud?.ChargeFraction} scale={Time.timeScale}");
                Require(unexpectedError == null, "Unity 오류: " + unexpectedError);
                if (EditorApplication.isPaused)
                {
                    Debug.LogWarning("CHARGE_VERIFY_ENVIRONMENT: 전용 배치 Editor의 일시정지를 해제합니다.");
                    EditorApplication.isPaused = false;
                }
                if (!Application.isPlaying || Time.frameCount <= frame) return;
                frame = Time.frameCount;
                GameManager manager = GameManager.Instance;
                if (manager == null || manager.IsLoading || manager.Player == null || !manager.Player.activeInHierarchy) return;
                if (combat == null)
                {
                    Application.runInBackground = true;
                    Time.captureFramerate = 60;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse = InputSystem.AddDevice<Mouse>();
                    mouse.MakeCurrent();
                    // 다른 Editor에서 사람이 조작해도 이 배치 검증의 가상 입력을 빼앗지 않게 합니다.
                    foreach (InputDevice device in InputSystem.devices.Where(device => device != mouse && (device is Mouse || device is Keyboard)).ToArray())
                    {
                        InputSystem.DisableDevice(device);
                        mutedDevices.Add(device);
                    }
                    mouse.MakeCurrent();

                    combat = manager.Player.GetComponent<PlayerEquipmentCombat>();
                    combat.Fired += VerifyStaffShot;
                    inventory = manager.Player.GetComponent<PlayerInventory>();
                    equipment = manager.Player.GetComponent<PlayerEquipment>();
                    hud = manager.Player.GetComponentInChildren<PlayerHud>();

                    manager.Player.GetComponent<PlayerMovement>().enabled = false;
                    CharacterController controller = manager.Player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    manager.Player.transform.position = new Vector3(0, 0.05f, -24);
                    controller.enabled = true;

                    inventory.Add(PlayerEquipmentCombat.ArrowItemId, 30);
                    foreach (string id in Weapons) inventory.Add(id, 1);
                    changedFrame = frame;
                    return;
                }
                Vector2 pointer = Camera.main.WorldToScreenPoint(combat.transform.position + new Vector3(0, 1, 7));
                if (stage == 0)
                {
                    if (frame < changedFrame + 30) return;
                    int slot = Enumerable.Range(0, PlayerInventory.Capacity).First(index => inventory.GetSlot(index).Item?.ItemId == Weapons[weaponIndex]);
                    Require(equipment.TryEquip(slot, EquipmentSlot.Weapon1), "검토 무기 장착");
                    equipment.SelectWeapon(EquipmentSlot.Weapon1);
                    stage = 1;
                    return;
                }
                if (stage == 1)
                {
                    if (!combat.TryBeginCharge(pointer)) return;
                    Require(combat.TryReleaseCharge(pointer) && combat.LastShotPower == 1f, "좌클릭 짧게 누른 일반 위력 발사");
                    changedFrame = frame;
                    firedAt = Time.time;
                    weakCaptured = false;
                    stage = 2;
                    return;
                }
                if (stage == 2)
                {
                    if (!weakCaptured && frame >= changedFrame + 2) { Capture("weak"); weakCaptured = true; }
                    if (Time.time < firedAt + combat.CurrentWeapon.AttackInterval + 0.1f) return;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Left));
                    stage = 3;
                    return;
                }
                if (stage == 3)
                {
                    if (!combat.IsCharging || combat.ChargeProgress < 0.5f) return;
                    Require(hud.ChargeGaugeVisible && hud.ChargeFraction > 0.4f, "진행 게이지 표시");
                    Capture("charging");
                    changedFrame = frame;
                    stage = 4;
                    return;
                }
                if (stage == 4)
                {
                    Require(combat.IsCharging, "검토용 좌클릭 유지 중 차징 취소됨 currentMouse=" + Mouse.current?.name);
                    // Editor update는 HUD LateUpdate보다 먼저 올 수 있으므로 그리기 완료까지 기다립니다.
                    if (combat.ChargeProgress < 1 || hud.ChargeFraction < 0.999f || frame < changedFrame + 35) return;
                    Require(hud.ChargeGaugeVisible, "완충 게이지");
                    Require(combat.GetComponentInChildren<ChargeGatherVfx>().ParticleCount > 10, "차징 주변 입자 축적");
                    Capture("full");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                    changedFrame = frame;
                    stage = 5;
                    return;
                }
                if (frame < changedFrame + 3) return;
                Require(!combat.IsCharging && combat.LastShotPower == 2.5f && !hud.ChargeGaugeVisible, "놓기 발사·게이지 정리");
                Capture("strong");
                if (++weaponIndex < Weapons.Length) { stage = 0; changedFrame = frame; return; }
                Debug.Log("CHARGE_FEEDBACK_PLAY_PASS leftTap=1 full=2.5 gauge=pass bow,staff=pass captures=8");
                Finish(0);
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void Capture(string state) => Nomad.Minimap.Editor.MinimapVerification.Capture("issue210-main-" + weaponIndex + "-" + state + ".png");
        private static void VerifyStaffShot(Nomad.Combat.Projectile shot, ItemDefinition weapon)
        {
            if (weapon.AttackMode != WeaponAttackMode.Staff) return;
            Transform tip = combat.GetComponent<EquipmentCombatVisuals>().WeaponModel.GetComponentsInChildren<Transform>()
                .Single(node => node.name == "Staff Tip Muzzle");
            Transform right = combat.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            Require(tip.IsChildOf(right) && Vector3.Distance(shot.transform.position, combat.ProjectileOrigin) < 0.01f,
                "실제 마법탄은 마지막으로 표시한 오른손 지팡이 끝에서 발사");
            if (combat.LastShotPower > 1.1f)
                Require(combat.IsCharging, "차징 자세를 해제하기 전에 지팡이 끝에서 발사");
        }
        private static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && trace.Contains("UnityEditor.Search.SearchDatabase") && !trace.Contains("Nomad.")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) unexpectedError = message;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (combat != null) combat.Fired -= VerifyStaffShot;
            foreach (InputDevice device in mutedDevices) InputSystem.EnableDevice(device);
            mutedDevices.Clear();
            EditorApplication.Exit(code);
        }
    }
}
