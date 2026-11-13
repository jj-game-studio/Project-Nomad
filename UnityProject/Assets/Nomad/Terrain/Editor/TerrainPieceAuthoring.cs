using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Terrain.Editor
{
    public static class TerrainPieceAuthoring
    {
        public const string Output = "Assets/ThirdParty/Nomad/Terrain";
        private const string Source = "Assets/ThirdParty/Synty_Prototype/PolygonPrototype/Prefabs/Buildings/Simple/";
        private static readonly string[] Names = { "Flat", "Stairs", "Raised" };

        [MenuItem("Nomad/Terrain/Create B Prototype Scene")]
        public static void Create()
        {
            // 재실행 시 기존 장면의 미저장 편집을 버리지 않습니다.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();

            for (int i = 0; i < Names.Length; i++)
                CreatePiece((TerrainPieceKind)i);

            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("TERRAIN_AUTHORING_PASS: 프리팹 3종 및 B_TerrainPieces.unity 생성");
        }

        private static void CreatePiece(TerrainPieceKind kind)
        {
            var root = new GameObject(Names[(int)kind]);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var collision = new GameObject("Collision");
            collision.transform.SetParent(root.transform, false);

            float top = kind == TerrainPieceKind.Raised ? 2f : 0f;
            if (kind == TerrainPieceKind.Stairs)
            {
                AddFloor(visual.transform, collision.transform, "LowerLanding",
                    new Bounds(new Vector3(0f, -0.125f, -3.5f), new Vector3(4f, 0.25f, 3f)));
                AddFloor(visual.transform, collision.transform, "UpperLanding",
                    new Bounds(new Vector3(0f, 1.875f, 3.5f), new Vector3(4f, 0.25f, 3f)));
                AddStairs(visual.transform, collision.transform, 0f, -1f);
                AddStairs(visual.transform, collision.transform, 1f, 1f);
            }
            else
            {
                AddFloor(visual.transform, collision.transform, "Floor",
                    new Bounds(new Vector3(0f, top - 0.125f, 0f), new Vector3(10f, 0.25f, 10f)));
            }

            var south = AddConnection(root.transform, "South", new Vector3(0f, top, -5f), 180f);
            var north = AddConnection(root.transform, "North",
                new Vector3(0f, kind == TerrainPieceKind.Stairs ? 2f : top, 5f), 0f);
            float maxHeight = kind == TerrainPieceKind.Flat ? 0f : 2f;
            var occupancy = new Bounds(new Vector3(0f, (maxHeight - 0.25f) * 0.5f, 0f),
                new Vector3(10f, maxHeight + 0.25f, 10f));

            root.AddComponent<TerrainPiece>().Configure(kind, occupancy, new[] { south, north });
            PrefabUtility.SaveAsPrefabAsset(root, $"{Output}/{Names[(int)kind]}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static TerrainConnection AddConnection(Transform parent, string name, Vector3 position, float yaw)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.localPosition = position;
            point.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return point.AddComponent<TerrainConnection>();
        }

        private static void AddFloor(Transform visual, Transform collision, string name, Bounds bounds)
        {
            FitVisual("SM_Buildings_Floor_5x5_01.prefab", visual, name, bounds);
            AddBox(collision, name, bounds);
        }

        private static void AddStairs(Transform visual, Transform collision, float baseHeight, float centerZ)
        {
            Vector3 center = new Vector3(0f, baseHeight + 0.5f, centerZ);
            var stairs = FitVisual("SM_Buildings_Stairs_1x2_01.prefab", visual, $"Stairs_{baseHeight}",
                new Bounds(center, new Vector3(4f, 1f, 2f)));

            // 원본 계단의 단 높이와 진행 방향을 메시에서 읽어 같은 형상의 충돌체를 만듭니다.
            var filter = stairs.GetComponentInChildren<MeshFilter>();
            var mesh = filter.sharedMesh;
            Vector3[] vertices = mesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
            float[] levels = vertices.Select(v => (float)Math.Round(v.y, 4)).Distinct().OrderBy(y => y).ToArray();
            float[] tops = levels.Where(y => y > baseHeight + 0.001f).ToArray();
            if (tops.Length == 0 || tops.Zip(new[] { baseHeight }.Concat(tops), (a, b) => a - b).Any(h => h > 0.251f))
                throw new InvalidOperationException($"원본 계단 단 높이가 0.25m 규격을 초과합니다: {string.Join(", ", levels)}");

            // 상단의 수평 면을 찾아 +Z 쪽이 높아지게 정렬합니다.
            int[] indices = mesh.triangles;
            float highFaceZ = 0f;
            int highFaceCount = 0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]];
                Vector3 b = vertices[indices[i + 1]];
                Vector3 c = vertices[indices[i + 2]];
                if (Mathf.Abs(a.y - (baseHeight + 1f)) < 0.001f && Mathf.Abs(b.y - (baseHeight + 1f)) < 0.001f
                    && Mathf.Abs(c.y - (baseHeight + 1f)) < 0.001f)
                {
                    highFaceZ += (a.z + b.z + c.z) / 3f;
                    highFaceCount++;
                }
            }

            if (highFaceCount == 0)
                throw new InvalidOperationException("계단 상단 면을 찾지 못했습니다.");

            bool reverse = highFaceZ / highFaceCount < centerZ;
            if (reverse)
                stairs.transform.RotateAround(center, Vector3.up, 180f);

            // 각 수평 단의 실제 Z 구간을 사용하므로 외형과 발판이 일치합니다.
            foreach (float height in tops)
            {
                var faceZ = Enumerable.Range(0, indices.Length / 3)
                    .Select(i => new[] { vertices[indices[i * 3]], vertices[indices[i * 3 + 1]], vertices[indices[i * 3 + 2]] })
                    .Where(t => t.All(v => Mathf.Abs(v.y - height) < 0.001f))
                    .SelectMany(t => t.Select(v => reverse ? 2f * centerZ - v.z : v.z)).ToArray();

                if (faceZ.Length == 0)
                    throw new InvalidOperationException($"계단 {height}m 수평 면이 없습니다.");

                float minZ = faceZ.Min();
                float maxZ = faceZ.Max();
                AddBox(collision, $"Step_{height:F2}",
                    new Bounds(new Vector3(0f, height * 0.5f, (minZ + maxZ) * 0.5f),
                        new Vector3(4f, height, maxZ - minZ)));
            }

            Debug.Log($"TERRAIN_STAIRS: {tops.Length}단, 높이={string.Join(", ", tops)}, reverse={reverse}");
        }

        private static GameObject FitVisual(string filename, Transform parent, string name, Bounds target)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + filename);
            if (source == null)
                throw new InvalidOperationException($"프로토타입 애셋이 없습니다: {filename}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // 외형 아래에는 충돌을 두지 않아, 아트 교체가 통행 판정을 바꾸지 않게 합니다.
            foreach (var collider in instance.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(collider);

            Bounds bounds = RendererBounds(instance);
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale,
                new Vector3(target.size.x / bounds.size.x, target.size.y / bounds.size.y, target.size.z / bounds.size.z));
            instance.transform.position += target.center - RendererBounds(instance).center;
            return instance;
        }

        private static Bounds RendererBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);

            return bounds;
        }

        private static void AddBox(Transform parent, string name, Bounds bounds)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var collider = go.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
        }

        private static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            for (int i = 0; i < Names.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Output}/{Names[i]}.prefab");
                var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                piece.transform.position = new Vector3(0f, 0f, i * 10f);
            }

            var probe = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            probe.name = "B_TraversalProbe_Automatic";
            UnityEngine.Object.DestroyImmediate(probe.GetComponent<CapsuleCollider>());
            probe.transform.position = new Vector3(0f, 0.03f, -3f);
            // 외형 중심만 올리고 캐릭터 원점은 발 위치에 유지합니다.
            var body = new GameObject("Body");
            body.transform.SetParent(probe.transform, false);
            body.transform.localPosition = Vector3.up * 0.9f;
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.AddComponent<MeshFilter>().sharedMesh = probe.GetComponent<MeshFilter>().sharedMesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = probe.GetComponent<MeshRenderer>().sharedMaterial;
            UnityEngine.Object.DestroyImmediate(probe.GetComponent<MeshFilter>());
            UnityEngine.Object.DestroyImmediate(probe.GetComponent<MeshRenderer>());

            var controller = probe.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.02f;
            controller.minMoveDistance = 0f;
            probe.AddComponent<TerrainTraversalProbe>();

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var camera = new GameObject("Overview").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(22f, 24f, -18f);
            camera.transform.LookAt(new Vector3(0f, 0f, 10f));
            camera.fieldOfView = 50f;
            camera.farClipPlane = 150f;
            camera.backgroundColor = new Color(0.13f, 0.16f, 0.21f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            RenderSettings.ambientLight = Color.gray;
            EditorSceneManager.SaveScene(scene, $"{Output}/B_TerrainPieces.unity");
        }

        public static void CreateAndValidate()
        {
            try
            {
                Create();
                TerrainPieceValidation.Validate();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }
    }
}
