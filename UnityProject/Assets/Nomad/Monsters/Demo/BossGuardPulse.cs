using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed class BossGuardPulse : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private Transform boss;
        private float age;

        public static void Create(Transform boss)
        {
            var root = new GameObject("Boss golden just guard pulse");
            var pulse = root.AddComponent<BossGuardPulse>();
            pulse.boss = boss;
            root.transform.position = boss.position + Vector3.up * 0.18f;
            root.transform.localScale = Vector3.one * 2.5f;
            pulse.Build();
            Destroy(root, 0.48f);
        }

        private void Build()
        {
            const int segments = 64, rings = 12;
            var vertices = new Vector3[(segments + 1) * (rings + 1)];
            var triangles = new int[segments * rings * 6];
            for (int row = 0; row <= rings; row++)
            for (int col = 0; col <= segments; col++)
            {
                float elevation = row * Mathf.PI * 0.5f / rings;
                float radians = col * Mathf.PI * 2 / segments;
                vertices[row * (segments + 1) + col] = new Vector3(Mathf.Cos(elevation) * Mathf.Sin(radians), Mathf.Sin(elevation) * 0.8f, Mathf.Cos(elevation) * Mathf.Cos(radians));
                if (row == rings || col == segments) continue;
                int a = row * (segments + 1) + col, b = a + segments + 1;
                int index = (row * segments + col) * 6;
                triangles[index] = a; triangles[index + 1] = b; triangles[index + 2] = a + 1;
                triangles[index + 3] = a + 1; triangles[index + 4] = b; triangles[index + 5] = b + 1;
            }
            mesh = new Mesh { name = "Golden guard hemisphere", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Resources.Load<Shader>("BossGuardPulse"));
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void LateUpdate()
        {
            if (boss == null) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            transform.position = boss.position + Vector3.up * 0.18f;
            transform.localScale = Vector3.one * Mathf.Lerp(2.5f, 4.6f, Mathf.Clamp01(age / 0.48f));
            material.SetFloat("_Age", age / 0.48f);
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
    }
}
