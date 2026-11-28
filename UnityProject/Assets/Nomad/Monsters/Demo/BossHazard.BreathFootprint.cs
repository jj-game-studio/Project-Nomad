using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossHazard
    {
        private bool innerBuilt;
        private LineRenderer innerBoundary;

        private void BuildBreathFootprint()
        {
            const int segments = 64, rings = 8;
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var triangles = new int[rings * segments * 6];
            for (int row = 0; row <= rings; row++)
            for (int col = 0; col <= segments; col++)
            {
                float radians = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, col / (float)segments) * Mathf.Deg2Rad;
                int vertex = row * (segments + 1) + col;
                vertices[vertex] = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)) * Mathf.Lerp(InnerRadius, Radius, row / (float)rings);
                if (row == rings || col == segments) continue;
                int triangle = (row * segments + col) * 6;
                int next = vertex + segments + 1;
                triangles[triangle] = vertex; triangles[triangle + 1] = next; triangles[triangle + 2] = next + 1;
                triangles[triangle + 3] = vertex; triangles[triangle + 4] = next + 1; triangles[triangle + 5] = vertex + 1;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            fullFootprint = vertices;
            projected = new Vector3[vertices.Length];
            boundaryStart = rings * (segments + 1);
            if (angle >= 359)
            {
                boundary.positionCount = segments;
                innerBoundary = new GameObject("Inner ring boundary").AddComponent<LineRenderer>();
                innerBoundary.transform.SetParent(transform, false);
                innerBoundary.useWorldSpace = false;
                innerBoundary.loop = true;
                innerBoundary.positionCount = segments;
                innerBoundary.sharedMaterial = boundary.sharedMaterial;
                innerBoundary.widthMultiplier = 0.07f;
                for (int index = 0; index < segments; index++)
                {
                    Vector3 local = vertices[index];
                    Vector3 world = transform.TransformPoint(local);
                    local.y = (GroundHeight != null ? GroundHeight(world) : transform.position.y) + 0.25f - transform.position.y;
                    innerBoundary.SetPosition(index, local);
                }
            }
            else boundary.positionCount = (segments + 1) * 2;
            for (int index = 0; index < boundary.positionCount; index++)
            {
                Vector3 local = index <= segments ? vertices[boundaryStart + index] : vertices[segments * 2 + 1 - index];
                Vector3 world = transform.TransformPoint(local);
                local.y = (GroundHeight != null ? GroundHeight(world) : transform.position.y) + 0.25f - transform.position.y;
                boundary.SetPosition(index, local);
            }
            boundaryProjected = true;
            innerBuilt = true;
        }
    }
}
