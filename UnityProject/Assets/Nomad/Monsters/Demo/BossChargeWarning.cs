using Nomad.Equipment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed class BossChargeWarning : MonoBehaviour
    {
        public const float Width = 3.2f;
        public float Length { get; private set; }
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
            Vector3[] corners = { new(-Width / 2, 0, 0), new(-Width / 2, 0, length),
                new(Width / 2, 0, length), new(Width / 2, 0, 0) };
            mesh.vertices = corners;
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            outline.SetPositions(corners);
            outline.startColor = outline.endColor = locked ? Color.red : new Color(1, 0.75f, 0.15f);
            fill.localScale = new Vector3(1, 1, Mathf.Lerp(0.15f, 1, progress));
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
