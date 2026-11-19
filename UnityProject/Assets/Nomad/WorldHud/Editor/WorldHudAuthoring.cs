using System;
using System.IO;
using Nomad.Combat;
using Nomad.Interaction;
using Nomad.Inventory;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Nomad.WorldHud.Editor
{
    public static class WorldHudAuthoring
    {
        public const string ScenePath = "Assets/Nomad/WorldHud/WorldHudPreview.unity";
        private const string VerifyKey = "Nomad.WorldHud.Verify";
        private static int stage;
        private static double next;
        private static double deadline;
        private static Keyboard keyboard;
        private static Keyboard previousKeyboard;
        private static InputSettings.BackgroundBehavior background;
        private static InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private static float previousCaptureDelta;

        [MenuItem("Nomad/HUD/Open World HUD Preview")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Create();
        }

        public static void Create()
        {
            // 기존 HUD 번들 설정을 명시적으로 읽어 TMP 최초 실행의 모달 창을 피합니다.
            TMPro.TMP_Settings.LoadDefaultSettings();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(new Vector3(0, 9, -6), Quaternion.Euler(56, 0, 0));
            camera.fieldOfView = 50;
            camera.backgroundColor = new Color(.06f, .08f, .12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            Cube("Ground", new Vector3(0, -.3f, 0), new Vector3(18, .5f, 18), new Color(.18f, .24f, .24f));

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            UnityEngine.Object.DestroyImmediate(player.GetComponent<Collider>());
            player.transform.position = Vector3.up;
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2;
            controller.radius = .4f;
            player.AddComponent<Health>();
            player.AddComponent<PlayerInventory>();
            var movement = player.AddComponent<PlayerMovement>();
            SetReference(movement, "viewCamera", camera);
            player.AddComponent<PlayerInteraction>();
            player.AddComponent<CharacterStaminaHud>();
            player.AddComponent<InteractionHints>();
            player.AddComponent<WorldHudPreview>();
            var follow = camera.gameObject.AddComponent<TopDownCamera>();
            SetReference(follow, "target", player.transform);

            Target("A - Chest", new Vector3(-.85f, .45f, .8f), new Color(.55f, .33f, .15f));
            Target("B - Supplies", new Vector3(.85f, .45f, .8f), new Color(.25f, .45f, .55f));
            Target("C - Distant", new Vector3(3.8f, .45f, 2), new Color(.45f, .38f, .6f));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            string path = "Assets/Nomad/WorldHud/" + name.Split(' ')[0] + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null) AssetDatabase.CreateAsset(material, path);
            else { UnityEngine.Object.DestroyImmediate(material); material = existing; }
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static void Target(string name, Vector3 position, Color color) =>
            Cube(name, position, new Vector3(.65f, .75f, .65f), color).AddComponent<WorldHudPreviewTarget>();

        private static void SetReference(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Verify()
        {
            Create();
            stage = 0;
            SessionState.SetBool(VerifyKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(VerifyKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish("timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (stage == 12)
                {
                    Require(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                        "destroying player removes all detached HUD canvases");
                    Finish(null);
                    return;
                }
                var hints = UnityEngine.Object.FindFirstObjectByType<InteractionHints>();
                var stamina = UnityEngine.Object.FindFirstObjectByType<CharacterStaminaHud>();
                if (hints == null || stamina == null) return;
                var preview = hints.GetComponent<WorldHudPreview>();
                if (stage == 0)
                {
                    previousCaptureDelta = Time.captureDeltaTime;
                    Time.captureDeltaTime = 1f / 60f;
                    preview.enabled = false;
                    Screen.SetResolution(1920, 1080, false);
                    previousKeyboard = Keyboard.current;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    background = InputSystem.settings.backgroundBehavior;
                    editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    next = EditorApplication.timeSinceStartup + 1;
                }
                else if (stage == 1)
                {
                    hints.Refresh();
                    Require(hints.VisibleCount == 2, "two nearby markers; distant excluded");
                    Require(hints.Selected.InteractionId == "A - Chest", "ID tie chooses A");
                    Require(stamina.Visible && Mathf.Approximately(stamina.Fraction, .6f), "partial stamina visible");
                    Directory.CreateDirectory("Artifacts/world-hud");
                    Capture("preview-1920.png", 1920, 1080);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    next = EditorApplication.timeSinceStartup + .5;
                }
                else if (stage == 2)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Require(GameObject.Find("A - Chest").GetComponent<WorldHudPreviewTarget>().Interactions == 1, "real E uses highlighted A once");
                    hints.Refresh();
                    Require(hints.VisibleCount == 1 && hints.Selected.InteractionId == "B - Supplies", "used target hidden");
                    stamina.SetStamina(float.NaN, 100);
                    next = EditorApplication.timeSinceStartup + .2;
                }
                else if (stage == 3)
                {
                    Require(!stamina.Visible, "invalid stamina hidden");
                    stamina.SetStamina(100, 100);
                    next = EditorApplication.timeSinceStartup + 1;
                }
                else if (stage == 4)
                {
                    Require(!stamina.Visible, "full stamina auto hides");
                    stamina.NotifyInsufficient();
                    next = EditorApplication.timeSinceStartup + .1;
                }
                else if (stage == 5)
                {
                    Require(stamina.Visible, "insufficient reveals gauge");
                    GameObject.Find("B - Supplies").transform.position = new Vector3(0, .45f, 1.4f);
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "TestWall";
                    wall.transform.position = new Vector3(0, 1, .7f);
                    wall.transform.localScale = new Vector3(2, 3, .15f);
                    next = EditorApplication.timeSinceStartup + .2;
                }
                else if (stage == 6)
                {
                    hints.Refresh();
                    Require(hints.VisibleCount == 0, "wall hides marker");
                    UnityEngine.Object.Destroy(GameObject.Find("TestWall"));
                    stamina.SetStamina(40, 100);
                    hints.GetComponent<PlayerInteraction>().enabled = false;
                    hints.Refresh();
                    Require(hints.Selected == null && hints.VisibleCount == 0, "disabled interaction hides hints");
                    hints.GetComponent<PlayerInteraction>().enabled = true;
                    Screen.SetResolution(1280, 720, false);
                    next = EditorApplication.timeSinceStartup + .5;
                }
                else if (stage == 7)
                {
                    Capture("preview-1280.png", 1280, 720);
                    Camera.main.transform.rotation = Quaternion.Euler(0, 180, 0);
                    Camera.main.GetComponent<TopDownCamera>().enabled = false;
                    hints.Refresh();
                    Require(hints.VisibleCount == 0, "behind camera hidden");
                    Camera.main.GetComponent<TopDownCamera>().enabled = true;
                    Screen.SetResolution(2560, 1080, false);
                    next = EditorApplication.timeSinceStartup + .5;
                }
                else if (stage == 8)
                {
                    Capture("preview-2560.png", 2560, 1080);
                    hints.enabled = false;
                    hints.enabled = true;
                    hints.Refresh();
                    Require(hints.VisibleCount == 1, "reenable restores markers");
                    next = EditorApplication.timeSinceStartup + .5;
                }
                else if (stage == 9)
                {
                    Require(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsSortMode.None).Length == 0,
                        "display-only HUD adds no raycaster");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                    // 배치 Editor의 느린 콜백 사이에 0.3초 구르기가 끝나지 않도록 같은 입력 틱에서 검사합니다.
                    InputSystem.Update();
                    hints.GetComponent<PlayerMovement>().SendMessage("Update");
                    Require(hints.GetComponent<PlayerMovement>().IsRolling, "real Shift starts roll");
                    hints.Refresh();
                    Require(hints.VisibleCount == 0, "rolling hides hints");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    next = EditorApplication.timeSinceStartup + .5;
                }
                else if (stage == 10)
                {
                    hints.GetComponent<CharacterController>().enabled = false;
                    hints.transform.position = new Vector3(10, 1, 10);
                    hints.GetComponent<CharacterController>().enabled = true;
                    hints.Refresh();
                    Require(hints.VisibleCount == 0, "outside reach hidden");
                    stamina.enabled = false;
                    Require(!stamina.Visible, "disabled stamina hidden");
                    stamina.enabled = true;
                    next = EditorApplication.timeSinceStartup + .1;
                }
                else if (stage == 11)
                {
                    VerifyTracking(stamina, hints);
                    Camera.main.GetComponent<TopDownCamera>().enabled = false;
                    UnityEngine.Object.Destroy(stamina.gameObject);
                    next = EditorApplication.timeSinceStartup + .2;
                }
                else { Finish(null); return; }
                stage++;
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        private static void Finish(string failure)
        {
            Time.captureDeltaTime = previousCaptureDelta;
            SessionState.SetBool(VerifyKey, false);
            EditorApplication.update -= Tick;
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null) previousKeyboard.MakeCurrent();
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
            if (failure == null) Debug.Log("WORLD_HUD_PLAYMODE_PASS: stamina, E selection, tie, occlusion, lifecycle, resolutions");
            else Debug.LogError("WORLD_HUD_FAIL: " + failure);
            if (Application.isBatchMode) EditorApplication.Exit(failure == null ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        private static void VerifyTracking(CharacterStaminaHud stamina, InteractionHints hints)
        {
            Camera camera = Camera.main;
            var follow = camera.GetComponent<TopDownCamera>();
            var controller = stamina.GetComponent<CharacterController>();
            controller.enabled = false;
            stamina.SetStamina(50, 100);
            RectTransform arc = GameObject.Find("Character Stamina HUD").transform.Find("Stamina Arc") as RectTransform;
            Canvas canvas = arc.GetComponentInParent<Canvas>();
            float worst = 0;
            // 대상 경계를 왕복하고 방향을 바꾸면서 두 LateUpdate 실행 순서를 모두 검사합니다.
            for (int i = 0; i < 24; i++)
            {
                stamina.transform.SetPositionAndRotation(new Vector3((i % 3 - 1) * .3f, 1, i % 2 == 0 ? .1f : -.2f),
                    Quaternion.Euler(0, i * 45, 0));
                if (i % 2 == 0) follow.SendMessage("LateUpdate");
                stamina.SendMessage("LateUpdate");
                hints.Refresh();
                if (i % 2 != 0) follow.SendMessage("LateUpdate");
                Canvas.ForceUpdateCanvases();

                Vector2 expected = camera.WorldToScreenPoint(stamina.transform.position + Vector3.up);
                expected += new Vector2(100, -60) * canvas.scaleFactor;
                Vector2 actual = RectTransformUtility.WorldToScreenPoint(null, arc.position);
                worst = Mathf.Max(worst, Vector2.Distance(expected, actual));
            }
            Debug.Log($"WORLD_HUD_TRACKING_ERROR pixels={worst:F3}");
            Require(worst < 1, "stamina stays anchored after movement/rotation/target boundary and either camera order");
            controller.enabled = true;
        }

        private static void Capture(string name, int width, int height)
        {
            Camera camera = Camera.main;
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (Canvas canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1;
                }
                Canvas.ForceUpdateCanvases();
                foreach (TMPro.TMP_Text text in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
                {
                    text.ForceMeshUpdate();
                    Require(text.textInfo.characterCount > 0, "text mesh: " + text.name);
                }
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Artifacts/world-hud/" + name, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                foreach (Canvas canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
