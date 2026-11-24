using System;
using System.IO;
using System.Linq;
using Nomad.Monsters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Nomad.Visibility.Editor
{
    public static class RenderRegressionVerification
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("배치 전용 검증입니다.");
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                VerifySkeleton();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                VerifyOffscreenShadow();
                Debug.Log("RENDER_REGRESSION_PASS skeleton_partial=pass hiding_restore=pass offscreen_shadow=pass");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("RENDER_REGRESSION_FAIL " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void VerifySkeleton()
        {
            Transform observer = new GameObject("Observer").transform;
            PlayerVisibility sight = new GameObject("Sight").AddComponent<PlayerVisibility>();
            sight.Configure(observer, null);
            MonsterAppearanceSet catalog = AssetDatabase.LoadAssetAtPath<MonsterAppearanceSet>(
                "Assets/ThirdParty/Nomad/Monsters/Skeletons/SkeletonAppearances.asset");
            GameObject skeleton = new GameObject("Skeleton actor");
            GameObject model = Object.Instantiate(catalog.normal[0], skeleton.transform);
            model.name = "Body";
            skeleton.transform.position = Vector3.forward * 8;
            var capsule = skeleton.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = Vector3.up * 0.9f;
            Renderer[] bodies = skeleton.GetComponentsInChildren<Renderer>();
            Bounds bounds = bodies[0].bounds;
            foreach (Renderer body in bodies) bounds.Encapsulate(body.bounds);
            Vector3 head = bounds.center + Vector3.up * bounds.extents.y * 0.95f;
            float low = (sight.Eye.y + 1.485f) * 0.5f;
            float high = (sight.Eye.y + head.y) * 0.5f;
            Require(high > low + 0.03f, "실제 해골 머리가 몸통 판정점보다 높음");
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            float top = (low + high) * 0.5f;
            wall.transform.position = new Vector3(0, top * 0.5f, 4);
            wall.transform.localScale = new Vector3(8, top, 0.1f);
            Physics.SyncTransforms();
            Require(!sight.CanSee(new Vector3(0, 1.485f, 8), skeleton.transform) &&
                sight.CanSee(head, skeleton.transform), "몸통은 가려지고 실제 머리는 보이는 재현 조건");
            VisibilityTarget target = skeleton.AddComponent<VisibilityTarget>();
            JsonUtility.FromJsonOverwrite("{\"hideDelay\":0}", target);
            target.Evaluate(sight);
            Require(target.IsVisible && bodies.All(body => !body.forceRenderingOff), "부분 노출 해골 표시");

            wall.transform.localScale = new Vector3(8, 10, 0.1f);
            // 모델 밖 공격 표시가 시야 판정 범위를 늘리지 않아야 합니다.
            GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Cube);
            effect.transform.SetParent(skeleton.transform, false);
            effect.transform.localPosition = Vector3.back * 20;
            Physics.SyncTransforms();
            target.Evaluate(sight);
            Require(!target.IsVisible && bodies.All(body => body.forceRenderingOff &&
                body.shadowCastingMode == ShadowCastingMode.On), "완전히 가린 모델은 그림자까지 숨김");
            sight.Suppressed = true;
            target.Evaluate(sight);
            Require(!target.Controlled && bodies.All(body => !body.forceRenderingOff), "숨김 해제 후 원래 상태 복원");
        }

        private static void VerifyOffscreenShadow()
        {
            Camera camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 20, -10);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.aspect = 1;
            camera.farClipPlane = 100;
            Light light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Hard;
            light.transform.rotation = Quaternion.LookRotation(new Vector3(0, -0.35f, -1));
            RenderSettings.sun = light;
            RenderSettings.ambientLight = Color.gray;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 12;
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject tree = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tree.transform.position = new Vector3(0, 6, 22);
            tree.transform.localScale = new Vector3(3, 10, 3);
            Renderer caster = tree.GetComponent<Renderer>();
            caster.sharedMaterial = new Material(Shader.Find("Nomad/Visibility/Foliage Fade"));
            Require(!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), caster.bounds), "그림자 물체는 화면 밖");
            caster.enabled = false;
            Color32[] baseline = Capture(camera, null);
            caster.enabled = true;
            Color32[] shadow = Capture(camera, "render174-offscreen-shadow.png");
            int darkened = baseline.Zip(shadow, (a, b) => a.r > b.r + 12 && a.g > b.g + 12).Count(value => value);
            Require(darkened > 30, "화면 밖 물체의 실제 그림자 픽셀: " + darkened);
        }

        private static Color32[] Capture(Camera camera, string filename)
        {
            var target = new RenderTexture(256, 256, 24);
            var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                pixels.Apply();
                if (filename != null)
                {
                    Directory.CreateDirectory("Artifacts");
                    File.WriteAllBytes("Artifacts/" + filename, pixels.EncodeToPNG());
                }
                return pixels.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
