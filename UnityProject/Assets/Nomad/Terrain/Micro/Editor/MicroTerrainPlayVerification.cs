using System;
using System.Linq;
using Nomad.Player;
using Nomad.Props;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class MicroTerrainPlayVerification
    {
        private const string SessionKey = "Nomad.Terrain.MicroPlay";
        private static double deadline;
        private static int frame;
        private static int waitUntil;
        private static int stage;
        private static Keyboard keyboard;
        private static TerrainIntegrationControls controls;
        private static string signature;
        private static int seed;
        private static int propCount;
        private static Vector3 origin;

        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "timeout stage=" + stage);
                if (!Application.isPlaying || Time.frameCount <= frame || Time.frameCount < 4) return;
                frame = Time.frameCount;
                if (frame < waitUntil) return;
                if (keyboard == null)
                {
                    Application.runInBackground = true;
                    Time.captureFramerate = 60;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
                }
                TerrainGenerationResult result = controls.Generator.LastResult;
                Require(result.Success && controls.Player.activeSelf, "playable field");
                switch (stage++)
                {
                    case 0:
                        Require(controls.Generator.ReachableSamples == 2916, "all 324 tiles reachable");
                        signature = TerrainIntegrationValidation.Signature(result);
                        Press(Key.T);
                        break;
                    case 1:
                        Require(signature == TerrainIntegrationValidation.Signature(result), "same seed");
                        seed = controls.Seed;
                        Press(Key.R);
                        break;
                    case 2:
                        Require(seed != controls.Seed && controls.Generator.ReachableSamples == 2916, "new reachable seed");
                        Press(Key.O);
                        break;
                    case 3:
                        Require(!Camera.main.GetComponent<TopDownCamera>().enabled, "overview");
                        Press(Key.G);
                        break;
                    case 4:
                        Require(Object.FindObjectsByType<MicroTerrainTile>().Count(tile => tile.transform.parent.name == "IntegrationCombinationGallery") == 11, "micro gallery 11 sources");
                        Press(Key.G);
                        break;
                    case 5:
                        Require(Camera.main.GetComponent<TopDownCamera>().enabled, "player camera");
                        Press(Key.L);
                        break;
                    case 6:
                        Require(controls.Legacy && result.Pieces.Count == 12, "legacy comparison preserved");
                        Press(Key.L);
                        break;
                    case 7:
                        Require(!controls.Legacy && result.Pieces.Count == 324, "micro restored");
                        propCount = Object.FindAnyObjectByType<PropScatter>().Positions.Count;
                        Press(Key.P);
                        break;
                    case 8:
                        Require(Object.FindAnyObjectByType<PropScatter>().Positions.Count == 0, "props off");
                        Press(Key.P);
                        break;
                    case 9:
                        Require(Object.FindAnyObjectByType<PropScatter>().Positions.Count == propCount, "props replay and walkability valid");
                        origin = controls.Player.transform.position;
                        Press(Key.W);
                        waitUntil = frame + 12;
                        break;
                    case 10:
                        Require(Vector3.Distance(origin, controls.Player.transform.position) > 0.4f, "movement");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        var body = controls.Player.GetComponent<CharacterController>();
                        body.enabled = false;
                        controls.Player.transform.position = Vector3.down * 100f;
                        body.enabled = true;
                        waitUntil = frame + 3;
                        break;
                    default:
                        Require(controls.Player.transform.position.y >= -0.1f, "fall recovery");
                        TerrainIntegrationValidation.Capture(controls, 1, "micro-play-overview");
                        TerrainIntegrationValidation.Capture(controls, 0, "micro-play-player");
                        string report = $"MICRO_PLAY_PASS frame={frame} time={Time.time:F2}: 3m saved scene, 2916 reachable samples, T/R/O/G/L/P/W input, 11-source gallery, camera, movement, recovery";
                        System.IO.File.WriteAllText("Artifacts/micro-play-results.txt", report);
                        Debug.Log(report);
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            waitUntil = frame + 3;
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("MICRO_PLAY_FAIL: " + message);
        }
    }
}
