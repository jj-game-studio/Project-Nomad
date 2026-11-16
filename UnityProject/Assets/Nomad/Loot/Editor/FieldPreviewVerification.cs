using System;
using System.IO;
using System.Linq;
using Nomad.Player;
using Nomad.Props;
using Nomad.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.Loot.Editor
{
    public static class FieldPreviewVerification
    {
        private const string SessionKey = "Nomad.Loot.FieldPreviewVerify";
        private static double deadline;
        private static int lastFrame;
        private static int stage;
        private static int waitUntil;
        private static Keyboard keyboard;
        private static TerrainGenerator generator;
        private static PropScatter scatter;
        private static FieldPreview preview;
        private static PlayerMovement player;
        private static int seed;
        private static string id;
        private static string signature;
        private static Vector3 origin;
        private static TerrainPiece oldPiece;

        public static void Run()
        {
            LootVerification.OpenPreview();
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
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
                    preview = Object.FindAnyObjectByType<FieldPreview>();
                    generator = Object.FindAnyObjectByType<TerrainGenerator>();
                    scatter = Object.FindAnyObjectByType<PropScatter>();
                    player = Object.FindAnyObjectByType<PlayerMovement>();
                    CheckPlayer();
                    CheckHud();
                    seed = preview.Seed;
                    id = generator.LastResult.GenerationId;
                    oldPiece = generator.LastResult.Pieces[0];
                    Queue(Key.R);
                    return;
                }

                switch (stage++)
                {
                    case 0:
                        Require(preview.Seed != seed && generator.LastResult.GenerationId != id, "R new seed + generation");
                        Require(oldPiece == null || !oldPiece.gameObject.activeInHierarchy, "old terrain inactive");
                        CheckPlayer();
                        CheckHud();
                        signature = Signature();
                        seed = preview.Seed;
                        id = generator.LastResult.GenerationId;
                        Queue(Key.T);
                        break;
                    case 1:
                        Require(preview.Seed == seed && id != generator.LastResult.GenerationId, "T same seed + new generation");
                        Require(signature == Signature(), "T identical terrain and props");
                        CheckHud();
                        origin = player.transform.position;
                        Queue(Key.W);
                        waitUntil = Time.frameCount + 12;
                        break;
                    case 2:
                        Require(Vector3.Distance(origin, player.transform.position) > 0.4f, "WASD movement");
                        CheckCamera();
                        Queue();
                        var body = player.GetComponent<CharacterController>();
                        body.enabled = false;
                        player.transform.position = new Vector3(0, -100, 0);
                        body.enabled = true;
                        waitUntil = Time.frameCount + 2;
                        break;
                    case 3:
                        CheckPlayer();
                        Require(Vector3.Distance(player.transform.position, origin) < 0.5f, "fall recovery");
                        Queue(Key.R);
                        waitUntil = Time.frameCount + 2;
                        break;
                    case 4:
                        Require(preview.Seed != seed, "repeated R");
                        CheckPlayer();
                        CheckHud();
                        Queue();
                        waitUntil = Time.frameCount + 3;
                        break;
                    default:
                        Require(oldPiece == null, "deferred terrain cleanup");
                        Require(Object.FindObjectsByType<TerrainPiece>(FindObjectsSortMode.None).Length == 12, "only current terrain alive");
                        Require(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                            .Count(t => t.name.StartsWith("GeneratedProps_")) == 1, "only current prop root alive");
                        CheckCamera();
                        Capture();
                        Debug.Log($"FIELD_PREVIEW_PLAYMODE_PASS frame={Time.frameCount} time={Time.time:F2}: R/T keyboard, replay, HUD, spawn, movement, player camera, cleanup, fall recovery");
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

        private static void CheckPlayer()
        {
            Require(player != null && player.isActiveAndEnabled && generator.LastResult.Success, "active player on valid field");
            Vector3 foot = player.transform.position;
            Require(Physics.Raycast(foot + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 0.6f,
                ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<TerrainPiece>() != null, "player floor support");
        }

        private static void CheckCamera()
        {
            Require(Camera.main.GetComponent<TopDownCamera>() != null, "existing player camera reused");
            Require(Vector3.Distance(Camera.main.transform.position, player.transform.position + new Vector3(0, 12, -8)) < 0.1f,
                "camera follows moving player");
        }

        private static void CheckHud()
        {
            Require(preview.Summary.Contains("Seed " + preview.Seed)
                && preview.Summary.Contains("Props " + scatter.Positions.Count)
                && preview.Summary.Contains("Ready"), "seed/count HUD refreshed");
        }

        private static string Signature()
        {
            return string.Join(";", generator.LastResult.Pieces.Select(p => p.name + p.transform.position.ToString("F4") + p.transform.eulerAngles))
                + string.Join(";", scatter.Positions.Select(p => p.ToString("F4")));
        }

        private static void Queue(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static void Capture()
        {
            Directory.CreateDirectory("Artifacts");
            var camera = Camera.main;
            var canvas = Object.FindAnyObjectByType<Canvas>();
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Artifacts/field-preview.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("FIELD_PREVIEW_FAIL: " + message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
