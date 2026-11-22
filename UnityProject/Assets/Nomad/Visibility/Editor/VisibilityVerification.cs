using System;
using System.IO;
using Nomad.Combat;
using Nomad.Monsters;
using Nomad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.Visibility.Editor
{
    public static class VisibilityVerification
    {
        private const string Key = "Nomad.Visibility.Verify";
        private static PlayerVisibility sight;
        private static VisibilityField field;
        private static Transform player;
        private static VisibilityTarget front;
        private static VisibilityTarget behind;
        private static VisibilityTarget rear;
        private static Mouse mouse;
        private static double deadline;
        private static float started;
        private static int stage;
        private static int checks;
        private static Material[] foliageMaterials;
        private static Renderer foliageRenderer;

        [MenuItem("Nomad/Visibility/Verify Visibility")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(VisibilityAuthoring.ScenePath);
            SessionState.SetBool(Key, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90f;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Visibility verification timeout stage " + stage);
                if (!EditorApplication.isPlaying || Time.time < 0.2f) return;
                if (sight == null) Initialize();
                if (Time.time - started < 0.2f) return;
                VerifyStage();
                started = Time.time;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void Initialize()
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            sight = Object.FindFirstObjectByType<PlayerVisibility>();
            field = Object.FindFirstObjectByType<VisibilityField>();
            player = sight.Observer;
            front = GameObject.Find("Visible enemy").GetComponent<VisibilityTarget>();
            behind = GameObject.Find("Behind wall").GetComponent<VisibilityTarget>();
            rear = GameObject.Find("Behind player").GetComponent<VisibilityTarget>();
            foreach (VisibilityTarget target in Object.FindObjectsByType<VisibilityTarget>(FindObjectsSortMode.None))
                JsonUtility.FromJsonOverwrite("{\"hideDelay\":0}", target);
            mouse = InputSystem.AddDevice<Mouse>();
            Point(Vector3.forward);
            started = Time.time;
        }

        private static void Point(Vector3 direction)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(player.position + Vector3.up * 1.2f + direction * 6f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(screen.x, screen.y) });
        }

        private static void Evaluate()
        {
            sight.SetDirection(Vector3.forward);
            Physics.SyncTransforms();
            front.Evaluate(sight);
            behind.Evaluate(sight);
            rear.Evaluate(sight);
        }

        private static void VerifyStage()
        {
            switch (stage++)
            {
                case 0:
                    Check(Vector3.Dot(sight.Forward, Vector3.forward) > 0.99f, "actual mouse controls sight direction");
                    Evaluate();
                    Check(front.IsVisible && !behind.IsVisible && !rear.IsVisible, "front / wall / rear");
                    Check(behind.GetComponentInChildren<Renderer>().forceRenderingOff, "hidden body");
                    Check(!behind.GetComponent<SegmentedHealthBar>().enabled, "hidden health bar");
                    Check(behind.GetComponent<Collider>().enabled && behind.GetComponent<Health>().enabled, "collision and health maintained");
                    Check(!sight.InCone(player.position + Vector3.forward * 25f), "range boundary");
                    Check(sight.InCone(player.position + Vector3.forward * 23f), "inside range");
                    Check(!sight.InCone(player.position + Vector3.right * 5f), "outside cone");
                    Check(sight.InCone(player.position + Quaternion.Euler(0f, 59f, 0f) * Vector3.forward * 5f), "inside cone edge");
                    Check(!sight.InCone(player.position + Quaternion.Euler(0f, 61f, 0f) * Vector3.forward * 5f), "outside cone edge");

                    var open = Capture("visibility-initial.png");
                    Shader.SetGlobalFloat("_NomadVisibilityActive", 0f);
                    var closed = Capture("visibility-foliage-closed.png");
                    Shader.SetGlobalFloat("_NomadVisibilityActive", 1f);
                    Check(closed.green > open.green + 50, "rendered foliage opens only with visibility mask");
                    Check(open.red > closed.red + 20, "rendered enemy inside foliage is revealed");
                    Point(Vector3.back);
                    break;
                case 1:
                    Check(Vector3.Dot(sight.Forward, Vector3.back) > 0.99f, "mouse reversal independent of player movement");
                    rear.Evaluate(sight);
                    Check(rear.IsVisible, "rear revealed after aiming");
                    Object.Destroy(GameObject.Find("Wall"));
                    Point(Vector3.forward);
                    break;
                case 2:
                    Evaluate();
                    Check(behind.IsVisible, "wall removal reveals enemy");
                    Check(!sight.CanSee(new Vector3(0f, -2f, 3f)), "ground blocks below terrain");
                    Check(sight.CanSee(new Vector3(4f, 1.7f, 6f)), "see over low cover");
                    Check(behind.GetComponent<Health>().TryApplyDamage(1), "damage works independent of visibility");

                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Verification wall";
                    wall.transform.position = front.transform.position - Vector3.forward * 1.5f + Vector3.up;
                    wall.transform.localScale = new Vector3(3f, 5f, 0.4f);
                    break;
                case 3:
                    Evaluate();
                    Check(!front.IsVisible, "new wall hides existing target");
                    front.GetComponent<Health>().TryApplyDamage(2);
                    Check(front.GetComponent<Health>().CurrentHealth < front.GetComponent<Health>().MaxHealth, "hidden target still takes damage");

                    GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    effect.name = "Late hidden effect";
                    effect.transform.SetParent(front.transform, false);
                    Object.Destroy(effect.GetComponent<Collider>());
                    break;
                case 4:
                    Check(GameObject.Find("Late hidden effect").GetComponent<Renderer>().forceRenderingOff, "dynamic hidden renderer before draw");
                    Check(!front.GetComponent<SegmentedHealthBar>().enabled, "hidden HUD stays off");

                    Renderer body = rear.GetComponentInChildren<Renderer>();
                    rear.Release();
                    body.forceRenderingOff = true;
                    rear.Evaluate(sight);
                    rear.Release();
                    Check(body.forceRenderingOff, "preserve preexisting forceRenderingOff");
                    body.forceRenderingOff = false;

                    field.enabled = false;
                    sight.enabled = false;
                    Check(!front.GetComponentInChildren<Renderer>().forceRenderingOff, "module removal restores renderer");
                    Check(front.GetComponent<SegmentedHealthBar>().enabled, "module removal restores HUD");
                    Check(Shader.GetGlobalFloat("_NomadVisibilityActive") == 0f, "module removal disables cutaway");
                    sight.enabled = true;
                    field.enabled = true;
                    break;
                case 5:
                    Evaluate();
                    Check(!front.IsVisible, "module restart reapplies occlusion");
                    var foliage = Object.FindFirstObjectByType<VisibilityFoliage>();
                    foliageRenderer = foliage.GetComponentsInChildren<Renderer>()[1];
                    foliage.enabled = false;
                    foliageMaterials = foliageRenderer.sharedMaterials;
                    foliage.enabled = true;
                    Check(foliageRenderer.sharedMaterial.shader.name == "Nomad/Visibility/Foliage Cutaway", "instance foliage shader");
                    Check(foliageMaterials[0].shader.name != "Nomad/Visibility/Foliage Cutaway", "source material unchanged");
                    foliage.enabled = false;
                    Check(foliageRenderer.sharedMaterial == foliageMaterials[0], "foliage disable restores materials");
                    foliage.enabled = true;
                    Check(!ShaderUtil.ShaderHasError(foliageRenderer.sharedMaterial.shader), "shader compile");

                    player.gameObject.SetActive(false);
                    break;
                case 6:
                    Check(!front.Controlled && !front.GetComponentInChildren<Renderer>().forceRenderingOff, "inactive observer releases targets");
                    player.gameObject.SetActive(true);
                    player.position = new Vector3(0f, 0.1f, 0f);
                    break;
                case 7:
                    Evaluate();
                    Check(!rear.IsVisible, "teleport invalidates stale visibility");
                    Capture("visibility-forward.png");
                    Point(Vector3.back);
                    break;
                case 8:
                    Capture("visibility-rear.png");
                    Debug.Log("VISIBILITY_PLAYMODE_PASS checks=" + checks + " frames=" + Time.frameCount);
                    Finish(0);
                    break;
            }
        }

        private static (int green, int red) Capture(string name)
        {
            Directory.CreateDirectory("Artifacts/visibility");
            Camera camera = Camera.main;
            var texture = new RenderTexture(1280, 720, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes("Artifacts/visibility/" + name, image.EncodeToPNG());
            int green = 0;
            int red = 0;
            foreach (Color pixel in image.GetPixels())
            {
                if (pixel.g > pixel.r * 1.8f && pixel.g > pixel.b * 1.8f) green++;
                if (pixel.r > pixel.g * 1.8f && pixel.r > pixel.b * 1.8f) red++;
            }
            Debug.Log("VISIBILITY_RENDER " + name + " green=" + green + " red=" + red);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.Destroy(image);
            Object.Destroy(texture);
            return (green, red);
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Visibility: " + label);
            checks++;
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}

