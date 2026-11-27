using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossHazard
    {
        private Material jetMaterial;
        private readonly List<Mesh> jetMeshes = new();

        private void CreateContinuousJet()
        {
            jetMaterial = new Material(Resources.Load<Shader>("BossBreathJet"));
            // 흩어진 점 대신 세 겹의 흐르는 화염 면을 겹쳐 끊기지 않는 부채꼴을 만듭니다.
            const int rows = 24, cols = 24;
            for (int layer = 0; layer < 3; layer++)
            {
                var vertices = new Vector3[(rows + 1) * (cols + 1)];
                var uv = new Vector2[vertices.Length];
                var triangles = new int[rows * cols * 6];
                int triangle = 0;
                for (int row = 0; row <= rows; row++)
                for (int col = 0; col <= cols; col++)
                {
                    float along = row / (float)rows, across = col / (float)cols;
                    float radius = Mathf.Lerp(InnerRadius + 0.75f, Radius - 0.85f, along);
                    float radians = (across - 0.5f) * (angle - 12) * Mathf.Deg2Rad;
                    int index = row * (cols + 1) + col;
                    vertices[index] = new Vector3(Mathf.Sin(radians) * radius,
                        Mathf.Lerp(1.5f, 0.65f, along) + layer * 0.32f + Mathf.Sin(across * 17 + along * 21 + layer) * 0.18f,
                        Mathf.Cos(radians) * radius);
                    uv[index] = new Vector2(across, along);
                    if (row == rows || col == cols) continue;
                    triangles[triangle++] = index; triangles[triangle++] = index + cols + 1; triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + 1; triangles[triangle++] = index + cols + 1; triangles[triangle++] = index + cols + 2;
                }
                var mesh = new Mesh { name = "Continuous flame fan", vertices = vertices, uv = uv, triangles = triangles };
                mesh.RecalculateBounds(); jetMeshes.Add(mesh);
                var sheet = new GameObject("Coherent breath flame sheet");
                sheet.transform.SetParent(transform, false);
                sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = sheet.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = jetMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
