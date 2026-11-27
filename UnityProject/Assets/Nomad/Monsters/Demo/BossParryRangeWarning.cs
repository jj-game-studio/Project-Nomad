using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed class BossParryRangeWarning : MonoBehaviour
    {
        private const int Segments = 96, Rings = 8;
        private Mesh mesh;
        private Vector3[] vertices;
        private System.Func<Vector3, float> ground;
        private float radius, duration, age;
        private Transform player;
        private System.Func<Vector3, float> clip;
        private LineRenderer timing;
        public float Progress => Mathf.Clamp01(age / duration);

        public static BossParryRangeWarning Create(Transform parent, Vector3 center, float radius, float seconds, System.Func<Vector3, float> ground, Transform player, System.Func<Vector3, float> clip)
        {
            var root = new GameObject("Golden fireball parry range");
            root.transform.SetParent(parent, false);
            root.transform.position = center;
            var warning = root.AddComponent<BossParryRangeWarning>();
            warning.radius = radius;
            warning.duration = Mathf.Max(0.1f, seconds);
            warning.ground = ground;
            warning.player = player;
            warning.clip = clip;
            warning.Build();
            Destroy(root, seconds + 0.18f);
            return warning;
        }

        private void Build()
        {
            vertices = new Vector3[(Rings + 1) * (Segments + 1)];
            var triangles = new int[Rings * Segments * 6];
            for (int row = 0; row < Rings; row++)
            for (int col = 0; col < Segments; col++)
            {
                int a = row * (Segments + 1) + col, b = a + Segments + 1;
                int offset = (row * Segments + col) * 6;
                triangles[offset] = a; triangles[offset + 1] = b; triangles[offset + 2] = b + 1;
                triangles[offset + 3] = a; triangles[offset + 4] = b + 1; triangles[offset + 5] = a + 1;
            }
            mesh = new Mesh { name = "Golden parry timing fill", vertices = vertices, triangles = triangles };
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WeaponVfx.Plain;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(1, 0.82f, 0.04f, 0.2f));
            renderer.SetPropertyBlock(block);

            var boundary = new GameObject("Golden range boundary").AddComponent<LineRenderer>();
            boundary.transform.SetParent(transform, false);
            boundary.sharedMaterial = WeaponVfx.Plain;
            boundary.startColor = boundary.endColor = new Color(1, 0.84f, 0.12f, 0.9f);
            boundary.widthMultiplier = 0.13f;
            boundary.loop = true;
            boundary.useWorldSpace = true;
            boundary.positionCount = Segments;
            for (int index = 0; index < Segments; index++)
            {
                float radians = index * Mathf.PI * 2 / Segments;
                Vector3 heading = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
                Vector3 world = transform.position + heading * Mathf.Min(radius, clip(heading));
                world.y = ground(world) + 0.34f;
                boundary.SetPosition(index, world);
            }
            timing = new GameObject("Player parry timing arc").AddComponent<LineRenderer>();
            timing.transform.SetParent(transform, false);
            timing.useWorldSpace = true;
            timing.sharedMaterial = WeaponVfx.Plain;
            timing.widthMultiplier = 0.15f;
            timing.startColor = timing.endColor = new Color(1, 0.86f, 0.08f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float progress = Mathf.SmoothStep(0, 1, Progress);
            int points = Mathf.Max(2, Mathf.CeilToInt(Segments * Progress) + 1);
            timing.positionCount = points;
            Vector3 center = player != null ? player.position : transform.position;
            center.y = ground(center) + 0.36f;
            for (int index = 0; index < points; index++)
            {
                float theta = Mathf.PI * 2 * Progress * index / (points - 1);
                timing.SetPosition(index, center + new Vector3(Mathf.Sin(theta), 0, Mathf.Cos(theta)) * 1.1f);
            }
            for (int row = 0; row <= Rings; row++)
            for (int col = 0; col <= Segments; col++)
            {
                float radians = col * Mathf.PI * 2 / Segments;
                Vector3 heading = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
                Vector3 local = heading * (Mathf.Min(radius, clip(heading)) * progress * row / Rings);
                local.y = ground(transform.position + local) + 0.3f - transform.position.y;
                vertices[row * (Segments + 1) + col] = local;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
