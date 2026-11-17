using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class MicroTerrainAuthoring
    {
        public const string Folder = "Assets/Nomad/Terrain/Micro";
        public const string CatalogPath = Folder + "/MicroCatalog.asset";

        public static void Create()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var variants = new List<WfcTileVariant>();
            for (int level = 0; level < 3; level++)
            {
                CreatePiece(MicroTerrainShape.Flat, level, variants);
                if (level > 0) CreatePiece(MicroTerrainShape.Cliff, level, variants);
                if (level == 2) continue;
                CreatePiece(MicroTerrainShape.Stair, level, variants);
                CreatePiece(MicroTerrainShape.ConvexCorner, level, variants);
                CreatePiece(MicroTerrainShape.ConcaveCorner, level, variants);
            }
            WfcTerrainCatalog catalog = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WfcTerrainCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.cellSize = MicroTerrainTile.Size;
            catalog.microTiles = true;
            catalog.variants = variants.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static void CreatePiece(MicroTerrainShape shape, int level, List<WfcTileVariant> variants)
        {
            var root = new GameObject($"Micro_{shape}_{level}");
            var profile = root.AddComponent<MicroTerrainTile>();
            profile.Configure(shape, level);
            var combine = new List<CombineInstance>();
            Material defaultMaterial = null;
            try
            {
                int rows = profile.IsStair ? 4 : 1;
                for (int z = 0; z < rows; z++)
                {
                    int x = 0;
                    while (x < rows)
                    {
                        float unit = 3f / rows;
                        float top = profile.HeightAt(-1.5f + (x + 0.5f) * unit, -1.5f + (z + 0.5f) * unit);
                        int end = x + 1;
                        while (end < rows && Mathf.Abs(profile.HeightAt(-1.5f + (end + 0.5f) * unit,
                            -1.5f + (z + 0.5f) * unit) - top) < 0.001f) end++;
                        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        box.name = $"Surface_{x}_{z}";
                        box.transform.SetParent(root.transform, false);
                        box.transform.localPosition = new Vector3(-1.5f + (x + end) * unit * 0.5f,
                            (top - 0.25f) * 0.5f, -1.5f + (z + 0.5f) * unit);
                        box.transform.localScale = new Vector3((end - x) * unit, top + 0.25f, unit);
                        MeshFilter filter = box.GetComponent<MeshFilter>();
                        defaultMaterial = box.GetComponent<Renderer>().sharedMaterial;
                        combine.Add(new CombineInstance { mesh = filter.sharedMesh, transform = box.transform.localToWorldMatrix });
                        Object.DestroyImmediate(box.GetComponent<Renderer>());
                        Object.DestroyImmediate(filter);
                        x = end;
                    }
                }
                var mesh = new Mesh { name = root.name };
                mesh.CombineMeshes(combine.ToArray(), true, true);
                string meshPath = $"{Folder}/{root.name}.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
                else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
                var visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(root.transform, false);
                visual.GetComponent<MeshFilter>().sharedMesh = mesh;
                Material material = MaterialFor(level, profile.IsStair, defaultMaterial);
                visual.GetComponent<Renderer>().sharedMaterial = material;
                var ports = new List<TerrainConnection>();
                for (int side = 0; side < 4; side++)
                {
                    Vector3 edge = WfcTileRules.Outward[side] * 1.5f;
                    var point = new GameObject("Port_" + side);
                    point.transform.SetParent(root.transform, false);
                    point.transform.localPosition = edge + Vector3.up * profile.ContinuousHeight(edge.x, edge.z);
                    point.transform.localRotation = Quaternion.LookRotation(WfcTileRules.Outward[side]);
                    ports.Add(point.AddComponent<TerrainConnection>());
                }
                root.AddComponent<TerrainPiece>().Configure(profile.IsStair ? TerrainPieceKind.Stairs : level == 0 ? TerrainPieceKind.Flat : TerrainPieceKind.Raised,
                    new Bounds(new Vector3(0f, (level + (profile.IsStair ? 1f : 0f) - 0.25f) * 0.5f, 0f),
                        new Vector3(3f, level + (profile.IsStair ? 1f : 0f) + 0.25f, 3f)), ports.ToArray());
                string path = $"{Folder}/{root.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                TerrainPiece prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<TerrainPiece>();
                for (int turn = 0; turn < (profile.IsStair ? 4 : 1); turn++) variants.Add(Variant(prefab, profile, turn));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static WfcTileVariant Variant(TerrainPiece prefab, MicroTerrainTile profile, int turn)
        {
            var tile = new WfcTileVariant { prefab = prefab, micro = true, cliff = profile.IsCliff,
                level = profile.Level, stairs = profile.IsStair, wide = true, turn = turn, weight = 1f };
            Quaternion inverse = Quaternion.Euler(0f, -90f * turn, 0f);
            float Height(Vector3 point) { Vector3 local = inverse * point; return profile.HeightAt(local.x, local.z); }
            Vector3[] corners = { new Vector3(-1.5f, 0f, -1.5f), new Vector3(1.5f, 0f, -1.5f),
                new Vector3(1.5f, 0f, 1.5f), new Vector3(-1.5f, 0f, 1.5f) };
            for (int index = 0; index < 4; index++)
            {
                Vector3 local = inverse * corners[index];
                tile.cornerLevels[index] = Mathf.RoundToInt(profile.ContinuousHeight(local.x, local.z));
            }
            for (int side = 0; side < 4; side++)
            {
                Vector3 outward = WfcTileRules.Outward[side];
                Vector3 tangent = side % 2 == 0 ? Vector3.right : Vector3.forward;
                for (int sample = 0; sample < 20; sample++)
                {
                    Vector3 edge = outward * 1.49f + tangent * (-1.425f + sample * 0.15f);
                    int height = Mathf.RoundToInt(Height(edge) * 4f);
                    tile.edgeHeights[side * 20 + sample] = height;
                    tile.edgeSlope[side * 20 + sample] = height - Mathf.RoundToInt(Height(edge - outward * 0.8f) * 4f);
                }
                for (int lane = 0; lane < 3; lane++)
                {
                    int slot = side * 3 + lane;
                    tile.surfaces[slot] = tile.edgeHeights[side * 20 + lane * 7];
                    bool constant = Enumerable.Range(0, 20).All(index => tile.edgeHeights[side * 20 + index] == tile.edgeHeights[side * 20]);
                    Vector3 localEdge = inverse * (outward * 1.5f);
                    tile.required[slot] = profile.IsStair && constant ? Mathf.RoundToInt(profile.ContinuousHeight(localEdge.x, localEdge.z) * 4f) : -99;
                }
            }
            return tile;
        }

        private static Material MaterialFor(int level, bool stairs, Material fallback)
        {
            string path = $"{Folder}/MicroLevel{level}{(stairs ? "Stairs" : "")}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(fallback); AssetDatabase.CreateAsset(material, path); }
            Color[] colors = { new Color(0.22f, 0.45f, 0.36f), new Color(0.62f, 0.46f, 0.26f), new Color(0.3f, 0.48f, 0.7f) };
            material.color = stairs ? Color.Lerp(colors[level], Color.white, 0.4f) : colors[level];
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
