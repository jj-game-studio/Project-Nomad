using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    // 운석 본체와 체적 화염은 같은 재질을 쓰되, 생성한 메시의 수명은 각 효과가 소유합니다.
    public sealed class BossFlameVfx : MonoBehaviour
    {
        private static Material molten, flame;
        private readonly List<Mesh> meshes = new();

        public static Material Molten => molten != null ? molten : molten = new Material(Resources.Load<Shader>("BossMoltenRock"));
        public static Material Flame => flame != null ? flame : flame = new Material(Resources.Load<Shader>("BossVolumeFlame"));

        public static Transform Rock(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            float phi = (1 + Mathf.Sqrt(5)) / 2;
            Vector3[] points = { new(-1,phi,0),new(1,phi,0),new(-1,-phi,0),new(1,-phi,0),
                new(0,-1,phi),new(0,1,phi),new(0,-1,-phi),new(0,1,-phi),
                new(phi,0,-1),new(phi,0,1),new(-phi,0,-1),new(-phi,0,1) };

            int[] faces = { 0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,
                3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1 };
            var vertices = new Vector3[faces.Length]; var triangles = new int[faces.Length];
            for (int index = 0; index < faces.Length; index++)
            { vertices[index] = points[faces[index]].normalized * (0.48f + Mathf.Sin(faces[index] * 7.31f) * 0.055f); triangles[index] = index; }
            var mesh = new Mesh { name = "Fractured molten meteor", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); Attach(root, mesh, Molten);

            return root.transform;
        }

        public static Transform Jet(Transform parent, Vector3 start, Vector3 end, float baseRadius, float tipRadius)
        {
            var root = new GameObject("Volumetric flame jet");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = start;
            root.transform.localRotation = Quaternion.LookRotation(end - start);
            const int rings = 18, segments = 14;

            float length = Vector3.Distance(start, end);
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var uv = new Vector2[vertices.Length]; var triangles = new int[rings * segments * 6];
            int triangle = 0;
            for (int row = 0; row <= rings; row++)
            for (int col = 0; col <= segments; col++)
            {
                float t = row / (float)rings, turn = col / (float)segments;

                float angle = turn * Mathf.PI * 2;
                float radius = Mathf.Lerp(baseRadius, tipRadius, t) * (1 + Mathf.Sin(t * 24 + angle * 3) * 0.12f);
                int index = row * (segments + 1) + col;
                vertices[index] = new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, t * length);
                uv[index] = new Vector2(turn, t);
                if (row == rings || col == segments) continue;
                triangles[triangle++] = index; triangles[triangle++] = index + 1; triangles[triangle++] = index + segments + 1;

                triangles[triangle++] = index + 1; triangles[triangle++] = index + segments + 2; triangles[triangle++] = index + segments + 1;
            }
            var mesh = new Mesh { name = "Turbulent flame volume", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); Attach(root, mesh, Flame);
            return root.transform;
        }

        public static void Attach(GameObject root, Mesh mesh, Material material)
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            root.AddComponent<BossFlameVfx>().meshes.Add(mesh);
        }

        private void OnDestroy() { foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh); }
    }
}
