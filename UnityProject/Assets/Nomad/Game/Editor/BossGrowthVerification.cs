using System;
using System.Collections.Generic;
using System.Linq;
using Nomad.Combat;
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
        private static readonly DragonPattern[] Patterns = { DragonPattern.Circle, DragonPattern.TripleFireball,
            DragonPattern.Breath, DragonPattern.FirePool, DragonPattern.Rain, DragonPattern.Charge };
        private static readonly float[] CaptureAt = { 1.1f, 1.25f, 2.1f, 2.4f, 2.6f, 1.7f };
        private static readonly List<InputDevice> muted = new();
        private static DragonBossCombat boss;
        private static BossArena arena;
        private static PlayerEquipmentCombat player;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static int stage, pattern, frame;
        private static float since;
        private static bool requestedField;
        private static string error;
        private static double deadline;

        public static void Run() => Begin(false);
        public static void RunTerrain() => Begin(true);
        private static void Begin(bool terrain)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("전용 배치 검증입니다.");
            EditorSceneManager.OpenScene(terrain ? "Assets/Nomad/Terrain/Wfc/TerrainIntegration.unity" : "Assets/Nomad/Game/Game.unity");
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".terrain", terrain);
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
                if (!terrain)
                {
                    GameManager manager = GameManager.Instance;
                    if (manager == null || manager.IsLoading) return;
                    if (!requestedField) { requestedField = manager.LoadRegion("Field"); return; }
                }
                if (boss == null)
                {
                    boss = Object.FindFirstObjectByType<DragonBossCombat>();
                    if (boss == null) return;
                    arena = Object.FindFirstObjectByType<BossArena>();
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
                    player.GetComponent<Health>().IsCombatImmune = true;
                    foreach (MonsterIdentity actor in Object.FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None))
                        if (actor.Rank != MonsterRank.Boss) actor.GetComponent<Health>().TryApplyDamage(int.MaxValue);
                    Require(!arena.IsLocked && arena.Connected && arena.RockCount == 6, "입장 해금·경로·바위 6개");
                    PlayerInventory bag = player.GetComponent<PlayerInventory>();
                    bag.Add("weapon-apprentice-staff", 2);
                    int slot = Enumerable.Range(0, bag.SlotCount).First(index => bag.GetSlot(index).Item?.ItemId == "weapon-apprentice-staff");
                    PlayerEquipment gear = player.GetComponent<PlayerEquipment>();
                    Require(gear.TryEquip(slot, EquipmentSlot.Weapon1) && gear.TryEnhance(EquipmentSlot.Weapon1), "실제 시작 장비에서 강화");
                    MovePlayer(arena.Center - arena.Forward * 7);
                    boss.Stun(0.03f);
                    since = Time.time;
                    return;
                }
                if (stage == 0)
                {
                    if (Time.time < since + 0.1f) return;
                    boss.GetComponent<NavMeshAgent>().Warp(arena.Center);
                    MovePlayer(arena.Center - arena.Forward * 7);
                    if (Patterns[pattern] == DragonPattern.Charge)
                    {
                        BossBreakableRock rock = arena.GetComponentsInChildren<BossBreakableRock>().First();
                        boss.GetComponent<NavMeshAgent>().Warp(rock.transform.position + Vector3.right * 4);
                        MovePlayer(rock.transform.position - Vector3.right * 3);
                    }
                    Physics.SyncTransforms();
                    boss.BeginPattern(Patterns[pattern]);
                    Require(boss.Phase == DragonPhase.Windup, "실제 패턴 시작 " + Patterns[pattern]);
                    since = Time.time;
                    stage = 1;
                    return;
                }
                if (stage == 1)
                {
                    if (Time.time < since + CaptureAt[pattern]) return;
                    if (Patterns[pattern] == DragonPattern.Charge) Require(boss.RockStuns == 1, "실제 바위 충돌 기절");
                    Capture(Patterns[pattern].ToString());
                    boss.Stun(0.03f);
                    since = Time.time;
                    stage = ++pattern < Patterns.Length ? 0 : 2;
                    return;
                }
                if (stage == 2)
                {
                    boss.GetComponent<Health>().TryApplyDamage(Mathf.CeilToInt(boss.GetComponent<Health>().MaxHealth * 0.26f));
                    since = Time.time;
                    stage = 3;
                    return;
                }
                if (stage == 3)
                {
                    if (Time.time < since + 0.15f) return;
                    Require(boss.ThresholdStuns == 1 && boss.Phase == DragonPhase.Stunned, "75% 기절");
                    Capture("ThresholdStun");
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
                    since = Time.time;
                    stage = 5;
                    return;
                }
                if (Time.time < since + 0.2f) return;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Vector2 pointer = Camera.main.WorldToScreenPoint(boss.transform.position + Vector3.up);
                Require(player.TryUseSkill(pointer), "실제 강화 지팡이 스킬");
                Capture("EnhancedSkill");
                Debug.Log("BOSS_GROWTH_PLAY_PASS scene=" + (terrain ? "TerrainIntegration" : "Game/Field") + " patterns=6 rocks=6 rockStun=1 thresholdStun=1 enhance=1 skill=pass captures=9");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static void MovePlayer(Vector3 point)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
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
            try
            {
                camera.transform.position = arena.Center + Quaternion.LookRotation(arena.Forward) * new Vector3(12, 22, -18);
                camera.transform.LookAt(arena.Center);
                camera.orthographicSize = 17;
                Nomad.Minimap.Editor.MinimapVerification.Capture("issue214-" + (SessionState.GetBool(Key + ".terrain", false) ? "terrain-" : "main-") + name + ".png");
            }
            finally { camera.transform.SetPositionAndRotation(position, rotation); camera.orthographicSize = size; }
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
