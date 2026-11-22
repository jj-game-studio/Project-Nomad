using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class DragonLowPolyMesh
    {
        public static Mesh Create(Mesh original, string path)
        {
            // 스킨 메시의 정점/UV를 합치면 이음매와 관절이 손상되므로 원본 삼각형을 그대로 보존합니다.
            Vector3[] sourceVertices = original.vertices;
            Vector2[] sourceUv = original.uv;
            BoneWeight[] sourceWeights = original.boneWeights;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var submeshes = new List<int[]>();
            for (int sub = 0; sub < original.subMeshCount; sub++)
            {
                int[] indices = original.GetTriangles(sub);
                var output = new int[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    int source = indices[i];
                    output[i] = vertices.Count;
                    vertices.Add(sourceVertices[source]);
                    uv.Add(sourceUv[source]);
                    weights.Add(sourceWeights[source]);
                }
                submeshes.Add(output);
            }
            var mesh = new Mesh { name = original.name + " Stable Flat", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = original.bindposes;
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, saved);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(saved);
            }
            saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Debug.Log($"DRAGON_STABLE_FLAT triangles={original.triangles.Length / 3}->{saved.triangles.Length / 3}; exact vertex/UV/skin mapping");
            return saved;
        }
    }
}
