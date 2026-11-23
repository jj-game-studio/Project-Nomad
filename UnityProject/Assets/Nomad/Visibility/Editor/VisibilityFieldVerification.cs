using System;
using System.Linq;
using Nomad.Combat;
using Nomad.Monsters;
using Nomad.Player;
using Nomad.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.Visibility.Editor
{
    public static class VisibilityFieldVerification
    {
        private const string SessionKey = "Nomad.Visibility.FieldVerify";
        private static TerrainIntegrationControls controls;
        private static PlayerVisibility sight;
        private static MonsterIdentity tracked;
        private static Keyboard keyboard;
        private static double deadline;
        private static int stage;
        private static int generations;
        private static string previousGeneration;
        private static float waitUntil;
        private static int checks;
        private static VisibilityFoliage tree;
        private static Texture sourceTexture;

        public static void Run()
        {
            VisibilityAuthoring.BuildField();
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Visibility field timeout " + stage);
                if (!EditorApplication.isPlaying || Time.time < waitUntil) return;
                if (controls == null)
                {
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    controls = Object.FindFirstObjectByType<TerrainIntegrationControls>();
                    sight = Object.FindFirstObjectByType<PlayerVisibility>();
                    controls.Player.GetComponent<Health>().IsCombatImmune = true;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                }

                if (stage == 0)
                {
                    if (controls.Navigation.State != FieldGenerationState.Ready) return;
                    string id = controls.Navigation.LastResult.GenerationId;
                    if (id == previousGeneration) return;
                    previousGeneration = id;
                    stage = 1;
                    waitUntil = Time.time + 0.3f;
                }
                else if (stage == 1)
                {
                    MonsterIdentity[] monsters = Object.FindObjectsByType<MonsterIdentity>(FindObjectsSortMode.None);
                    Check(monsters.Length > 0, "field spawns monsters");
                    Check(monsters.All(m => m.GetComponent<VisibilityTarget>()?.Controlled == true), "all spawns registered");
                    Check(monsters.All(m => m.GetComponent<Health>().enabled && m.GetComponent<Collider>().enabled), "combat and collision preserved");
                    Check(monsters.All(m => m.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled), "AI navigation preserved");
                    Check(monsters.Any(m => !m.GetComponent<VisibilityTarget>().IsVisible), "field has hidden enemies");
                    Check(VisibilityTarget.ActiveCount == monsters.Length, "no stale targets after regeneration");
                    generations++;
                    if (generations < 3)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                        stage = 2;
                    }
                    else
                    {
                        tracked = monsters[0];
                        controls.Player.GetComponent<PlayerMovement>().enabled = false;
                        controls.Player.transform.position = tracked.transform.position;
                        stage = 3;
                        waitUntil = Time.time + 0.2f;
                    }
                }
                else if (stage == 2)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    stage = 0;
                }
                else if (stage == 3)
                {
                    sight.SetDirection(Vector3.forward);
                    controls.Player.transform.position = tracked.transform.position;
                    Physics.SyncTransforms();
                    var target = tracked.GetComponent<VisibilityTarget>();
                    target.Evaluate(sight);
                    Check(target.IsVisible, "field target revealed nearby");
                    Check(!tracked.GetComponentsInChildren<Renderer>().Any(r => r.forceRenderingOff), "field model revealed");
                    Check(tracked.GetComponent<SegmentedHealthBar>().enabled, "field HUD revealed");
                    controls.Player.transform.position = tracked.transform.position + Vector3.forward * 5f;
                    stage = 4;
                    waitUntil = Time.time + 0.3f;
                }
                else if (stage == 4)
                {
                    sight.SetDirection(Vector3.forward);
                    tracked.GetComponent<VisibilityTarget>().Evaluate(sight);
                    Check(!tracked.GetComponent<VisibilityTarget>().IsVisible, "field rear target hidden");
                    Check(tracked.GetComponent<MonsterTracker>().enabled, "hidden tracker still enabled");
                    Check(tracked.GetComponent<MonsterMeleeAttack>().enabled, "hidden attacker still enabled");
                    PrepareTree();
                    stage = 5;
                    waitUntil = Time.time + 0.6f;
                }
                else if (stage == 5)
                {
                    VerifyTree();
                    Debug.Log("VISIBILITY_FIELD_PASS generations=" + generations + " checks=" + checks);
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        // 실제 구매 식생: 나무는 시선을 막지 않고, 소품은 차단을 유지합니다. 플레이어를 나무 옆에 둡니다.
        private static void PrepareTree()
        {
            VisibilityFoliage[] connected = Object.FindObjectsByType<VisibilityFoliage>(FindObjectsSortMode.None);
            VisibilityFoliage[] trees = connected.Where(f => Label(f).Contains("tree") && !Label(f).Contains("stump")).ToArray();
            Check(trees.Length > 0, "field trees connected");
            Check(trees.All(f => !f.BlocksSight), "field trees do not block sight");
            Check(connected.Where(f => Label(f).Contains("prop_")).All(f => f.BlocksSight), "field props keep blocking");

            tree = trees.First(f => !f.Faded);
            Material source = tree.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            sourceTexture = new[] { "_BaseMap", "_MainTex", "_Albedo_Map" }
                .Where(source.HasProperty).Select(source.GetTexture).FirstOrDefault(texture => texture != null);
            Check(sourceTexture != null, "field tree source texture found");
            controls.Player.transform.position = tree.transform.position + Vector3.right * 1.5f;
            Physics.SyncTransforms();
        }

        private static void VerifyTree()
        {
            Check(tree.InSight && tree.Faded && tree.Alpha < 1f, "field tree near player fades");
            Material faded = tree.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Check(faded.shader.name == "Nomad/Visibility/Foliage Fade", "field tree uses fade shader");
            Check(faded.GetTexture("_BaseMap") == sourceTexture, "field tree keeps source texture");

            Vector3 toTree = tree.transform.position - sight.Eye;
            toTree.y = 0f;
            float length = toTree.magnitude + 1f;
            Check(sight.ClearDistance(sight.Eye, toTree.normalized, length) >= toTree.magnitude + 0.5f, "field trunk does not block sight");
            Check(Object.FindFirstObjectByType<VisibilityField>().ShadeRenderer.enabled, "field shade active");
            Debug.Log("VISIBILITY_FIELD_TREE " + tree.name);
        }

        private static string Label(Component item) => item.name.ToLowerInvariant();

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Visibility field: " + label);
            checks++;
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Tick;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}
