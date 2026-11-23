using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Nomad.Minimap
{
    // 생성/지역 전환 때만 만드는 작은 지도입니다. 보조 카메라와 매 프레임 지형 렌더는 사용하지 않습니다.
    public static class MinimapSurface
    {
        private const int Resolution = 96;
        private static readonly Color32 Empty = new Color32(26, 38, 34, 255);
        private static readonly Color32 Ground = new Color32(104, 126, 94, 255);
        private static readonly Color32 Road = new Color32(199, 180, 133, 255);
        private static readonly Color32 Obstacle = new Color32(58, 73, 60, 255);

        public static Texture2D Field(out MinimapProjection projection)
        {
            NavMeshTriangulation nav = NavMesh.CalculateTriangulation();
            if (nav.vertices.Length == 0)
            {
                projection = default;
                return null;
            }
            var bounds = new Bounds(nav.vertices[0], Vector3.zero);
            foreach (Vector3 point in nav.vertices) bounds.Encapsulate(point);
            projection = new MinimapProjection(bounds);
            Color32[] pixels = Background();

            for (int index = 0; index < nav.indices.Length; index += 3)
            {
                Vector3 a = nav.vertices[nav.indices[index]];
                Vector3 b = nav.vertices[nav.indices[index + 1]];
                Vector3 c = nav.vertices[nav.indices[index + 2]];
                Vector2 pa = projection.Normalize(a) * Resolution;
                Vector2 pb = projection.Normalize(b) * Resolution;
                Vector2 pc = projection.Normalize(c) * Resolution;
                int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))), 0, Resolution - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))), 0, Resolution - 1);
                int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))), 0, Resolution - 1);
                int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))), 0, Resolution - 1);
                Color tint = Color.Lerp(Ground, Road, Mathf.InverseLerp(bounds.min.y, bounds.max.y + 0.1f, (a.y + b.y + c.y) / 3));

                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 sample = new Vector2(x + 0.5f, y + 0.5f);
                    float first = Cross(pb - pa, sample - pa);
                    float second = Cross(pc - pb, sample - pb);
                    float third = Cross(pa - pc, sample - pc);
                    if ((first >= 0 && second >= 0 && third >= 0) || (first <= 0 && second <= 0 && third <= 0))
                        pixels[y * Resolution + x] = tint;
                }
            }
            return Texture(pixels);
        }

        public static Texture2D Village(Scene scene, out MinimapProjection projection)
        {
            var colliders = new List<Collider>();
            var roads = new List<Bounds>();
            var grass = new List<Renderer>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                    if (collider.enabled && !collider.isTrigger) colliders.Add(collider);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.name.Contains("Dirt") || renderer.name.Contains("Road")) roads.Add(renderer.bounds);
                    else if (renderer.name.Contains("Ground_Flat") || renderer.name.Contains("Ground_Raised")) grass.Add(renderer);
                }
            }
            if (colliders.Count == 0)
            {
                projection = default;
                return null;
            }

            Bounds all = colliders[0].bounds;
            Bounds floor = all;
            float largest = 0;
            foreach (Collider collider in colliders)
            {
                Bounds bounds = collider.bounds;
                all.Encapsulate(bounds);
                float area = bounds.size.x * bounds.size.z;
                if (bounds.size.y <= 2 && area > largest) { floor = bounds; largest = area; }
            }
            if (largest == 0) floor = all;
            projection = new MinimapProjection(floor);
            Color32[] pixels = Background();
            var hits = new RaycastHit[32];
            Physics.SyncTransforms();

            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                Vector3 sample = projection.World((x + 0.5f) / Resolution, (y + 0.5f) / Resolution);
                sample.y = all.max.y + 5;
                int count = Physics.RaycastNonAlloc(sample, Vector3.down, hits, all.size.y + 10,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float highest = float.NegativeInfinity;
                for (int hit = 0; hit < count; hit++)
                    if (hits[hit].collider.gameObject.scene == scene) highest = Mathf.Max(highest, hits[hit].point.y);
                if (float.IsNegativeInfinity(highest)) continue;

                Color32 color = highest > floor.max.y + 1.4f ? Obstacle : Ground;
                if (highest <= floor.max.y + 1.4f)
                {
                    foreach (Bounds road in roads)
                        if (Contains(road, sample))
                        { color = Road; break; }
                    // 잔디는 충돌 없는 장식 메시이므로 실제 배치 경계를 별도로 투영합니다.
                    foreach (Renderer patch in grass)
                    {
                        Vector3 local = patch.transform.InverseTransformPoint(sample);
                        if (Contains(patch.localBounds, local)) { color = Ground; break; }
                    }
                }
                pixels[y * Resolution + x] = color;
            }
            return Texture(pixels);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool Contains(Bounds bounds, Vector3 point) => point.x >= bounds.min.x && point.x <= bounds.max.x &&
            point.z >= bounds.min.z && point.z <= bounds.max.z;

        private static Color32[] Background()
        {
            var pixels = new Color32[Resolution * Resolution];
            for (int index = 0; index < pixels.Length; index++) pixels[index] = Empty;
            return pixels;
        }

        private static Texture2D Texture(Color32[] pixels)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            { name = "Region minimap", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
