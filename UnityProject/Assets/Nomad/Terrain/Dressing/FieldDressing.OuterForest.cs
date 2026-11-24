using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    public sealed partial class FieldDressing
    {
        private readonly List<Object> outerAssets = new List<Object>();
        public int OuterTreeCount { get; private set; }

        private void PlaceOuterForest(FieldDressingSet source, int seed)
        {
            if (outerForestWidth <= 0f) return;

            var forest = new GameObject("Outer Forest");
            forest.transform.SetParent(root.transform, false);
            Vector3 origin = ground.transform.position;
            Vector3 size = ground.terrainData.size;
            float width = outerForestWidth;
            var area = new Rect(origin.x, origin.z, size.x, size.z);
            var random = new System.Random(unchecked(seed * 3571 + 203));
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.mainTexture = ground.terrainData.terrainLayers[0].diffuseTexture;
            material.SetFloat("_Smoothness", 0f);
            outerAssets.Add(material);

            // 내부 Terrain·충돌·NavMesh는 그대로 두고 바깥 네 면만 장식 메시로 이어 붙입니다.
            var strips = new[]
            {
                new Rect(area.xMin - width, area.yMin - width, area.width + width * 2, width),
                new Rect(area.xMin - width, area.yMax, area.width + width * 2, width),
                new Rect(area.xMin - width, area.yMin, width, area.height),
                new Rect(area.xMax, area.yMin, width, area.height),
            };
            foreach (Rect strip in strips) CreateOuterGround(forest.transform, strip, area, seed, material);

            // 별도 난수열과 충돌 없는 나무를 사용해 내부 소품 배치·보행 검사를 바꾸지 않습니다.
            const float spacing = 4.5f;
            for (float z = area.yMin - width + 2f; z < area.yMax + width - 1f; z += spacing)
            for (float x = area.xMin - width + 2f; x < area.xMax + width - 1f; x += spacing)
            {
                Vector3 position = new Vector3(x + Range(random, -1f, 1f), 0, z + Range(random, -1f, 1f));
                float distance = DistanceOutside(position, area);
                if (distance < 3f) continue;
                GameObject prefab = Pick(source.trees, random);
                if (prefab == null) continue;

                position.y = OuterHeight(position, area, seed);
                GameObject tree = Instantiate(prefab, position, Quaternion.Euler(0, Range(random, 0, 360), 0), forest.transform);
                tree.transform.localScale = prefab.transform.localScale * Range(random, 0.9f, 1.25f);
                foreach (Collider collider in tree.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
                OuterTreeCount++;
            }
        }

        private void CreateOuterGround(Transform parent, Rect strip, Rect area, int seed, Material material)
        {
            int columns = Mathf.CeilToInt(strip.width / 0.75f);
            int rows = Mathf.CeilToInt(strip.height / 0.75f);
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            int triangle = 0;

            for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= columns; column++)
            {
                int index = row * (columns + 1) + column;
                Vector3 point = new Vector3(strip.xMin + strip.width * column / columns, 0, strip.yMin + strip.height * row / rows);
                point.y = OuterHeight(point, area, seed);
                vertices[index] = parent.InverseTransformPoint(point);
                uv[index] = new Vector2(point.x, point.z) / 6f;
                if (column == columns || row == rows) continue;

                int next = index + columns + 1;
                triangles[triangle++] = index;
                triangles[triangle++] = next;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = next;
                triangles[triangle++] = next + 1;
            }

            var mesh = new Mesh { name = "Outer Forest Ground", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            outerAssets.Add(mesh);
            var patch = new GameObject("Forest Ground");
            patch.transform.SetParent(parent, false);
            patch.AddComponent<MeshFilter>().sharedMesh = mesh;
            patch.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private float OuterHeight(Vector3 point, Rect area, int seed)
        {
            Vector3 edge = new Vector3(Mathf.Clamp(point.x, area.xMin, area.xMax), 0, Mathf.Clamp(point.z, area.yMin, area.yMax));
            float height = GroundY(edge, 0);
            float blend = Mathf.SmoothStep(0, 1, DistanceOutside(point, area) / 8f);
            float noise = Mathf.PerlinNoise(point.x * 0.07f + seed % 997, point.z * 0.07f + seed % 991);
            return height + blend * noise * 1.5f;
        }

        private static float DistanceOutside(Vector3 point, Rect area) => Mathf.Max(
            Mathf.Max(area.xMin - point.x, point.x - area.xMax), Mathf.Max(area.yMin - point.z, point.z - area.yMax));

        private void ClearOuterForest()
        {
            OuterTreeCount = 0;
            foreach (Object asset in outerAssets)
            {
                if (asset == null) continue;
                if (Application.isPlaying) Destroy(asset);
                else DestroyImmediate(asset);
            }
            outerAssets.Clear();
        }
    }
}
