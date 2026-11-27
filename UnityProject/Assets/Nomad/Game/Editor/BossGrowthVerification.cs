using System;
using System.Collections.Generic;
using System.Linq;
using Nomad.Combat;
using Nomad.CombatFeedback;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.Game.Editor
{
    public static class BossGrowthVerification
    {
        private const string Key = "Nomad.BossGrowthVerify";
        private static readonly DragonPattern[] Patterns = { DragonPattern.Circle, DragonPattern.RadialFireballs,
            DragonPattern.Breath, DragonPattern.FirePool, DragonPattern.Rain, DragonPattern.Charge };
        private static readonly float[] CaptureAt = { 2.3f, 1.48f, 2.4f, 1.9f, 6.5f, 2.15f };
        private static readonly List<InputDevice> muted = new();
        private static DragonBossCombat boss;
        private static BossArena arena;
        private static PlayerEquipmentCombat player;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static int stage, pattern, frame;
        private static float since;
        private static bool requestedField;
        private static int rocksBeforeParry;
        private static bool chargeTellCaptured;
        private static bool arrivalWarningCaptured, arrivalFallCaptured, deathCaptured;
        private static string error;
        private static double deadline;

        public static void Run() => Begin(false);
        public static void RunTerrain() => Begin(true);
        public static void RunDebug() => Begin(false, true);
        private static void Begin(bool terrain, bool debug = false)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("전용 배치 검증입니다.");
            EditorSceneManager.OpenScene(debug ? BossDebugAuthoring.ScenePath : terrain ? "Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity" : "Assets/Nomad/Game/Game.unity");
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".terrain", terrain);
            SessionState.SetBool(Key + ".debug", debug);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "보스 검증 시간 초과 stage=" + stage);
                Require(error == null, "Unity 오류: " + error);
                if (!Application.isPlaying || Time.frameCount == frame) return;
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                frame = Time.frameCount;
                bool terrain = SessionState.GetBool(Key + ".terrain", false);
                bool debug = SessionState.GetBool(Key + ".debug", false);
                if (debug && Object.FindFirstObjectByType<BossDebugBootstrap>()?.Ready != true) return;
                if (stage == 9)
                {
                    if (Time.time < since + 0.2f) return;
                    var restarted = Object.FindFirstObjectByType<BossDebugBootstrap>();
                    Require(restarted.Ready && restarted.Boss.GetComponent<BossArmor>().BreakCount == 0 &&
                        restarted.Arena.RockCount == BossArena.InitialRockCount && restarted.Boss.GetComponent<Health>().CurrentHealth == BossArmor.ReferenceHealth,
                        "실제 R 입력으로 보스/갑옷/바위 재생성");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Debug.Log("BOSS_DEBUG_RESTART_PASS");
                    Finish(0);
                    return;
                }
                if (stage == 11)
                {
                    if (boss.Phase == DragonPhase.Stunned && boss.Remaining > 0) return;
                    boss.GetComponent<NavMeshAgent>().Warp(arena.Center);
                    MovePlayer(arena.Center - arena.Forward * 7);
                    Health health = boss.GetComponent<Health>();
                    health.TryApplyDamage(health.CurrentHealth - health.MaxHealth / 4, DamageKind.Fall);
                    Require(boss.Enraged && boss.GetComponent<BossVisualStyle>().EyesBurning, "25% 광폭/붉은 눈");
                    Vector3 pointer = Camera.main.WorldToScreenPoint(boss.GetComponent<Collider>().bounds.center);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                    since = Time.time;
                    stage = 16;
                    return;
                }
                if (stage == 16)
                {
                    Vector3 pointer = Camera.main.WorldToScreenPoint(boss.GetComponent<Collider>().bounds.center);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                    if (Time.time < since + 0.8f) return;
                    Require(boss.GetComponent<BossVisualStyle>().Hovered, "실제 커서 레이/보스 피격체 붉은 윤곽");
                    Capture("HoverOutline");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width - 20, Screen.height / 2) });
                    since = Time.time;
                    stage = 17;
                    return;
                }
                if (stage == 17)
                {
                    if (Time.time < since + 0.12f) return;
                    Capture("EnragedEyes");
                    boss.BeginPattern(DragonPattern.Circle);
                    rocksBeforeParry = arena.RockCount;
                    since = Time.time;
                    stage = 12;
                    return;
                }
                if (stage == 12)
                {
                    var signal = boss.GetComponent<BossParrySignal>();
                    if (!signal.Flashing) return;
                    Capture("SlamYellowFlash");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.F));
                    since = Time.time;
                    stage = 13;
                    return;
                }
                if (stage == 13)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (Time.time < since + 0.9f) return;
                    Require(boss.ParrySuccesses == 2 && arena.RockCount - rocksBeforeParry >= 2, "실제 F 강타 패링/바위 생성");
                    Capture("ParryRocksEnraged");
                    boss.BeginPattern(DragonPattern.RadialFireballs);
                    since = Time.time;
                    stage = 14;
                    return;
                }
                if (stage == 14)
                {
                    if (!boss.GetComponent<BossParrySignal>().Flashing) return;
                    player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(boss.transform.position - player.transform.position, Vector3.up));
                    Capture("RadialYellowFlash");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.F));
                    since = Time.time;
                    stage = 15;
                    return;
                }
                if (stage == 15)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (Time.time < since + 0.9f) return;
                    Require(boss.ParrySuccesses == 3 && player.GetComponent<PlayerBossParry>().Successes == 3,
                        "실제 F 구체 반사/단일 반사 boss=" + boss.ParrySuccesses + " player=" + player.GetComponent<PlayerBossParry>().Successes);
                    Capture("RadialParried");
                    Debug.Log("BOSS_PARRY_PLAY_PASS yellow=pass F=pass slam=pass radial=pass rocks=pass enrage=pass");
                    boss.GetComponent<Health>().TryApplyDamage(BossArmor.ReferenceHealth * 2);
                    since = Time.time;
                    stage = 10;
                    return;
                }
                if (stage == 10)
                {
                    if (Time.time < since + 0.2f) return;
                    Require(boss.GetComponent<BossDeathPresentation>().Playing &&
                        boss.GetComponentInChildren<Animator>().GetCurrentAnimatorClipInfo(0).First().clip.name == "BossDeath", "실제 전용 사망 클립");
                    if (!deathCaptured) { Capture("DeathStart"); deathCaptured = true; }
                    if (Time.time < since + 2.8f) return;
                    Capture("DeathEnd");
                    Debug.Log("BOSS_LIFECYCLE_PASS scene=" + (debug ? "BossDebug" : terrain ? "TerrainIntegration" : "Game/Field") + " hiddenArrival=pass death=pass");
                    if (debug)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.R));
                        stage = 9;
                        since = Time.time;
                    }
                    else Finish(0);
                    return;
                }
                if (!terrain && !debug)
                {
                    GameManager manager = GameManager.Instance;
                    if (manager == null || manager.IsLoading) return;
                    if (!requestedField) { requestedField = manager.LoadRegion("Field"); return; }
                }
                if (boss == null)
                {
                    boss = Object.FindFirstObjectByType<DragonBossCombat>();
                    if (boss == null) return;
                    boss.AutomaticPatterns = false;
                    arena = Object.FindFirstObjectByType<BossArena>();
                    Require(boss.GetComponent<Health>().MaxHealth == BossArmor.ReferenceHealth, "실제 씬 보스 체력");
                    player = Object.FindFirstObjectByType<PlayerEquipmentCombat>();
                    if (player == null) return;
                    Application.runInBackground = true;
                    Time.captureFramerate = 60;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse = InputSystem.AddDevice<Mouse>();
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    foreach (InputDevice device in InputSystem.devices.Where(device => device != mouse && device != keyboard && (device is Keyboard || device is Mouse)).ToArray())
                    { InputSystem.DisableDevice(device); muted.Add(device); }
                    mouse.MakeCurrent();
                    keyboard.MakeCurrent();
                    player.GetComponent<Nomad.Player.PlayerMovement>().enabled = false;
                    player.GetComponent<Health>().IsCombatImmune = true;
                    foreach (MonsterIdentity actor in Object.FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None))
                        if (actor.Rank != MonsterRank.Boss) actor.GetComponent<Health>().TryApplyDamage(int.MaxValue);
                    Require(!arena.IsLocked && arena.Connected && arena.RockCount == BossArena.InitialRockCount, "입장 해금·경로·초기 바위 8개");
                    PlayerInventory bag = player.GetComponent<PlayerInventory>();
                    bag.Add("weapon-apprentice-staff", 2);
                    int slot = Enumerable.Range(0, bag.SlotCount).First(index => bag.GetSlot(index).Item?.ItemId == "weapon-apprentice-staff");
                    PlayerEquipment gear = player.GetComponent<PlayerEquipment>();
                    Require(gear.TryEquip(slot, EquipmentSlot.Weapon1) && gear.TryEnhance(EquipmentSlot.Weapon1), "실제 시작 장비에서 강화");
                    player.GetComponent<PlayerQuickSlots>()?.RegisterEquipped(EquipmentSlot.Weapon1, 0);
                    MovePlayer(arena.Center - arena.Forward * 7);
                    boss.Stun(0.03f);
                    since = Time.time;
                    return;
                }
                if (stage == 0)
                {
                    if (!boss.Introduced)
                    {
                        var arrival = boss.GetComponent<BossArrival>();
                        Require(!boss.GetComponent<BossBattleHud>().Visible, "낙하 착지 전 보스 체력 숨김");
                        if (arrival.Started && !arrival.BodyVisible && !arrivalWarningCaptured)
                        { Capture("ArrivalWarning"); arrivalWarningCaptured = true; }
                        if (arrival.BodyVisible && !arrival.Landed && !arrivalFallCaptured)
                        { Capture("ArrivalFall"); arrivalFallCaptured = true; }
                        return;
                    }
                    Require(arrivalWarningCaptured && arrivalFallCaptured, "실제 경고 후 낙하 등장");
                    if (Time.time < since + 0.1f) return;
                    boss.GetComponent<NavMeshAgent>().Warp(arena.Center);
                    MovePlayer(arena.Center - arena.Forward * 7);
                    if (Patterns[pattern] == DragonPattern.Charge)
                    {
                        BossBreakableRock rock = arena.GetComponentsInChildren<BossBreakableRock>().First();
                        Vector3 groundPoint = arena.GroundPoint(rock.GetComponentInChildren<BoxCollider>().bounds.center);
                        Vector3 towardCenter = Vector3.ProjectOnPlane(arena.Center - groundPoint, Vector3.up).normalized;
                        Require(boss.GetComponent<NavMeshAgent>().Warp(groundPoint + towardCenter * 4), "관찰용 돌진 시작 위치");
                        MovePlayer(groundPoint - towardCenter * 8);
                    }
                    Physics.SyncTransforms();
                    boss.BeginPattern(Patterns[pattern]);
                    chargeTellCaptured = false;
                    Require(boss.Phase == DragonPhase.Windup, "실제 패턴 시작 " + Patterns[pattern]);
                    since = Time.time;
                    stage = 1;
                    return;
                }
                if (stage == 1)
                {
                    if (Patterns[pattern] == DragonPattern.Circle && !chargeTellCaptured && Time.time >= since + 1.45f)
                    {
                        Require(boss.GetComponent<BossJumpSlam>().Airborne, "예고 완료 후 실제 도약");
                        Capture("CircleLeap");
                        chargeTellCaptured = true;
                    }
                    if (Patterns[pattern] == DragonPattern.RadialFireballs && !chargeTellCaptured && Time.time >= since + 0.9f)
                    {
                        Require(boss.GetComponent<BossRadialChargePose>().Weight > 0.3f, "고개를 들고 화염 기운 모으기");
                        Capture("RadialGather");
                        chargeTellCaptured = true;
                    }
                    if (Patterns[pattern] == DragonPattern.Charge && !chargeTellCaptured && Time.time >= since + 0.5f)
                    {
                        Require(boss.Phase == DragonPhase.Windup && !boss.ChargeAimLocked, "돌진 추적 전조 확인");
                        Capture("ChargeTell");
                        chargeTellCaptured = true;
                    }
                    if (Time.time < since + (Patterns[pattern] == DragonPattern.Charge ? 3.35f : CaptureAt[pattern])) return;
                    if (Patterns[pattern] == DragonPattern.Circle)
                        Require(boss.GetComponent<BossJumpSlam>().Landings == 1 && boss.Effects.GetComponentInChildren<BossGroundCracks>() != null,
                            "착지 1회·균열 생성");
                    if (Patterns[pattern] == DragonPattern.Charge) Require(boss.RockStuns == 1, "실제 바위 충돌 기절");
                    if (Patterns[pattern] == DragonPattern.Breath)
                    {
                        BossHazard breath = boss.Effects.GetComponentInChildren<BossHazard>();
                        Require(boss.Mouth.parent.name == "jaw_upper" && breath.BreathOrigin == boss.Mouth &&
                            Vector3.Distance(breath.LastBreathOrigin, boss.Mouth.position) < 0.1f, "현재 실제 머리에서 브레스");
                    }
                    if (Patterns[pattern] == DragonPattern.Rain) Require(boss.RainProjectilesSpawned >= 45, "길게 이어지는 감량 낙하 패턴");
                    Capture(Patterns[pattern].ToString());
                    boss.Stun(0.03f);
                    since = Time.time;
                    stage = ++pattern < Patterns.Length ? 0 : 2;
                    return;
                }
                if (stage == 2)
                {
                    boss.GetComponent<Health>().TryApplyCombatImpact(140, false, CombatImpactSource.ParriedFireball);
                    since = Time.time;
                    stage = 3;
                    return;
                }
                if (stage == 3)
                {
                    if (Time.time < since + 0.65f) return;
                    Require(boss.GetComponent<BossArmor>().BreakCount == 1 && boss.Phase == DragonPhase.Stunned, "내구도 파괴·녹다운");
                    Require(boss.GetComponentInChildren<Animator>().GetCurrentAnimatorClipInfo(0).First().clip.name == "BossKnockdown", "사망과 별개 녹다운 클립");
                    Require(!boss.GetComponentsInChildren<TextMesh>(true).Any(text => text.text.Contains("기절")), "기절 글자 제거");
                    Capture("ArmorBreak");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Tab));
                    since = Time.time;
                    stage = 4;
                    return;
                }
                if (stage == 4)
                {
                    if (Time.time < since + 0.1f) return;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Require(player.GetComponent<EquipmentPanel>().IsOpen, "강화 UI 열림");
                    Capture("EnhancementUI");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Tab));
                    MovePlayer(boss.transform.position - boss.transform.forward * 6);
                    since = Time.time;
                    stage = 5;
                    return;
                }
                if (stage == 5)
                {
                    if (Time.time < since + 0.2f) return;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Vector2 pointer = Camera.main.WorldToScreenPoint(boss.transform.position + Vector3.up);
                    Require(player.TryUseSkill(pointer), $"실제 강화 지팡이 스킬 weapon={player.CurrentWeapon?.ItemId}+{player.CurrentWeapon?.EnhancementLevel} allowed={player.CanParry} aim={player.TryGetAim(pointer, out _)} pointer={pointer}");
                    Capture("EnhancedSkill");
                    player.GetComponent<PlayerStats>().RandomSource = () => 0.01f;
                    int criticalDamage = player.GetComponent<PlayerStats>().RollOutgoingDamage(player.CurrentWeapon, out bool critical);
                    boss.GetComponent<Health>().TryApplyCombatDamage(criticalDamage, critical);
                    Require(critical && boss.GetComponent<CombatFeedbackOverlay>().LastCritical, "치명타 숫자 표시");
                    Capture("BossHit");
                    player.GetComponent<Health>().IsCombatImmune = false;
                    player.GetComponent<PlayerStats>().RandomSource = () => 0.99f;
                    Require(player.GetComponent<Health>().TryApplyDamage(18), "플레이어 피격");
                    since = Time.time;
                    stage = 6;
                    return;
                }
                if (Time.time < since + 0.08f) return;
                if (stage == 6)
                {
                    var feedback = player.GetComponent<CombatFeedbackOverlay>();
                    Require(feedback != null && feedback.LastDamage > 0 && feedback.VignetteAlpha > 0 &&
                        player.GetComponent<PlayerDamageFeedback>().Reacting, "실제 피해 숫자·비네트·피격 반동");
                    Capture("PlayerHurt");
                    boss.GetComponent<BossArmor>().DamageArmor(BossArmor.DurabilityPerLayer);
                    stage = 7;
                    since = Time.time;
                    return;
                }
                if (stage == 8)
                {
                    Require(player.GetComponent<PlayerBossParry>().Reflections == 1, "실제 F 입력으로 탄 하나만 패링");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Capture("Parry");
                    Debug.Log("BOSS_GROWTH_PLAY_PASS scene=" + (debug ? "BossDebug" : terrain ? "TerrainIntegration" : "Game/Field") + " patterns=6 initialRocks=8 rockStun=1 armorBreak=2 knockdown=pass enhance=1 skill=pass playerHurt=pass parry=1");
                    stage = 11;
                    since = Time.time;
                    return;
                }
                if (Time.time < since + 0.65f) return;
                Require(boss.GetComponent<BossArmor>().BreakCount == 2, "실제 씬 갑옷 2차 파괴");
                Capture("ArmorBreak2");
                player.GetComponent<Health>().IsCombatImmune = true;
                Vector3 parryPoint = player.transform.position + Vector3.up * 1.3f + player.transform.forward * 1.1f;
                for (int index = 0; index < 2; index++)
                    BossFireball.Launch(boss.Effects, boss.transform, player.GetComponent<Health>(), parryPoint + player.transform.right * (index * 0.15f),
                        player.transform.position + Vector3.up, 0.8f, false);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.F));
                stage = 8;
                since = Time.time;
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static void MovePlayer(Vector3 point)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            point = arena.GroundPoint(point);
            player.transform.position = point + Vector3.up * 0.04f;
            controller.enabled = true;
            controller.Move(Vector3.down * 0.04f);
        }

        private static void Capture(string name)
        {
            Camera camera = Camera.main;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize;
            var impulse = camera.GetComponent<HitCameraImpulse>();
            bool shake = impulse != null && impulse.enabled;
            // 정지 화면에서는 카메라 충격이 캡처용 ScreenSpaceCamera HUD를 밀지 않게 합니다.
            if (shake) impulse.enabled = false;
            try
            {
                camera.transform.position = arena.Center + Quaternion.LookRotation(arena.Forward) * new Vector3(23, 36, -30);
                camera.transform.LookAt(arena.Center);
                camera.orthographicSize = 27;
                if (name == "PlayerHurt")
                {
                    camera.transform.position = player.transform.position + new Vector3(5, 7, -7);
                    camera.transform.LookAt(player.transform.position + Vector3.up);
                    camera.orthographicSize = 4;
                }
                foreach (CombatFeedbackOverlay overlay in Object.FindObjectsByType<CombatFeedbackOverlay>(FindObjectsSortMode.None))
                    overlay.RefreshPositions(camera);
                string prefix = SessionState.GetBool(Key + ".debug", false) ? "debug-" : SessionState.GetBool(Key + ".terrain", false) ? "terrain-" : "main-";
                Nomad.Minimap.Editor.MinimapVerification.Capture("issue214-" + prefix + name + ".png");
                if (name == "Breath")
                {
                    Vector3 point = boss.Mouth.position + boss.transform.forward;
                    camera.transform.position = point + boss.transform.rotation * new Vector3(6, 3, -5);
                    camera.transform.LookAt(point);
                    camera.orthographicSize = 4;
                    Nomad.Minimap.Editor.MinimapVerification.Capture("issue214-" + prefix + "MouthBreathClose.png");
                }
                if (name == "EnragedEyes")
                {
                    Vector3 point = boss.Mouth.position + Vector3.up * 0.2f;
                    camera.transform.position = point + boss.transform.rotation * new Vector3(3, 1, 4);
                    camera.transform.LookAt(point);
                    camera.orthographicSize = 2.7f;
                    Nomad.Minimap.Editor.MinimapVerification.Capture("issue214-" + prefix + "EnragedEyesClose.png");
                }
                if (name == "ArmorBreak" || name == "RadialGather" || name == "HoverOutline" || name.Contains("YellowFlash") || name == "ParryRocksEnraged" || name.StartsWith("Death"))
                {
                    camera.transform.position = boss.transform.position + boss.transform.rotation * new Vector3(7, 4, -6);
                    camera.transform.LookAt(boss.transform.position + Vector3.up);
                    foreach (CombatFeedbackOverlay overlay in Object.FindObjectsByType<CombatFeedbackOverlay>(FindObjectsSortMode.None)) overlay.RefreshPositions(camera);
                    Nomad.Minimap.Editor.MinimapVerification.Capture("issue214-" + prefix + (name == "ArmorBreak" ? "KnockdownClose" : name + "Close") + ".png");
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size;
                if (shake && impulse != null) impulse.enabled = true;
            }
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && trace.Contains("UnityEditor.Search.SearchDatabase") && !trace.Contains("Nomad.")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message;
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            foreach (InputDevice device in muted) InputSystem.EnableDevice(device);
            EditorApplication.Exit(code);
        }
    }
}
