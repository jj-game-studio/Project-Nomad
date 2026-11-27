using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed class BossParryRangeWarning : MonoBehaviour
    {
        private const int Steps = 20;
        private Mesh mesh;
        private Vector3[] vertices;
        private BossArena arena;
        private Vector3[] headings;
        private float[] lengths;
        private float duration, age, halfWidth;
        public float Progress => Mathf.Clamp01(age / duration);
        public int LaneCount => headings.Length;

        public static BossParryRangeWarning Create(Transform parent, BossArena arena, Vector3 center, Vector3 forward,
            int count, float radius, float range, float fillSeconds, float offset)
        {
            var root = new GameObject("Golden fireball parry lanes");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(center, Quaternion.identity);
            var warning = root.AddComponent<BossParryRangeWarning>();
            warning.arena = arena;
            warning.duration = Mathf.Max(0.1f, fillSeconds);
            warning.halfWidth = radius;
            warning.headings = new Vector3[count];
            warning.lengths = new float[count];
            for (int index = 0; index < count; index++)
            {
                warning.headings[index] = Quaternion.Euler(0, index * 360f / count + offset, 0) * forward;
                warning.lengths[index] = Mathf.Max(0, Mathf.Min(range + radius, arena.DistanceToBoundary(center, warning.headings[index]) - radius * 0.5f));
            }
            warning.Build();
            Destroy(root, fillSeconds + BossParrySignal.ReactionSeconds + 0.18f);
            return warning;
        }

        private Vector3 Ground(Vector3 point) => arena.GroundPoint(point) + Vector3.up * 0.32f;

        private void Build()
        {
            vertices = new Vector3[headings.Length * (Steps + 1) * 2];
            var triangles = new int[headings.Length * Steps * 6];
            for (int lane = 0; lane < headings.Length; lane++)
            {
                for (int step = 0; step < Steps; step++)
                {
                    int a = (lane * (Steps + 1) + step) * 2;
                    int triangle = (lane * Steps + step) * 6;
                    triangles[triangle] = a; triangles[triangle + 1] = a + 2; triangles[triangle + 2] = a + 1;
                    triangles[triangle + 3] = a + 1; triangles[triangle + 4] = a + 2; triangles[triangle + 5] = a + 3;
                }
                var line = new GameObject("Straight projectile boundary").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = WeaponVfx.Plain;
                line.useWorldSpace = true;
                line.loop = true;
                line.widthMultiplier = 0.055f;
                line.startColor = line.endColor = new Color(1, 0.83f, 0.1f, 0.7f);
                Vector3 side = Vector3.Cross(Vector3.up, headings[lane]) * halfWidth;
                Vector3 start = transform.position + headings[lane] * Mathf.Max(0, 3 - halfWidth);
                Vector3 end = transform.position + headings[lane] * lengths[lane];
                line.positionCount = 4;
                line.SetPositions(new[] { Ground(start - side), Ground(end - side), Ground(end + side), Ground(start + side) });
            }
            mesh = new Mesh { name = "Actual fireball lane fill", vertices = vertices, triangles = triangles };
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WeaponVfx.Plain;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(1, 0.8f, 0.04f, 0.28f));
            renderer.SetPropertyBlock(tint);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float fill = Mathf.SmoothStep(0, 1, Progress);
            for (int lane = 0; lane < headings.Length; lane++)
            {
                float start = Mathf.Min(lengths[lane], Mathf.Max(0, 3 - halfWidth));
                Vector3 side = Vector3.Cross(Vector3.up, headings[lane]) * halfWidth;
                for (int step = 0; step <= Steps; step++)
                {
                    Vector3 center = transform.position + headings[lane] * Mathf.Lerp(start, lengths[lane], fill * step / Steps);
                    int vertex = (lane * (Steps + 1) + step) * 2;
                    vertices[vertex] = Ground(center - side) - transform.position;
                    vertices[vertex + 1] = Ground(center + side) - transform.position;
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
