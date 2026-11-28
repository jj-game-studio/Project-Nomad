using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed class BossChargeWarning : MonoBehaviour
    {
        public const float Width = 3.2f;
        public float Length { get; private set; }
        public System.Func<Vector3, float> GroundHeight { get; set; }
        private Mesh mesh;
        private LineRenderer outline;
        private Transform fill;

        public static BossChargeWarning Create(Transform parent)
        {
            var root = new GameObject("Rectangular charge tell");
            root.transform.SetParent(parent, false);
            var warning = root.AddComponent<BossChargeWarning>();
            warning.mesh = new Mesh { name = "Charge rectangle" };
            var surface = new GameObject("Charge fill", typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(root.transform, false);
            warning.fill = surface.transform;
            surface.GetComponent<MeshFilter>().sharedMesh = warning.mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = WeaponVfx.Plain;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(1, 0.2f, 0.02f, 0.25f));
            renderer.SetPropertyBlock(tint);

            warning.outline = root.AddComponent<LineRenderer>();
            warning.outline.useWorldSpace = false;
            warning.outline.loop = true;
            warning.outline.positionCount = 4;
            warning.outline.widthMultiplier = 0.09f;
            warning.outline.sharedMaterial = WeaponVfx.Plain;
            return warning;
        }

        public void Refresh(Vector3 point, Vector3 direction, float length, float progress, bool locked)
        {
            Length = length;
            transform.SetPositionAndRotation(point + Vector3.up * 0.1f, Quaternion.LookRotation(direction));
            const int rows = 24;
            var vertices = new Vector3[(rows + 1) * 2];
            var triangles = new int[rows * 6];
            for (int row = 0; row <= rows; row++)
            for (int side = 0; side < 2; side++)
            {
                Vector3 vertex = new(side == 0 ? -Width / 2 : Width / 2, 0, length * Mathf.Lerp(0.15f, 1, progress) * row / rows);
                if (GroundHeight != null) vertex.y = GroundHeight(transform.TransformPoint(vertex)) + 0.22f - transform.position.y;
                vertices[row * 2 + side] = vertex;
                if (side != 0 || row == rows) continue;
                int first = row * 6, index = row * 2;
                triangles[first] = index; triangles[first + 1] = index + 2; triangles[first + 2] = index + 1;
                triangles[first + 3] = index + 1; triangles[first + 4] = index + 2; triangles[first + 5] = index + 3;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            outline.positionCount = (rows + 1) * 2;
            for (int row = 0; row <= rows; row++)
            for (int side = 0; side < 2; side++)
            {
                Vector3 pointOnEdge = new(side == 0 ? -Width / 2 : Width / 2, 0, length * row / rows);
                if (GroundHeight != null) pointOnEdge.y = GroundHeight(transform.TransformPoint(pointOnEdge)) + 0.25f - transform.position.y;
                outline.SetPosition(side == 0 ? row : outline.positionCount - row - 1, pointOnEdge);
            }
            outline.startColor = outline.endColor = locked ? Color.red : new Color(1, 0.75f, 0.15f);
            fill.localScale = Vector3.one;
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
