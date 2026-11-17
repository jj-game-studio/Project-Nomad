using System;
using System.Linq;
using System.Reflection;
using Nomad.Combat;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Player;
using Nomad.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class MonsterMapDebugVerification
    {
        private const string Key = "Nomad.MapDebug.Verify";
        private static int stage;
        private static int frame;
        private static int waitUntil;
        private static double deadline;
        private static MonsterMapDebug debug;
        private static MonsterTestPanel panel;
        private static TerrainIntegrationControls controls;

        public static void Run()
        {
            EditorSceneManager.OpenScene(MonsterDebugSetup.Path);
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            EditorApplication.update -= Tick;
            if (!SessionState.GetBool(Key, false)) return;
            stage = 0;
            frame = -1;
            deadline = EditorApplication.timeSinceStartup + 90f;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("map debug verification");
                if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
                frame = Time.frameCount;
                Application.runInBackground = true;
                if (stage == 0)
                {
                    debug = Object.FindFirstObjectByType<MonsterMapDebug>();
                    if (debug == null || !debug.Ready || Time.time < 0.5f) return;
                    controls = debug.GetComponent<TerrainIntegrationControls>();
                    panel = debug.GetComponent<MonsterTestPanel>();
                    Require(debug.GetComponent<GameplayIntegration>() == null && panel.MonsterCount == 0, "empty generated-map sandbox");
                    Require(Camera.main.GetComponent<TopDownCamera>().enabled && !Camera.main.orthographic, "same play camera");
                    Require(Vector3.Distance(Camera.main.transform.position - controls.Player.transform.position,
                        new Vector3(0f, 12f, -8f)) < 0.1f, "same play camera offset");
                    for (int type = 0; type < 5; type++)
                    {
                        int before = panel.MonsterCount;
                        foreach (FieldSpawnPoint point in controls.Navigation.LastResult.EnemyPoints)
                        {
                            if (Vector3.Distance(point.Position, controls.Player.transform.position) < 13f) continue;
                            if (type < 3) panel.SpawnNormalStyle(type, point.Position);
                            else { panel.SetKind(type - 2); panel.SpawnAt(point.Position); }
                            if (panel.MonsterCount > before) break;
                        }
                        Require(panel.MonsterCount == before + 1, "spawn chosen type on generated NavMesh: " + type);
                    }
                    foreach (MonsterIdentity actor in Object.FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None))
                    {
                        actor.GetComponent<MonsterMeleeAttack>().enabled = false;
                        actor.GetComponent<NavMeshAgent>().speed = 0f;
                    }
                    Set("detection", false);
                    Set("skillWarnings", false);
                    Set("healthBars", false);
                    stage = 1;
                    waitUntil = Time.frameCount + 2;
                    return;
                }
                if (stage == 1)
                {
                    if (Time.frameCount < waitUntil) return;
                    MonsterIdentity[] actors = Object.FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None);
                    Require(actors.Length == 5 && actors.Count(a => a.Rank == MonsterRank.Normal) == 3, "five selected types placed");
                    foreach (MonsterIdentity actor in actors)
                    {
                        Require(!actor.GetComponent<SegmentedHealthBar>().enabled, "HP toggle");
                        Require(!actor.GetComponentInChildren<MonsterCombatDemo>().ShowSkillWarnings, "telegraph toggle");
                        Require(!actor.GetComponentsInChildren<LineRenderer>().Single(line => line.name == "Test range 0").enabled, "range toggle");
                    }
                    Require(panel.BlocksPointer(new Vector2(Screen.width - 50f, Screen.height - 50f)), "panel blocks shooting");
                    actors[0].GetComponent<Health>().TryApplyDamage(10000);
                    controls.Regenerate(false);
                    stage = 2;
                    return;
                }
                if (!debug.Ready) return;
                Require(panel.MonsterCount == 0 && Object.FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None).Length == 0,
                    "regeneration removes old actors");
                Require(Object.FindObjectsByType<PlaceholderEquipmentDrop>(FindObjectsSortMode.None).Length == 0, "regeneration removes drops");
                Require(Object.FindObjectsByType<MonsterBolt>(FindObjectsSortMode.None).Length == 0, "regeneration removes bolts");
                Debug.Log("MONSTER_MAP_DEBUG_PASS: generated map, play camera, five chosen types, display toggles, UI blocking, regeneration cleanup");
                Finish(0);
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void Set(string field, object value) => panel.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, value);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
