using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Nomad.Player.Editor
{
    internal static class PlayerMovementVerification
    {
        private const string ScenePath = "Assets/Nomad/Player/A_PlayerMovement.unity";
        private const string SessionKey = "Nomad.Player.Verify";
        private static Keyboard keyboard;
        private static PlayerMovement player;
        private static Camera camera;
        private static int stage;
        private static float start;
        private static float peak;
        private static Vector3 origin;
        private static double deadline;

        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false))
                return;

            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException($"Timeout at stage {stage}: {player?.transform.position}");
                if (!Application.isPlaying)
                    return;

                if (keyboard == null)
                {
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                    camera = Camera.main;
                    if (player == null || camera == null)
                        return;

                    Time.captureFramerate = 60;
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode =
                        InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    Advance();
                }

                Vector3 pos = player.transform.position;
                Vector3 offset = new Vector3(0f, 12f, -8f);
                Check(Vector3.Distance(camera.transform.position, pos + offset) < 0.1f, "follow offset");
                Check(Quaternion.Angle(camera.transform.rotation, Quaternion.LookRotation(-offset)) < 0.1f, "fixed rotation");
                Check(Mathf.Abs(camera.fieldOfView - 50f) < 0.01f && !camera.orthographic, "camera projection");

                float elapsed = Time.time - start;
                switch (stage)
                {
                    case 1 when elapsed > 0.25f:
                        origin = pos;
                        Advance(Key.W, Key.D);
                        break;
                    case 2 when elapsed > 0.3f:
                        Vector3 diagonal = pos - origin;
                        Check(diagonal.x > 0.8f && Mathf.Abs(diagonal.x - diagonal.z) < 0.02f &&
                              diagonal.magnitude > 1.35f && diagonal.magnitude < 1.7f, $"normalized diagonal {diagonal}, W={keyboard.wKey.isPressed}, current={Keyboard.current == keyboard}");
                        MoveTo(new Vector3(-12f, 0.05f, -3f));
                        Advance(Key.Space);
                        peak = 0f;
                        break;
                    case 3:
                        peak = Mathf.Max(peak, pos.y);
                        if (elapsed > 1f)
                        {
                            Check(peak > 1.15f && peak < 1.26f && pos.y < 0.1f, "jump and landing");
                            Advance(Key.LeftShift, Key.A);
                            origin = pos;
                        }
                        break;
                    case 4 when elapsed > 0.31f:
                        Check(!player.IsRolling && origin.x - pos.x > 2.9f && origin.x - pos.x < 3.1f, "3m roll");
                        Advance(Key.A);
                        break;
                    case 5 when elapsed > 0.8f:
                        Check(pos.x > -15.35f, "wall collision");
                        origin = pos;
                        Advance(Key.LeftShift, Key.A);
                        break;
                    case 6 when elapsed > 0.12f:
                        Check(player.IsRolling && Mathf.Abs(pos.x - origin.x) < 0.05f, "wall stops displacement while roll stays active");
                        Advance();
                        break;
                    case 7 when elapsed > 0.22f:
                        Check(!player.IsRolling, "wall-blocked roll expires");
                        Advance(Key.LeftShift);
                        break;
                    case 8 when elapsed > 0.09f:
                        Check(!player.IsRolling, "roll cooldown ignores early press");
                        MoveTo(new Vector3(-12f, 0.05f, 4f));
                        Advance(Key.W);
                        break;
                    case 9 when pos.z > 12f:
                        Check(Mathf.Abs(pos.y - 2f) < 0.12f, "fixed ramp ascent");
                        Advance(Key.S);
                        break;
                    case 10 when pos.z < 4f:
                        Check(Mathf.Abs(pos.y) < 0.12f, "fixed ramp descent");
                        MoveTo(new Vector3(0f, 0.05f, -3f));
                        Advance(Key.W);
                        break;
                    case 11 when pos.z > 23f:
                        Check(Mathf.Abs(pos.y - 2f) < 0.12f, "B stairs ascent");
                        Advance(Key.S);
                        break;
                    case 12 when pos.z < -3f:
                        Check(Mathf.Abs(pos.y) < 0.12f, "B stairs descent");
                        MoveTo(new Vector3(-12f, 0.05f, -4.4f));
                        Advance();
                        break;
                    case 13 when elapsed > 0.3f:
                        Advance(Key.LeftShift, Key.S);
                        break;
                    case 14 when elapsed > 0.19f:
                        Check(!player.IsRolling && pos.y < 0.015f, "leaving a ledge ends roll before its duration expires");
                        Debug.Log($"PLAYER_MOVEMENT_PASS: diagonal, camera, jump, roll, wall, cooldown, ledge, ramp and B traversal, frame={Time.frameCount}");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"PLAYER_MOVEMENT_FAIL stage={stage} position={player?.transform.position}: {exception}");
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

        private static void Check(bool passed, string condition)
        {
            if (!passed)
                throw new InvalidOperationException(condition);
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            EditorApplication.Exit(exitCode);
        }
    }
}
