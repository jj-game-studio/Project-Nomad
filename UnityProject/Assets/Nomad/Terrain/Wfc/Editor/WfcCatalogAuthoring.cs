using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class WfcCatalogAuthoring
    {
        public const string Folder = "Assets/Nomad/Terrain/Wfc";
        public const string CatalogPath = Folder + "/TerrainCatalog.asset";

        public static void Create()
        {
            CreateFloor("WfcLow", 0f);
            CreateFloor("WfcHigh", 2f);
            FullWidthStairAuthoring.Create();
            var variants = new List<WfcTileVariant>();
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string name in new[] { "WfcLow", "WfcHigh", "ModularCross", "ModularTee", "WideStairs" }.Concat(FullWidthStairAuthoring.Names))
                {
                    string path = (name.StartsWith("Wfc", StringComparison.Ordinal) || FullWidthStairAuthoring.Names.Contains(name)) ? $"{Folder}/{name}.prefab"
                        : $"{ModularTileAuthoring.Output}/{name}.prefab";
                    TerrainPiece prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<TerrainPiece>();
                    if (prefab == null) throw new InvalidOperationException("원형이 없습니다: " + path);
                    TerrainPiece sample = Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(sample.gameObject, scene);
                    ModularTerrainTile modular = sample.GetComponent<ModularTerrainTile>();
                    int count = modular == null ? 1 : modular.CombinationCount;
                    for (int combination = 0; combination < count; combination++)
                    {
                        modular?.ApplyCombination(combination);
                        Physics.SyncTransforms();
                        TerrainIntegrationValidation.ValidateSingleSurface(sample);
                        for (int turn = 0; turn < 4; turn++)
                        {
                            sample.transform.rotation = Quaternion.Euler(0f, turn * 90f, 0f);
                            Physics.SyncTransforms();
                            var variant = new WfcTileVariant
                            {
                                prefab = prefab, combination = combination, turn = turn,
                                stairs = prefab.Kind == TerrainPieceKind.Stairs, wide = name == "WideStairs" || sample.GetComponent<FullWidthStairTile>() != null,
                                weight = name.StartsWith("Wfc", StringComparison.Ordinal) ? 1f : sample.GetComponent<ModularTerrainTile>() == null ? 0.35f : 0.5f / count
                            };
                            Bake(sample, scene.GetPhysicsScene(), variant);
                            variants.Add(variant);
                        }
                    }
                    Object.DestroyImmediate(sample.gameObject);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            WfcTerrainCatalog catalog = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WfcTerrainCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.variants = variants.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static void Bake(TerrainPiece piece, PhysicsScene physics, WfcTileVariant variant)
        {
            for (int slot = 0; slot < 12; slot++)
            {
                variant.surfaces[slot] = -99;
                variant.required[slot] = -99;
                int side = slot / 3;
                Vector3 outward = WfcTileRules.Outward[side];
                Vector3 center = WfcTileRules.EdgePoint(side, slot % 3) - outward * 0.05f;
                Vector3 tangent = side % 2 == 0 ? Vector3.right : Vector3.forward;
                var heights = new List<float>();
                foreach (float offset in new[] { -0.9f, 0f, 0.9f })
                {
                    if (physics.Raycast(center + tangent * offset + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 5f))
                        heights.Add(hit.point.y);
                }
                if (heights.Count == 3 && heights.Max() - heights.Min() <= 0.251f)
                    variant.surfaces[slot] = Mathf.RoundToInt(heights.Min() * 4f);
            }

            BakeEdgesAndVoids(physics, variant);

            if (!variant.stairs) return;
            foreach (TerrainConnection port in piece.Connections)
            {
                // 고정 출입구 지지 외에도 위에서 전체 변 높이와 빈 영역의 외곽 연결을 검사합니다.
                if (port.transform.parent != piece.transform) continue;
                int slot = WfcTileRules.Slot(port.Position, port.Outward);
                variant.required[slot] = Mathf.RoundToInt(port.Position.y * 4f);
            }
        }

        private static void BakeEdgesAndVoids(PhysicsScene physics, WfcTileVariant variant)
        {
            for (int side = 0; side < 4; side++)
            for (int sample = 0; sample < 20; sample++)
            {
                float coordinate = -4.75f + sample * 0.5f;
                Vector3 point = side % 2 == 0 ? new Vector3(coordinate, 4f, side == 0 ? 4.95f : -4.95f)
                    : new Vector3(side == 1 ? 4.95f : -4.95f, 4f, coordinate);
                variant.edgeHeights[side * 20 + sample] = physics.Raycast(point, Vector3.down, out RaycastHit hit, 5f)
                    ? Mathf.RoundToInt(hit.point.y * 4f) : -99;
            }

            var empty = new bool[400];
            for (int z = 0; z < 20; z++)
            for (int x = 0; x < 20; x++)
                empty[z * 20 + x] = !physics.Raycast(new Vector3(-4.75f + x * 0.5f, 4f, -4.75f + z * 0.5f), Vector3.down, out _, 5f);
            var regions = new List<int>();
            for (int cell = 0; cell < empty.Length; cell++)
            {
                if (!empty[cell]) continue;
                var queue = new Queue<int>();
                queue.Enqueue(cell);
                empty[cell] = false;
                int boundary = 0;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    for (int side = 0; side < 4; side++)
                    {
                        int neighbour = WfcTerrainSolver.Neighbour(current, side, 20, 20);
                        if (neighbour < 0) { boundary |= 1 << side; continue; }
                        if (!empty[neighbour]) continue;
                        empty[neighbour] = false;
                        queue.Enqueue(neighbour);
                    }
                }
                regions.Add(boundary);
            }
            variant.voidBoundaries = regions.ToArray();
        }
        private static void CreateFloor(string name, float height)
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var root = new GameObject(name);
            try
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(root.transform, false);
                floor.transform.localPosition = new Vector3(0f, height - 0.125f, 0f);
                floor.transform.localScale = new Vector3(10f, 0.25f, 10f);
                string materialPath = $"{Folder}/{name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.SetColor("_BaseColor", height == 0f ? new Color(0.22f, 0.4f, 0.36f) : new Color(0.6f, 0.48f, 0.3f));
                EditorUtility.SetDirty(material);
                floor.GetComponent<Renderer>().sharedMaterial = material;
                var ports = new List<TerrainConnection>();
                for (int side = 0; side < 4; side++)
                for (int lane = 0; lane < 3; lane++)
                {
                    if (lane != 1) continue;
                    var port = new GameObject($"Port_{side}_{lane}");
                    port.transform.SetParent(root.transform, false);
                    port.transform.localPosition = WfcTileRules.EdgePoint(side, lane) + Vector3.up * height;
                    port.transform.localRotation = Quaternion.LookRotation(WfcTileRules.Outward[side]);
                    ports.Add(port.AddComponent<TerrainConnection>());
                }
                root.AddComponent<TerrainPiece>().Configure(height == 0f ? TerrainPieceKind.Flat : TerrainPieceKind.Raised,
                    new Bounds(new Vector3(0f, height - 0.125f, 0f), new Vector3(10f, 0.25f, 10f)), ports.ToArray());
                PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
