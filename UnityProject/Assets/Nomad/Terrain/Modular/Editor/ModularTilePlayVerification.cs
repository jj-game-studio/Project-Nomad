using System;
using Nomad.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class ModularTilePlayVerification
    {
        private const string Key = "Nomad.Modular.PlayVerification";
        private static double deadline;
        private static int lastFrame;
        private static int waitUntil;
        private static int stage;
        private static Keyboard keyboard;
        private static PlayerMovement player;

        public static void Run()
        {
            ModularTilePreview.Cross();
            var tile = Object.FindAnyObjectByType<ModularTerrainTile>();
            var inspector = UnityEditor.Editor.CreateEditor(tile);
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                VisualElement panel = inspector.CreateInspectorGUI();
                window.Show();
                window.rootVisualElement.Add(panel);
                panel.Q<EnumField>().value = TerrainFloorState.High;
                Require(tile.Combination == 41 && tile.GetState(0) == TerrainFloorState.High,
                    "region selector changes only southwest corner");
                panel.Q<SliderInt>().value = 80;
                Require(tile.Combination == 80 && tile.GetComponent<TerrainPiece>().Connections.Count == 12,
                    "Inspector slider updates floor/ports " + tile.Combination);
                panel.Q<SliderInt>().value = 40;
                Require(tile.GetState(0) == TerrainFloorState.Low, "Inspector reset to low");
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(inspector);
            }

            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
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

                if (stage == 0)
                {
                    Application.runInBackground = true;
                    Time.captureFramerate = 60;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    player = Object.FindAnyObjectByType<PlayerMovement>();
                    Require(player != null, "review player exists");
                    Queue(KeyCode.W);
                    waitUntil = Time.frameCount + 80;
                    stage = 1;
                    return;
                }

                if (stage == 1)
                {
                    Require(player.transform.position.y > 1.9f && player.transform.position.z > -0.5f,
                        "W input climbs entire arm to landing");
                    Queue(KeyCode.S);
                    waitUntil = Time.frameCount + 80;
                    stage = 2;
                    return;
                }

                Require(player.transform.position.y < 0.1f && player.transform.position.z < -5.5f,
                    "S input returns to neighbour floor");
                Require(Vector3.Distance(Camera.main.transform.position,
                    player.transform.position + new Vector3(0f, 12f, -8f)) < 0.1f, "player camera follows");
                string result = $"MODULAR_PLAY_PASS frame={Time.frameCount} time={Time.time:F2}: Inspector 40/80, actual W/S input, full stair climb/return, existing player camera";
                System.IO.File.WriteAllText("Artifacts/modular-play-results.txt", result);
                Debug.Log(result);
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Queue(KeyCode key) => InputSystem.QueueStateEvent(keyboard,
            new KeyboardState(key == KeyCode.W ? UnityEngine.InputSystem.Key.W : UnityEngine.InputSystem.Key.S));

        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Update;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("MODULAR_PLAY_FAIL: " + message);
        }
    }
}
