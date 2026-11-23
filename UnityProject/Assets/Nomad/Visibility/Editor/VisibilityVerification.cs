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
        private static Keyboard keyboard;
        private static double deadline;
        private static float started;
        private static int stage;
        private static int checks;
        private static GameObject module;
        private const string FadeShaderName = "Nomad/Visibility/Foliage Fade";

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
            keyboard = InputSystem.AddDevice<Keyboard>();
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
                    Check(!sight.ShowDebugRange && sight.GetComponent<VisibilityDebugRange>() == null, "debug off by default without renderers");
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

                    VerifyFoliage();
                    VerifyShade();
                    Point(Vector3.back);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.F3));
                    break;
                case 1:
                    Check(sight.ShowDebugRange, "F3 enables debug range");
                    Transform debug = sight.transform.Find("Visibility Debug Range");
                    Check(debug != null && debug.gameObject.activeSelf,
                        "debug visible in Game view; component=" + (sight.GetComponent<VisibilityDebugRange>() != null) +
                        " children=" + sight.transform.childCount);
                    LineRenderer maximum = debug.Find("Maximum Cone").GetComponent<LineRenderer>();
                    Check(Mathf.Abs(Vector3.Distance(maximum.GetPosition(1), sight.Eye) - sight.Distance) < 0.01f, "debug uses configured distance");
                    Vector3 edge = (maximum.GetPosition(1) - sight.Eye).normalized;
                    Check(Mathf.Abs(Vector3.Angle(edge, sight.Forward) - sight.Angle * 0.5f) < 0.1f, "debug follows aim and configured angle");
                    Check(maximum.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off, "debug casts no shadow");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
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
                    LineRenderer blocked = sight.transform.Find("Visibility Debug Range/Occluded Boundary").GetComponent<LineRenderer>();
                    bool clipped = false;
                    for (int index = 1; index < blocked.positionCount - 1; index++)
                        clipped |= Vector3.Distance(blocked.GetPosition(index), sight.Eye) < sight.Distance - 1f;
                    Check(clipped, "debug boundary stops at wall");
                    Capture("visibility-debug-range.png");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.F3));
                    front.GetComponent<Health>().TryApplyDamage(2);
                    Check(front.GetComponent<Health>().CurrentHealth < front.GetComponent<Health>().MaxHealth, "hidden target still takes damage");

                    GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    effect.name = "Late hidden effect";
                    effect.transform.SetParent(front.transform, false);
                    Object.Destroy(effect.GetComponent<Collider>());
                    break;
                case 4:
                    Check(!sight.ShowDebugRange && !sight.transform.Find("Visibility Debug Range").gameObject.activeSelf, "F3 hides debug range");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    sight.ShowDebugRange = true;
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
                    Check(!sight.transform.Find("Visibility Debug Range").gameObject.activeSelf, "disabled sight hides debug immediately");
                    Check(!front.GetComponentInChildren<Renderer>().forceRenderingOff, "module removal restores renderer");
                    Check(front.GetComponent<SegmentedHealthBar>().enabled, "module removal restores HUD");
                    Check(Shader.GetGlobalFloat("_NomadVisibilityActive") == 0f, "module removal disables visibility mask");
                    Check(!field.ShadeRenderer.enabled, "module removal hides shade");
                    Check(!GameObject.Find("Bush cutaway sample").GetComponent<VisibilityFoliage>().InSight, "module removal returns foliage to opaque");
                    sight.enabled = true;
                    field.enabled = true;
                    break;
                case 5:
                    Check(sight.transform.Find("Visibility Debug Range").gameObject.activeSelf, "Inspector toggle and reenable restore debug");
                    Evaluate();
                    Check(!front.IsVisible, "module restart reapplies occlusion");
                    Check(!ShaderUtil.ShaderHasError(Shader.Find(FadeShaderName)), "fade shader compile");
                    Check(!ShaderUtil.ShaderHasError(Shader.Find("Nomad/Visibility/Shade")), "shade shader compile");

                    player.gameObject.SetActive(false);
                    break;
                case 6:
                    Check(!sight.transform.Find("Visibility Debug Range").gameObject.activeSelf, "inactive observer hides debug");
                    Check(!front.Controlled && !front.GetComponentInChildren<Renderer>().forceRenderingOff, "inactive observer releases targets");
                    player.gameObject.SetActive(true);
                    player.position = new Vector3(0f, 0.1f, 0f);
                    break;
                case 7:
                    Evaluate();
                    Check(!rear.IsVisible, "teleport invalidates stale visibility");
                    VerifyNearby();
                    Capture("visibility-forward.png");
                    Point(Vector3.back);
                    break;
                case 8:
                    Capture("visibility-rear.png");
                    field.enabled = false;
                    sight.enabled = false;
                    module = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        VisibilityAuthoring.Folder + "/PlayerVisibilityModule.prefab"));
                    break;
                case 9:
                    Check(module.GetComponent<PlayerVisibility>().Observer == player, "prefab binds current player without scene reference");
                    Check(front.Controlled, "prefab controls existing targets");
                    Object.Destroy(module);
                    break;
                case 10:
                    Check(!front.Controlled && !front.GetComponentInChildren<Renderer>().forceRenderingOff, "prefab removal restores target");
                    Debug.Log("VISIBILITY_PLAYMODE_PASS checks=" + checks + " frames=" + Time.frameCount);
                    Finish(0);
                    break;
            }
        }

        private static void VerifyNearby()
        {
            float savedRadius = sight.NearbyRadius;
            float savedDistance = sight.Distance;
            Vector3 savedRear = rear.transform.position;
            sight.SetDirection(Vector3.forward);
            sight.NearbyRadius = 3f;

            Check(sight.CanSee(sight.Eye - Vector3.forward * 2f), "nearby rear visible outside cone");
            Check(sight.CanSee(sight.Eye - Vector3.forward * 3f), "nearby edge included");
            Check(!sight.CanSee(sight.Eye - Vector3.forward * 3.1f), "outside nearby rear hidden");
            Check(sight.CanSee(sight.Eye + Vector3.right * 2f), "nearby side visible");
            Check(sight.RangeAtAngle(180f) == 3f && sight.RangeAtAngle(0f) == savedDistance, "cone and nearby ranges combined");

            rear.transform.position = player.position - Vector3.forward * 2f;
            Physics.SyncTransforms();
            rear.Evaluate(sight);
            Check(rear.IsVisible && !rear.GetComponentInChildren<Renderer>().forceRenderingOff, "nearby reveals real rear enemy");
            sight.NearbyRadius = 1.5f;
            rear.Evaluate(sight);
            Check(!rear.IsVisible, "reducing nearby radius hides rear enemy");
            sight.NearbyRadius = 0f;
            Check(!sight.CanSee(sight.Eye - Vector3.forward * 2f), "zero disables nearby vision");
            sight.GetComponent<VisibilityDebugRange>().Configure(sight);
            LineRenderer circle = sight.transform.Find("Visibility Debug Range/Nearby Circle").GetComponent<LineRenderer>();
            Check(!circle.enabled, "zero hides debug circle");

            sight.NearbyRadius = 3f;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Nearby verification wall";
            wall.transform.position = player.position - Vector3.forward + Vector3.up;
            wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            Physics.SyncTransforms();
            rear.Evaluate(sight);
            Check(!rear.IsVisible && !sight.CanSee(sight.Eye - Vector3.forward * 2f), "nearby cannot see through wall");
            sight.GetComponent<VisibilityDebugRange>().Configure(sight);
            LineRenderer clipped = sight.transform.Find("Visibility Debug Range/Nearby Boundary").GetComponent<LineRenderer>();
            Check(Vector3.Distance(clipped.GetPosition(64), sight.Eye) < 1.1f, "nearby debug boundary stops at wall");
            Check(Mathf.Abs(Vector3.Distance(circle.GetPosition(0), sight.Eye) - 3f) < 0.01f, "debug circle uses nearby radius");
            Check(Vector3.Distance(circle.GetPosition(0), circle.GetPosition(circle.positionCount - 1)) < 0.001f, "debug circle closes");

            wall.GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms();
            Check(sight.CanSee(sight.Eye - Vector3.forward * 2f), "nearby ignores trigger");
            wall.GetComponent<Collider>().enabled = false;
            wall.GetComponent<Renderer>().enabled = false;
            Object.Destroy(wall);
            sight.GetComponent<VisibilityDebugRange>().Configure(sight);

            sight.Configure(player, Camera.main, 1f, sight.Angle);
            Check(sight.CanSee(sight.Eye + Vector3.forward * 2f), "nearby can exceed forward distance");
            sight.Configure(player, Camera.main, savedDistance, sight.Angle);
            sight.SetDirection(Vector3.forward);
            rear.Evaluate(sight);
            Transform bush = GameObject.Find("Bush cutaway sample").transform;
            Vector3 savedBushPosition = bush.position;
            Vector3 savedBushScale = bush.localScale;
            bush.position = player.position - Vector3.forward * 2.2f;
            bush.localScale = Vector3.one * 0.6f;
            Physics.SyncTransforms();
            var rearFoliage = bush.GetComponent<VisibilityFoliage>();
            rearFoliage.Evaluate(sight);
            Check(rearFoliage.InSight, "nearby radius fades rear foliage");
            sight.NearbyRadius = 0f;
            rearFoliage.Evaluate(sight);
            Check(!rearFoliage.InSight, "rear foliage outside sight stays opaque");
            sight.NearbyRadius = 3f;
            bush.position = savedBushPosition;
            bush.localScale = savedBushScale;

            sight.NearbyRadius = -1f;
            Check(sight.NearbyRadius == 0f, "nearby radius minimum");
            sight.NearbyRadius = 21f;
            Check(sight.NearbyRadius == 20f, "nearby radius maximum");
            sight.NearbyRadius = savedRadius;
            rear.transform.position = savedRear;
            Physics.SyncTransforms();
            rear.Evaluate(sight);
            sight.GetComponent<VisibilityDebugRange>().Configure(sight);
        }

        // 숲 반투명: 시야 안 덤불은 원래 색의 반투명 재질로 바뀌고, 안쪽 적이 렌더에 드러납니다.
        // 나무: 줄기 충돌은 남지만 시선을 막지 않습니다.
        private static void VerifyFoliage()
        {
            var bush = GameObject.Find("Bush cutaway sample").GetComponent<VisibilityFoliage>();
            Renderer leaves = bush.GetComponentInChildren<Renderer>();
            Color source = AssetDatabase.LoadAssetAtPath<Material>(VisibilityAuthoring.Folder + "/Materials/Foliage.mat").GetColor("_BaseColor");
            Check(bush.InSight && bush.Faded && bush.Alpha < 1f, "foliage in sight fades");
            Check(leaves.sharedMaterial.shader.name == FadeShaderName, "faded foliage uses fade shader");
            Check(leaves.sharedMaterial.GetColor("_BaseColor") == source, "fade keeps source color");

            var open = Capture("visibility-initial.png");
            bush.enabled = false;
            Check(leaves.sharedMaterial.shader.name != FadeShaderName, "foliage disable restores source material");
            var closed = Capture("visibility-foliage-opaque.png");
            bush.enabled = true;
            Check(open.red > closed.red + 20, "rendered enemy inside faded foliage is revealed");

            var tree = GameObject.Find("Tree cutaway sample").GetComponent<VisibilityFoliage>();
            Transform trunk = tree.transform.Find("Trunk");
            Vector3 toTrunk = trunk.position - sight.Eye;
            toTrunk.y = 0f;
            Vector3 beyond = sight.Eye + toTrunk.normalized * (toTrunk.magnitude + 1.2f);
            Check(!tree.BlocksSight && trunk.GetComponent<Collider>().enabled, "tree keeps collision without blocking sight");
            Check(sight.CanSee(beyond), "sight passes through tree trunk");
            Check(tree.InSight, "tree in sight fades");
        }

        // 시야 밖(등 뒤, 벽 뒤)은 어둡고 시야 안은 밝기를 유지합니다.
        private static void VerifyShade()
        {
            Vector3 front = new Vector3(0f, 0f, 3f);
            Vector3 back = player.position - Vector3.forward * 4.5f;
            Vector3 hidden = new Vector3(-5f, 0f, 6f);
            Check(field.ShadeRenderer != null && field.ShadeRenderer.enabled, "shade active with sight");

            var shaded = Capture("visibility-shade.png", front, back, hidden);
            field.ShadeRenderer.enabled = false;
            var plain = Capture("visibility-shade-off.png", front, back, hidden);
            field.ShadeRenderer.enabled = true;
            Debug.Log($"VISIBILITY_SHADE front={shaded.light[0]:F3}/{plain.light[0]:F3} back={shaded.light[1]:F3}/{plain.light[1]:F3} wall={shaded.light[2]:F3}/{plain.light[2]:F3}");
            Check(shaded.light[0] > plain.light[0] * 0.9f, "visible area stays lit");
            Check(shaded.light[1] < plain.light[1] * 0.6f, "area behind player is shaded");
            Check(shaded.light[2] < plain.light[2] * 0.6f, "area behind wall is shaded");
        }

        private static (int green, int red, float[] light) Capture(string name, params Vector3[] probes)
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

            // 지점 주변 5x5 픽셀의 평균 밝기입니다. 화면 밖 지점은 검증 실패로 처리합니다.
            var light = new float[probes.Length];
            for (int index = 0; index < probes.Length; index++)
            {
                Vector3 view = camera.WorldToViewportPoint(probes[index]);
                Check(view.z > 0f && view.x > 0.02f && view.x < 0.98f && view.y > 0.02f && view.y < 0.98f, "probe on screen " + probes[index]);
                int x = Mathf.RoundToInt(view.x * 1279f);
                int y = Mathf.RoundToInt(view.y * 719f);
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        light[index] += image.GetPixel(x + dx, y + dy).grayscale / 25f;
            }

            Debug.Log("VISIBILITY_RENDER " + name + " green=" + green + " red=" + red);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.Destroy(image);
            Object.Destroy(texture);
            return (green, red, light);
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
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}
