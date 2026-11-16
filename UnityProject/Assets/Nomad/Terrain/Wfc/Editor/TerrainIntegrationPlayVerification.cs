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
    public static class TerrainIntegrationPlayVerification
    {
        private const string SessionKey = "Nomad.Terrain.IntegrationPlay";
        private static double deadline;
        private static int lastFrame;
        private static int waitUntil;
        private static int stage;
        private static Keyboard keyboard;
        private static TerrainIntegrationControls controls;
        private static int seed;
        private static string id;
        private static string signature;
        private static Vector3 origin;

        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            if (Object.FindAnyObjectByType<TerrainIntegrationControls>().Generator.UsesMicro)
            { MicroTerrainPlayVerification.Run(); return; }
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "timeout stage=" + stage);
                if (!Application.isPlaying || Time.frameCount <= lastFrame || Time.frameCount < 4) return;
                lastFrame = Time.frameCount;
                if (Time.frameCount < waitUntil) return;
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
                Require(result != null && result.Success && controls.Player.activeSelf, "playable field stage=" + stage);
                switch (stage++)
                {
                    case 0:
                        Require(result.Pieces.Count == 36 && !controls.Legacy, "saved scene starts in WFC");
                        Require(Object.FindAnyObjectByType<PropScatter>().Positions.Count > 0, "actual props");
                        seed = controls.Seed;
                        id = result.GenerationId;
                        Press(Key.R);
                        break;
                    case 1:
                        Require(seed != controls.Seed && id != result.GenerationId, "R new seed/generation");
                        signature = TerrainIntegrationValidation.Signature(result);
                        id = result.GenerationId;
                        Press(Key.T);
                        break;
                    case 2:
                        Require(id != result.GenerationId && signature == TerrainIntegrationValidation.Signature(result), "T replay");
                        Press(Key.O);
                        break;
                    case 3:
                        Require(!Camera.main.GetComponent<TopDownCamera>().enabled && Camera.main.transform.position.y > 50f, "overview camera");
                        Press(Key.G);
                        break;
                    case 4:
                        Require(Object.FindObjectsByType<ModularTerrainTile>().Length >= 108, "108 combination gallery active");
                        Require(Object.FindObjectsByType<FullWidthStairTile>().Count(tile => tile.transform.parent.name == "IntegrationCombinationGallery") == 4, "four new stair variants in shared gallery");
                        Press(Key.G);
                        break;
                    case 5:
                        Require(Camera.main.GetComponent<TopDownCamera>().enabled, "gallery returns to player camera");
                        Press(Key.P);
                        break;
                    case 6:
                        Require(Object.FindAnyObjectByType<PropScatter>().Positions.Count == 0, "props off");
                        Press(Key.P);
                        break;
                    case 7:
                        Require(Object.FindAnyObjectByType<PropScatter>().Positions.Count > 0, "props restored");
                        Press(Key.L);
                        break;
                    case 8:
                        Require(controls.Legacy && result.Pieces.Count == 12, "legacy comparison in same scene");
                        Press(Key.L);
                        break;
                    case 9:
                        Require(!controls.Legacy && result.Pieces.Count == 36, "return to WFC");
                        origin = controls.Player.transform.position;
                        Press(Key.W);
                        waitUntil = Time.frameCount + 12;
                        break;
                    case 10:
                        Require(Vector3.Distance(origin, controls.Player.transform.position) > 0.4f, "WASD input");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        var body = controls.Player.GetComponent<CharacterController>();
                        body.enabled = false;
                        controls.Player.transform.position = Vector3.down * 100f;
                        body.enabled = true;
                        waitUntil = Time.frameCount + 3;
                        break;
                    default:
                        Require(controls.Player.transform.position.y > -1f, "fall recovery");
                        Require(Vector3.Distance(Camera.main.transform.position,
                            controls.Player.transform.position + new Vector3(0f, 12f, -8f)) < 0.1f, "camera follows");
                        TerrainIntegrationValidation.Capture(controls, 0, "terrain-integration-play");
                        string report = $"TERRAIN_INTEGRATION_PLAY_PASS frame={Time.frameCount} time={Time.time:F2}: saved scene, R/T/L/P/O/G inputs, replay, props, 108 gallery, movement, fall recovery, camera, WFC/legacy switch";
                        System.IO.File.WriteAllText("Artifacts/terrain-integration-play-results.txt", report);
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
            waitUntil = Time.frameCount + 3;
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
            if (!value) throw new InvalidOperationException("TERRAIN_INTEGRATION_PLAY_FAIL: " + message);
        }
    }
}
