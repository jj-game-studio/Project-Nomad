using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Gameplay.Editor
{
    public static class DragonLowPolyMesh
    {
        public static Texture2D Palette(Texture source, string path)
        {
            var rt = RenderTexture.GetTemporary(128, 128, 0);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var texture = new Texture2D(128, 128, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(Mathf.Round(pixels[i].r * 5f) / 5f,
                    Mathf.Round(pixels[i].g * 5f) / 5f, Mathf.Round(pixels[i].b * 5f) / 5f);
            texture.SetPixels(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private struct Cluster
        {
            public Vector3 sum;
            public int count;
            public Vector2 uv;
            public BoneWeight weights;
        }

        public static Mesh Create(Mesh original, string path)
        {
            Vector3[] vertices = original.vertices;
            Vector2[] uv = original.uv;
            BoneWeight[] weights = original.boneWeights;
            Mesh reduced = null;
            int before = original.triangles.Length / 3;
            for (int resolution = 32; resolution >= 10; resolution -= 2)
            {
                if (reduced != null) Object.DestroyImmediate(reduced);
                float cell = Mathf.Max(original.bounds.size.x, original.bounds.size.y, original.bounds.size.z) / resolution;
                var map = new Dictionary<Vector4IntKey, int>();
                var clusters = new List<Cluster>();
                var remap = new int[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var key = new Vector4IntKey(Mathf.RoundToInt(vertices[i].x / cell),
                        Mathf.RoundToInt(vertices[i].y / cell), Mathf.RoundToInt(vertices[i].z / cell), weights[i].boneIndex0);
                    if (!map.TryGetValue(key, out int index))
                    {
                        index = clusters.Count;
                        map.Add(key, index);
                        clusters.Add(new Cluster { weights = weights[i] });
                    }
                    Cluster cluster = clusters[index];
                    cluster.sum += vertices[i];
                    cluster.uv += uv[i];
                    cluster.count++;
                    clusters[index] = cluster;
                    remap[i] = index;
                }
                var flatVertices = new List<Vector3>();
                var flatUv = new List<Vector2>();
                var flatWeights = new List<BoneWeight>();
                var submeshes = new List<int[]>();
                for (int sub = 0; sub < original.subMeshCount; sub++)
                {
                    int[] triangles = original.GetTriangles(sub);
                    var indices = new List<int>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = remap[triangles[i]], b = remap[triangles[i + 1]], c = remap[triangles[i + 2]];
                        if (a == b || b == c || c == a) continue;
                        foreach (int index in new[] { a, b, c })
                        {
                            Cluster cluster = clusters[index];
                            indices.Add(flatVertices.Count);
                            flatVertices.Add(cluster.sum / cluster.count);
                            flatUv.Add(cluster.uv / cluster.count);
                            flatWeights.Add(cluster.weights);
                        }
                    }
                    submeshes.Add(indices.ToArray());
                }
                reduced = new Mesh { name = original.name + " LowPoly", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                reduced.SetVertices(flatVertices);
                reduced.SetUVs(0, flatUv);
                reduced.boneWeights = flatWeights.ToArray();
                reduced.bindposes = original.bindposes;
                reduced.subMeshCount = submeshes.Count;
                for (int i = 0; i < submeshes.Count; i++) reduced.SetTriangles(submeshes[i], i);
                // 삼각형마다 정점을 분리해 평평한 면을 유지합니다. 감소 지표는 정점 수가 아닌 삼각형 수입니다.
                reduced.RecalculateNormals();
                reduced.RecalculateBounds();
                if (reduced.triangles.Length / 3 <= before * 0.65f) break;
            }
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) AssetDatabase.CreateAsset(reduced, path);
            else
            {
                EditorUtility.CopySerialized(reduced, saved);
                Object.DestroyImmediate(reduced);
                EditorUtility.SetDirty(saved);
            }
            saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Debug.Log($"DRAGON_LOW_POLY triangles={before}->{saved.triangles.Length / 3} bones={saved.bindposes.Length}");
            return saved;
        }

        private readonly struct Vector4IntKey
        {
            private readonly int x, y, z, bone;
            public Vector4IntKey(int x, int y, int z, int bone) { this.x = x; this.y = y; this.z = z; this.bone = bone; }
            public override bool Equals(object value) => value is Vector4IntKey other && x == other.x && y == other.y && z == other.z && bone == other.bone;
            public override int GetHashCode() => unchecked(((x * 397 ^ y) * 397 ^ z) * 397 ^ bone);
        }
    }
}
