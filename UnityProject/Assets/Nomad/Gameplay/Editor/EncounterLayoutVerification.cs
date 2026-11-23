using System;
using System.Collections;
using System.IO;
using System.Linq;
using Nomad.Game.Modules;
using Nomad.Monsters;
using Nomad.Monsters.Demo;
using Nomad.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class EncounterLayoutVerification
    {
        private const string Key = "Nomad.EncounterLayout.Verify";
        private static IEnumerator routine;
        private static int frame;
        private static double deadline;

        [MenuItem("Nomad/Monsters/Open Field Encounter Preview")]
        public static void OpenPreview()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Nomad/Game/Field.unity");
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Nomad/Game/Field.unity");
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "deadline");
                if (!Application.isPlaying || Time.frameCount == frame) return;
                frame = Time.frameCount;
                routine ??= Verify();
                if (routine.MoveNext()) return;
                Debug.Log("ENCOUNTER_LAYOUT_PASS seeds=0,7,51 normal=12 elite=1 boss=1 field=true");
                Finish(0);
            }
            catch (Exception error)
            {
                Debug.LogError("ENCOUNTER_LAYOUT_FAIL " + error);
                Finish(1);
            }
        }

        private static IEnumerator Verify()
        {
            TerrainIntegrationControls controls = null;
            FieldMonsters monsters = null;
            while (controls == null || monsters == null || controls.Navigation.State != FieldGenerationState.Ready)
            {
                controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
                monsters = Object.FindAnyObjectByType<FieldMonsters>();
                yield return null;
            }
            foreach (int seed in new[] { 0, 7, 51 })
            {
                var settings = new SerializedObject(controls);
                settings.FindProperty("seed").intValue = seed;
                settings.ApplyModifiedPropertiesWithoutUndo();
                controls.Regenerate(false);
                yield return null;
                yield return null;
                Require(controls.Navigation.State == FieldGenerationState.Ready, "field ready " + seed);
                var actors = monsters.Enemies;
                int normals = actors.Count(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Normal);
                int elites = actors.Count(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Elite);
                int bosses = actors.Count(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Boss);
                Require(normals == 12 && elites == 1 && bosses == 1,
                    $"seed {seed} counts: normal={normals}, elite={elites}, boss={bosses}");
                BossArena arena = Object.FindAnyObjectByType<BossArena>();
                Require(arena != null && arena.Connected, "separate connected arena " + seed);
                Vector3 start = controls.Navigation.LastResult.PlayerPosition;
                float eliteDistance = actors.Where(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Elite)
                    .Min(actor => MonsterEncounterLayout.PathLength(start, actor.transform.position));
                foreach (var normal in actors.Where(actor => actor.GetComponent<MonsterIdentity>().Rank == MonsterRank.Normal))
                    Require(MonsterEncounterLayout.PathLength(start, normal.transform.position) < eliteDistance,
                        "normal precedes elite " + seed);
                Require(MonsterEncounterLayout.PathLength(start, arena.Center) > eliteDistance,
                    "boss follows elite " + seed);
                foreach (var actor in actors)
                    Require(!float.IsInfinity(MonsterEncounterLayout.PathLength(start, actor.transform.position)),
                        "reachable actor " + actor.name);
                Capture(controls, arena, seed);
                BossArena previous = arena;
                controls.Regenerate(false);
                yield return null;
                Require(previous == null, "previous arena removed " + seed);
                Require(Object.FindObjectsByType<BossArena>(FindObjectsSortMode.None).Length == 1, "one arena per generation");
            }
        }

        private static void Capture(TerrainIntegrationControls controls, BossArena arena, int seed)
        {
            Camera camera = Camera.main;
            Vector3 previous = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            bool orthographic = camera.orthographic;
            float size = camera.orthographicSize;
            var target = new RenderTexture(1440, 1000, 24);
            var pixels = new Texture2D(1440, 1000, TextureFormat.RGB24, false);
            RenderTexture active = RenderTexture.active;
            try
            {
                Vector3 center = (controls.Navigation.LastResult.PlayerPosition + arena.Center) * 0.5f;
                camera.transform.SetPositionAndRotation(center + new Vector3(40, 70, -45), Quaternion.LookRotation(new Vector3(-40, -70, 45)));
                camera.orthographic = true;
                camera.orthographicSize = 55;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts/encounters");
                File.WriteAllBytes($"Artifacts/encounters/seed-{seed}.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = active;
                camera.transform.SetPositionAndRotation(previous, rotation);
                camera.orthographic = orthographic;
                camera.orthographicSize = size;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
