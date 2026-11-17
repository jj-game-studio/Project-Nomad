using System;
using System.IO;
using Nomad.Combat;
using Nomad.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Nomad.Hud.Editor
{
    public static class HudAuthoring
    {
        public const string ScenePath = "Assets/Nomad/Hud/HudPreview.unity";
        private const string VerifyKey = "Nomad.Hud.Verify";
        private static PlayerHud hud;
        private static Health health;
        private static int stage;
        private static int frame;
        private static double deadline;
        private static string failure;
        private static RenderTexture captureTarget;
        private static int captureIndex;
        private static int captureFrame;
        private static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(2560, 1080) };

        [MenuItem("Nomad/HUD/Create Preview")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // TMP가 처음 사용되는 프로젝트이므로 HUD에 필요한 기본 설정을 준비합니다.
            if (TMP_Settings.instance == null)
            {
                var settings = ScriptableObject.CreateInstance<TMP_Settings>();
                var data = new SerializedObject(settings);
                data.FindProperty("m_defaultFontAsset").objectReferenceValue = Resources.Load<TMP_FontAsset>("NomadHudFont");
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(settings, "Assets/Nomad/Hud/Resources/TMP Settings.asset");
                AssetDatabase.SaveAssets();
            }

            Scene scene = EditorSceneManager.OpenScene("Assets/Nomad/Player/A_PlayerCombat.unity");
            PlayerCombat player = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            player.gameObject.AddComponent<PlayerHud>();
            player.gameObject.AddComponent<HudPreview>();
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("HUD_AUTHORING_PASS " + ScenePath);
        }

        [MenuItem("Nomad/HUD/Open Preview")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!File.Exists(ScenePath))
                Create();
            else
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Nomad/HUD/Verify Preview")]
        public static void Verify()
        {
            stage = 0;
            frame = 0;
            captureIndex = 0;
            failure = null;
            hud = null;
            Create();
            SessionState.SetBool(VerifyKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(VerifyKey, false))
                return;

            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayState;
            EditorApplication.playModeStateChanged += PlayState;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("HUD verification timed out at " + stage);
                if (!Application.isPlaying || Time.frameCount < 8)
                    return;
                if (frame == Time.frameCount)
                    return;
                frame = Time.frameCount;

                if (hud == null)
                {
                    Application.runInBackground = true;
                    hud = UnityEngine.Object.FindAnyObjectByType<PlayerHud>();
                    health = hud.GetComponent<Health>();
                }

                Check(hud != null && health != null, "HUD and live Health exist");
                switch (stage)
                {
                    case 0:
                        Check(hud.HealthLabel == "100 / 100", "initial Health");
                        hud.GetComponent<HudPreview>().enabled = false;
                        hud.SetResources(0, 100, 100, 100);
                        Check(hud.ManaFraction == 0 && hud.StaminaFraction == 1, "empty/full resources");
                        health.TryApplyDamage(25);
                        break;
                    case 1:
                        Check(hud.HealthLabel == "75 / 100", "live damage update");
                        hud.SetResources(50, 100, 30, 100);
                        Check(Mathf.Approximately(hud.ManaFraction, .5f) && Mathf.Approximately(hud.StaminaFraction, .3f), "partial resources");
                        health.TryApplyDamage(100);
                        break;
                    case 2:
                        Check(hud.HealthLabel == "0 / 100", "death update");
                        hud.SetResources(-10, 100, 200, 100);
                        Check(hud.ManaFraction == 0 && hud.StaminaFraction == 1, "clamp");
                        hud.SetResources(float.NaN, 100, 10, 0);
                        Check(hud.ManaFraction == 0 && hud.StaminaFraction == 0, "invalid/unavailable resources");
                        foreach (var graphic in hud.Canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                        {
                            Check(graphic.canvasRenderer != null, "CanvasRenderer exists: " + graphic.name);
                            Check(!graphic.raycastTarget, "display graphics do not intercept input");
                        }
                        Check(hud.Canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null, "no HUD raycaster");
                        Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "single EventSystem");
                        hud.enabled = false;
                        Check(!hud.Canvas.gameObject.activeSelf, "HUD hides on disable");
                        hud.enabled = true;
                        Check(hud.Canvas.gameObject.activeSelf, "HUD returns on enable");
                        break;
                    case 3:
                    case 4:
                    case 5:
                        VerifyLayout(Sizes[stage - 3]);
                        break;
                    case 6:
                        // 새로운 플레이어로 재시작 화면을 만들어 최종 시안을 캡처합니다.
                        HudPreview.ResetPreview();
                        break;
                    case 7:
                        hud = UnityEngine.Object.FindAnyObjectByType<PlayerHud>();
                        if (hud == null || hud.HealthLabel != "100 / 100")
                            return;
                        PrepareCapture();
                        break;
                    case 8:
                        if (Time.frameCount - captureFrame < 3)
                            return;
                        Capture();
                        captureIndex++;
                        if (captureIndex < Sizes.Length)
                        {
                            PrepareCapture();
                            return;
                        }
                        Debug.Log($"HUD_PLAYMODE_PASS frame={Time.frameCount} health/resources/layout/input/lifecycle/restart");
                        SessionState.SetBool(VerifyKey, false);
                        EditorApplication.update -= Tick;
                        EditorApplication.ExitPlaymode();
                        return;
                }
                stage++;
            }
            catch (Exception exception)
            {
                failure = exception.ToString();
                Debug.LogException(exception);
                SessionState.SetBool(VerifyKey, false);
                EditorApplication.update -= Tick;
                EditorApplication.ExitPlaymode();
            }
        }

        private static void PrepareCapture()
        {
            Vector2Int size = Sizes[captureIndex];
            captureTarget = new RenderTexture(size.x, size.y, 24);
            Camera.main.targetTexture = captureTarget;
            hud.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            hud.Canvas.worldCamera = Camera.main;
            hud.Canvas.planeDistance = 1f;
            captureFrame = Time.frameCount;
        }

        private static void Capture()
        {
            Vector2Int size = Sizes[captureIndex];
            var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in hud.Canvas.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Check(text.mesh != null && text.textInfo.characterCount > 0,
                        "text mesh renders: " + text.name);
                }
                Camera.main.Render();
                RenderTexture.active = captureTarget;
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts/hud");
                File.WriteAllBytes($"Artifacts/hud/hud-{size.x}x{size.y}.png", pixels.EncodeToPNG());
            }
            finally
            {
                Camera.main.targetTexture = null;
                hud.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hud.Canvas.worldCamera = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(captureTarget);
            }
        }

        private static void VerifyLayout(Vector2Int size)
        {
            var scaler = hud.Canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            Vector2 reference = scaler.referenceResolution;
            float scale = Mathf.Sqrt((size.x / reference.x) * (size.y / reference.y));
            Vector2 logicalSize = (Vector2)size / scale;
            RectTransform bar = (RectTransform)hud.Canvas.transform.Find("Bottom Bar");
            Check(bar.rect.width <= logicalSize.x, $"bar fits {size}");
            foreach (RectTransform child in bar)
                Check(Mathf.Abs(child.anchoredPosition.x) + child.rect.width / 2 <= bar.rect.width / 2,
                    "groups fit bottom bar: " + child.name);
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in hud.Canvas.GetComponentsInChildren<TMP_Text>())
                Check(text.font != null && !text.isTextOverflowing, "text fits: " + text.name);
            Debug.Log("HUD_LAYOUT_PASS " + size);
        }

        private static void PlayState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;
            EditorApplication.playModeStateChanged -= PlayState;
            if (Application.isBatchMode)
                EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("HUD: " + message);
        }
    }
}
