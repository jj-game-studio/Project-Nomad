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
        private static PlayerAnimation animation;
        private static Quaternion idleLeg;
        private static bool sawRun, sawJump, sawFall, sawLanding, sawRoll, sawRollRecovery, rollReleased;
        private static string jumpPhase;
        private static int jumpEntries, fallEntries, landEntries;
        private static Vector3 previousHips;
        private static float maxHipsStep;
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
                    animation = player.GetComponent<PlayerAnimation>();
                    Check(animation != null, "Animation driver missing");
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
                if (stage == 2 && elapsed > 0.15f && !sawRun)
                {
                    Check(animation.CurrentState == "Run", "Run state missing");
                    Check(Quaternion.Angle(idleLeg, animation.Animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).localRotation) > 5f, "Retargeted leg did not animate");
                    CaptureFront("player-animation-run.png");
                    sawRun = true;
                }
                if (stage == 3 && elapsed > 0.15f && !sawJump)
                {
                    Check(animation.CurrentState == "Jump", "Jump state missing");
                    CaptureFront("player-animation-jump.png");
                    sawJump = true;
                }
                if (stage == 3)
                {
                    string phase = animation.CurrentState;
                    if (phase != jumpPhase)
                    {
                        if (phase == "Jump") jumpEntries++;
                        if (phase == "Fall") fallEntries++;
                        if (phase == "Land") landEntries++;
                        Check(jumpEntries <= 1 && fallEntries <= 1 && landEntries <= 1, "Jump phase restarted");
                        Debug.Log($"JUMP_PHASE {phase} elapsed={elapsed:F3}");
                        jumpPhase = phase;
                    }
                    Vector3 hips = animation.Animator.GetBoneTransform(HumanBodyBones.Hips).position - position;
                    if (elapsed > 0.05f)
                    {
                        float step = Vector3.Distance(previousHips, hips);
                        maxHipsStep = Mathf.Max(maxHipsStep, step);
                        if (step > 0.08f) Debug.Log($"JUMP_POSE_STEP elapsed={elapsed:F3} phase={phase} delta={step:F4}, prev={previousHips}, now={hips}");
                    }
                    previousHips = hips;
                }
                if (stage == 3 && elapsed > 0.45f && !sawFall)
                {
                    Check(animation.CurrentState == "Fall", "Apex did not switch to airborne pose");
                    CaptureFront("player-animation-fall.png");
                    sawFall = true;
                }
                if (stage == 3 && animation.CurrentState == "Land" && !sawLanding)
                {
                    Check(animation.CurrentState == "Land" && player.GetComponent<CharacterController>().isGrounded,
                        "Landing pose does not follow actual ground contact");
                    Check(SoleHeight() > -0.15f && SoleHeight() < 0.15f, "Landing feet drifted from ground");
                    CaptureFront("player-animation-jump-recovery.png");
                    sawLanding = true;
                }
                if (stage == 4 && elapsed > 0.12f && !sawRoll)
                {
                    Check(animation.CurrentState == "Roll", "Roll state missing");
                    CaptureFront("player-animation-roll.png");
                    sawRoll = true;
                }
                if (stage == 4 && elapsed > 0.25f && !rollReleased)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    rollReleased = true;
                }
                if (stage == 4 && elapsed > 0.4f && !sawRollRecovery)
                {
                    Check(!player.IsRolling && animation.CurrentState == "Idle", "Roll visual did not finish with physical movement");
                    CaptureFront("player-animation-roll-recovery.png");
                    sawRollRecovery = true;
                }
                switch (stage)
                {
                    case 1 when elapsed > 1.2f:
                        Check(Time.frameCount > 5 && Mathf.Abs(position.y) < 0.1f, "Frames or grounding");
                        Capture("player-visual-top.png", Camera.main);
                        Check(animation.CurrentState == "Idle", "Idle state missing");
                        Check(animation.Animator.GetBoneTransform(HumanBodyBones.Hips).name == "Hips", "Incorrect pelvis mapping");
                        float soleY = SoleHeight();
                        Check(soleY > -0.05f && soleY < 0.08f, $"Animated feet are not grounded: {soleY}");
                        idleLeg = animation.Animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).localRotation;
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
                        if (elapsed > 1.4f)
                        {
                            Check(peak > 1.1f && peak < 1.3f && Mathf.Abs(position.y) < 0.1f, $"Jump/land {peak}, {position}");
                            Check(animation.CurrentState == "Idle", "Landing recovery did not finish");
                            Check(jumpEntries == 1 && fallEntries == 1 && landEntries == 1 && maxHipsStep < 0.15f,
                                $"Jump phase continuity: {jumpEntries}/{fallEntries}/{landEntries}, maxHipStep={maxHipsStep}");
                            Debug.Log($"JUMP_PHASES_PASS maxHipsStep={maxHipsStep:F4}, peak={peak:F3}");
                            MoveTo(Vector3.zero);
                            origin = player.transform.position;
                            Advance(Key.LeftShift, Key.W);
                        }
                        break;
                    case 4 when elapsed > 0.9f:
                        Check(!player.IsRolling && Vector3.Distance(origin, position) > 2.9f && Vector3.Distance(origin, position) < 3.1f, "Roll distance regression");
                        Check(animation.CurrentState == "Idle" && sawRollRecovery, "Roll recovery did not finish");
                        MoveTo(new Vector3(0f, 0f, 3f));
                        Advance(Key.W);
                        break;
                    case 5 when elapsed > 0.6f:
                        Check(player.transform.position.z < 3.5f, "Wall collision regression");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Check(sawRun && sawJump && sawFall && sawLanding && sawRoll, "Missing animation state evidence");
                        Debug.Log($"PLAYER_VISUAL_PLAY_PASS: frame={Time.frameCount}, model, animated feet, idle loop, run leg motion, rise/fall/landing once each, pose continuity, synchronized roll, unchanged physics, movement, facing, jump, roll, wall");
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

        private static float SoleHeight()
        {
            float min = float.PositiveInfinity;
            var mesh = new Mesh();
            foreach (var renderer in animation.Animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                    min = Mathf.Min(min, renderer.transform.TransformPoint(vertex).y - player.transform.position.y);
            }
            Object.DestroyImmediate(mesh);
            return min;
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

        private static void CaptureFront(string filename = "player-visual-front.png")
        {
            var a = animation.Animator;
            Debug.Log($"ANIMATED_POSE {filename} avatar={a.avatar.name} root={a.transform.position} body={a.bodyPosition} hips={a.GetBoneTransform(HumanBodyBones.Hips).position} foot={a.GetBoneTransform(HumanBodyBones.LeftFoot).position}");
            var cameraObject = new GameObject("Capture Camera");
            var camera = cameraObject.AddComponent<Camera>();
            Vector3 offset = filename.Contains("recovery") ? new Vector3(3f, 1.2f, 0f) : new Vector3(0f, 1.2f, 3f);
            camera.transform.position = player.transform.position + offset;
            camera.transform.LookAt(player.transform.position + Vector3.up * 0.9f);
            camera.orthographic = true;
            camera.orthographicSize = 1.2f;
            var instructions = GameObject.Find("Instructions");
            if (instructions != null) instructions.SetActive(false);
            Capture(filename, camera);
            if (instructions != null) instructions.SetActive(true);
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
