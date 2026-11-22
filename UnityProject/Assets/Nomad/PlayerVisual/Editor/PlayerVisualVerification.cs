using System;
using System.IO;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.PlayerVisual.Editor
{
    internal static class PlayerVisualVerification
    {
        private const string SessionKey = "Nomad.PlayerVisual.Verify";
        private static PlayerMovement player;
        private static Keyboard keyboard;
        private static int stage;
        private static float start;
        private static float peak;
        private static Vector3 origin;
        private static double deadline;

        public static void Run()
        {
            PlayerVisualAuthoring.Validate();
            EditorSceneManager.OpenScene(PlayerVisualAuthoring.ScenePath);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, $"Timeout at stage {stage}");
                if (!Application.isPlaying) return;
                if (keyboard == null)
                {
                    player = Object.FindAnyObjectByType<PlayerMovement>();
                    if (player == null || Camera.main == null) return;
                    Application.runInBackground = true;
                    Time.captureFramerate = 60;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    Advance();
                }

                Vector3 position = player.transform.position;
                float elapsed = Time.time - start;
                Check(player.transform.Find("Visual").gameObject.activeInHierarchy, "Visual became inactive");
                switch (stage)
                {
                    case 1 when elapsed > 0.25f:
                        Check(Time.frameCount > 5 && Mathf.Abs(position.y) < 0.1f, "Frames or grounding");
                        Capture("player-visual-top.png", Camera.main);
                        CaptureFront();
                        origin = position;
                        Advance(Key.W);
                        break;
                    case 2 when elapsed > 0.35f:
                        Check((position - origin).magnitude > 1.5f, "Keyboard movement");
                        Check(Vector3.Dot(player.transform.forward, (position - origin).normalized) > 0.98f, "Facing does not follow movement");
                        Check(player.transform.Find("Visual").localPosition == Vector3.zero, "Visual drift");
                        MoveTo(Vector3.zero);
                        peak = 0f;
                        Advance(Key.Space);
                        break;
                    case 3:
                        peak = Mathf.Max(peak, position.y);
                        if (elapsed > 1f)
                        {
                            Check(peak > 1.1f && peak < 1.3f && Mathf.Abs(position.y) < 0.1f, $"Jump/land {peak}, {position}");
                            MoveTo(Vector3.zero);
                            origin = player.transform.position;
                            Advance(Key.LeftShift, Key.W);
                        }
                        break;
                    case 4 when elapsed > 0.35f:
                        Check(!player.IsRolling && Vector3.Distance(origin, position) > 2.9f, "Roll regression");
                        MoveTo(new Vector3(0f, 0f, 3f));
                        Advance(Key.W);
                        break;
                    case 5 when elapsed > 0.6f:
                        Check(player.transform.position.z < 3.5f, "Wall collision regression");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Debug.Log($"PLAYER_VISUAL_PLAY_PASS: frame={Time.frameCount}, model, grounding, movement, facing, jump, roll, wall");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"PLAYER_VISUAL_PLAY_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static void Advance(params Key[] keys)
        {
            stage++;
            start = Time.time;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        private static void MoveTo(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void CaptureFront()
        {
            var cameraObject = new GameObject("Capture Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.2f, 4f);
            camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 1.2f;
            Capture("player-visual-front.png", camera);
            Object.DestroyImmediate(cameraObject);
        }

        private static void Capture(string name, Camera camera)
        {
            Directory.CreateDirectory("Artifacts");
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine("Artifacts", name), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
            }
        }

        private static void Check(bool condition, string message) => PlayerVisualAuthoring.Require(condition, message);

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            EditorApplication.Exit(code);
        }
    }
}
