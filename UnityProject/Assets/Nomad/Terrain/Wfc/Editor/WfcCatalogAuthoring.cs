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
            var variants = new List<WfcTileVariant>();
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string name in new[] { "WfcLow", "WfcHigh", "ModularCross", "ModularTee", "WideStairs" })
                {
                    string path = name.StartsWith("Wfc", StringComparison.Ordinal) ? $"{Folder}/{name}.prefab"
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
                                stairs = prefab.Kind == TerrainPieceKind.Stairs, wide = name == "WideStairs",
                                weight = name.StartsWith("Wfc", StringComparison.Ordinal) ? 1f : name == "WideStairs" ? 0.35f : 0.5f / count
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

            if (!variant.stairs) return;
            foreach (TerrainConnection port in piece.Connections)
            {
                // 모듈 바닥의 선택 포트는 절벽을 허용합니다. 계단 팔/넓은 계단의 고정 포트만 필수입니다.
                if (port.transform.parent != piece.transform) continue;
                int slot = WfcTileRules.Slot(port.Position, port.Outward);
                variant.required[slot] = Mathf.RoundToInt(port.Position.y * 4f);
            }
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
