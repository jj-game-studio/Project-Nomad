using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossRuinsDressing
    {
        private readonly List<Mesh> masonryMeshes = new();

        private void BuildMasonry(Quaternion facing)
        {
            // 개별 석판은 하나의 메시로 묶고 틈·모서리·높낮이를 바닥 표면에 맞춥니다.
            var vertices = new List<Vector3>();
            var colors = new[] { Material(new Color(0.29f, 0.265f, 0.29f)),
                Material(new Color(0.245f, 0.225f, 0.25f)), Material(new Color(0.34f, 0.305f, 0.31f)) };
            var groups = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int row = 0; row < 9; row++)
            {
                float inner = 0.25f + row * 2.35f, outer = inner + 2.25f;
                int count = Mathf.Max(8, Mathf.RoundToInt((inner + outer) * Mathf.PI / 3.1f));
                for (int col = 0; col < count; col++)
                {
                    int first = vertices.Count;
                    float a = (col + (row % 2) * 0.5f) * Mathf.PI * 2 / count + 0.012f + Mathf.Sin(col * 13 + row * 7) * 0.008f;
                    float b = a + Mathf.PI * 2 / count - 0.024f;
                    for (int level = 0; level < 2; level++)
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float radius = corner < 2 ? inner : outer;
                        float theta = corner % 2 == 0 ? a : b;
                        if (level == 1) radius += corner < 2 ? 0.05f : -0.05f;
                        Vector3 world = arena.GroundPoint(arena.Center + new Vector3(Mathf.Sin(theta), 0, Mathf.Cos(theta)) * radius);
                        world.y += level == 0 ? 0.015f : 0.075f;
                        vertices.Add(transform.InverseTransformPoint(world));
                    }
                    int[] indices = { 4,6,5, 6,7,5, 0,1,4, 4,1,5, 2,6,3, 3,6,7, 0,4,2, 2,4,6, 1,3,5, 5,3,7 };
                    foreach (int index in indices) groups[(row * 7 + col * 13) % 3].Add(first + index);
                }
            }
            var mesh = new Mesh { name = "Beveled radial basalt paving" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 3;
            for (int index = 0; index < 3; index++) mesh.SetTriangles(groups[index], index);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            masonryMeshes.Add(mesh);
            var paving = new GameObject("Hand laid volcanic stone paving");
            paving.transform.SetParent(transform, false);
            paving.AddComponent<MeshFilter>().sharedMesh = mesh;
            paving.AddComponent<MeshRenderer>().sharedMaterials = colors;

            for (int index = 0; index < 12; index++)
            {
                Quaternion turn = facing * Quaternion.Euler(0, index * 30 + 15, 0);
                Vector3 position = arena.GroundPoint(arena.Center + turn * Vector3.forward * 23.2f);
                Ornament("BossRuinedPillar", position, turn, 4.5f + index % 3);
                for (int step = 0; step < 3; step++)
                    Primitive("Stepped basalt buttress", PrimitiveType.Cube, position + Vector3.up * (0.1f + step * 0.3f),
                        new Vector3(2.5f - step * 0.4f, 0.3f, 2.2f - step * 0.35f), turn, step % 2 == 0 ? stone : edge);
            }
            Ornament("BossRuinedArch", arena.Center + arena.Forward * 25, facing, 7.5f);
            for (int index = 0; index < 18; index++)
            {
                Quaternion turn = facing * Quaternion.Euler(0, index * 20 + 8, 0);
                Vector3 location = arena.Center + turn * Vector3.forward * (28 + index % 3 * 4);
                Cliff(location - Vector3.up * 3, new Vector3(7, 7 + index % 4 * 2, 6), turn, stone);
            }
        }

        private void Ornament(string resource, Vector3 position, Quaternion rotation, float height)
        {
            GameObject prefab = Resources.Load<GameObject>(resource);
            if (prefab == null) return;
            GameObject root = Instantiate(prefab, transform);
            root.name = resource;
            root.transform.SetPositionAndRotation(position, rotation);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            root.transform.localScale *= height / Mathf.Max(0.1f, bounds.size.y);
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            root.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            root.AddComponent<CartoonSurfaceStyle>();
        }
    }
}
