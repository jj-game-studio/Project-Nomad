using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossArena
    {
        private Mesh groundMesh;
        private Texture2D groundTexture;
        private Vector3 floorOrigin;
        private bool uneven;

        public float GroundHeight(Vector3 point)
        {
            if (!uneven) return floorOrigin.y;
            Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(Forward)) * (point - floorOrigin);
            if (Mathf.Abs(local.x) > HalfSize || Mathf.Abs(local.z) > HalfSize) return floorOrigin.y;

            float center = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6, 11, new Vector2(local.x, local.z).magnitude));
            float side = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3, 6, Mathf.Abs(local.x)));
            float approach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-HalfSize + 7, -HalfSize + 12, local.z));
            float relief = 0.36f + Mathf.Sin(local.x * 0.22f) * Mathf.Cos(local.z * 0.2f) * 0.22f + Mathf.Sin((local.x + local.z) * 0.31f) * 0.12f;
            return floorOrigin.y + relief * center * Mathf.Max(side, approach);
        }

        public Vector3 GroundPoint(Vector3 point) => new(point.x, GroundHeight(point), point.z);

        private void BuildGround(Material stone)
        {
            floorOrigin = Center;
            uneven = FindFirstObjectByType<BossEncounterContent>()?.UnevenFloor ?? true;
            const int cells = 22;
            var vertices = new Vector3[(cells + 1) * (cells + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[cells * cells * 6];
            Quaternion facing = Quaternion.LookRotation(Forward);

            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                int index = z * (cells + 1) + x;
                Vector3 local = new(Mathf.Lerp(-HalfSize, HalfSize, x / (float)cells), 0, Mathf.Lerp(-HalfSize, HalfSize, z / (float)cells));
                float nx = local.x / HalfSize, nz = local.z / HalfSize;
                local.x *= Mathf.Sqrt(1 - nz * nz * 0.5f);
                local.z *= Mathf.Sqrt(1 - nx * nx * 0.5f);
                local.y = GroundHeight(floorOrigin + facing * local) - floorOrigin.y;
                vertices[index] = local;
                uv[index] = new Vector2(x / (float)cells, z / (float)cells);
                if (x == cells || z == cells) continue;
                int first = (z * cells + x) * 6;
                int right = index + 1, above = index + cells + 1;
                triangles[first] = index; triangles[first + 1] = above; triangles[first + 2] = right;
                triangles[first + 3] = right; triangles[first + 4] = above; triangles[first + 5] = above + 1;
            }

            groundMesh = new Mesh { name = "Walkable boss stone relief", vertices = vertices, uv = uv, triangles = triangles };
            groundMesh.RecalculateNormals();
            groundMesh.RecalculateBounds();
            var root = new GameObject("Uneven arena ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(floorOrigin, facing);
            root.GetComponent<MeshFilter>().sharedMesh = groundMesh;
            root.GetComponent<MeshCollider>().sharedMesh = groundMesh;

            groundTexture = new Texture2D(128, 128, TextureFormat.RGB24, false);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float value = 0.76f + Mathf.PerlinNoise(x * 0.17f, y * 0.17f) * 0.22f;
                if (x % 12 == 0 || y % 12 == 0) value *= 0.9f;
                groundTexture.SetPixel(x, y, new Color(value, value, value));
            }
            groundTexture.Apply();

            stone.SetTexture("_BaseMap", groundTexture);
            stone.SetFloat("_Smoothness", 0.05f);
            root.GetComponent<MeshRenderer>().sharedMaterial = stone;
        }

        private NavMeshBuildSource GroundSource() => new() { shape = NavMeshBuildSourceShape.Mesh, sourceObject = groundMesh,
            transform = Matrix4x4.TRS(floorOrigin, Quaternion.LookRotation(Forward), Vector3.one) };

        public float DistanceToBoundary(Vector3 point, Vector3 direction)
        {
            Vector3 local = Vector3.ProjectOnPlane(point - Center, Vector3.up);
            Vector3 heading = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            float b = Vector3.Dot(local, heading);
            return Mathf.Max(0.5f, -b + Mathf.Sqrt(Mathf.Max(0, b * b + (HalfSize - 1) * (HalfSize - 1) - local.sqrMagnitude)));
        }
    }
}
