using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class KingdomTerrainAuthoring
    {
        public const string OutputFolder = "Assets/ThirdParty/Nomad/Terrain/Kingdom";
        public const string LibraryPath = OutputFolder + "/Resources/Nomad/KingdomTerrainVisuals.asset";
        private const string SourceFolder = "Assets/ThirdParty/Synty_Kingdom/PolygonFantasyKingdom/Prefabs/Environments/";

        [MenuItem("Nomad/Terrain/Kingdom/Rebuild Visuals")]
        public static void Create()
        {
            Directory.CreateDirectory(OutputFolder + "/Resources/Nomad");
            AssetDatabase.Refresh();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFolder + "SM_Env_Ground_Flat_01.prefab");
            if (source == null) throw new InvalidOperationException("PolygonFantasyKingdom 지형 원본이 필요합니다.");

            Mesh sourceMesh = source.GetComponent<MeshFilter>().sharedMesh;
            Material sourceMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(OutputFolder + "/KingdomGround.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, OutputFolder + "/KingdomGround.mat");
            }

            // 원본 재질은 수정하지 않고 Kingdom 텍스처를 URP 재질에 연결합니다.
            Texture texture = sourceMaterial.HasProperty("_Albedo_Map")
                ? sourceMaterial.GetTexture("_Albedo_Map") : sourceMaterial.mainTexture;
            if (texture == null) throw new InvalidOperationException("Kingdom 지형 텍스처가 누락되었습니다.");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);

            Material sides = AssetDatabase.LoadAssetAtPath<Material>(OutputFolder + "/KingdomSides.mat");
            if (sides == null)
            {
                sides = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(sides, OutputFolder + "/KingdomSides.mat");
            }
            sides.SetColor("_BaseColor", new Color(0.31f, 0.26f, 0.19f));
            sides.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(sides);

            WfcTerrainCatalog catalog = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(MicroTerrainAuthoring.CatalogPath);
            var entries = new List<KingdomTerrainVisuals.Entry>();
            foreach (TerrainPiece piece in catalog.variants.Select(v => v.prefab).Distinct())
            {
                MicroTerrainTile profile = piece.GetComponent<MicroTerrainTile>();
                Mesh mesh = BuildMesh(sourceMesh, piece);
                string path = OutputFolder + "/" + piece.name + ".asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    Object.DestroyImmediate(mesh);
                    mesh = existing;
                }

                entries.Add(new KingdomTerrainVisuals.Entry { shape = profile.Shape, level = profile.Level, mesh = mesh });
            }

            KingdomTerrainVisuals library = AssetDatabase.LoadAssetAtPath<KingdomTerrainVisuals>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<KingdomTerrainVisuals>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.material = material;
            library.sides = sides;
            library.entries = entries.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"KINGDOM_VISUALS_CREATED entries={entries.Count} source={source.name}");
        }

        private static Mesh BuildMesh(Mesh source, TerrainPiece piece)
        {
            var parts = new List<CombineInstance>();
            var temporary = new List<Mesh>();
            try
            {
                foreach (BoxCollider box in piece.GetComponentsInChildren<BoxCollider>())
                {
                    Mesh mesh = Object.Instantiate(source);
                    temporary.Add(mesh);
                    Vector3[] vertices = mesh.vertices;
                    Bounds bounds = source.bounds;
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        Vector3 point = vertices[index];
                        // 원본 외곽의 요철을 규격 경계에 맞추고 기존 충돌면 높이를 유지합니다.
                        point.x = Mathf.Clamp((point.x - bounds.center.x) / (bounds.extents.x * 0.8f), -1f, 1f) * box.size.x * 0.5f;
                        point.z = Mathf.Clamp((point.z - bounds.center.z) / (bounds.extents.z * 0.8f), -1f, 1f) * box.size.z * 0.5f;
                        // 원본은 높이가 흔들리는 지표면입니다. 모든 정점을 보행면에 놓습니다.
                        point.y = box.size.y * 0.5f + 0.002f / box.transform.localScale.y;
                        vertices[index] = point + box.center;
                    }
                    mesh.vertices = vertices;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    parts.Add(new CombineInstance { mesh = mesh,
                        transform = piece.transform.worldToLocalMatrix * box.transform.localToWorldMatrix });
                }

                var surface = new Mesh();
                temporary.Add(surface);
                surface.CombineMeshes(parts.ToArray(), true, true);
                // 기존 볼륨은 지표면 아래의 흙 단면을 채우며 충돌체 자체는 변경하지 않습니다.
                Mesh volume = piece.transform.Find("Visual").GetComponent<MeshFilter>().sharedMesh;
                var result = new Mesh { name = piece.name };
                result.CombineMeshes(new[] { new CombineInstance { mesh = surface, transform = Matrix4x4.identity },
                    new CombineInstance { mesh = volume, transform = Matrix4x4.identity } }, false, true);
                result.RecalculateBounds();
                return result;
            }
            finally
            {
                foreach (Mesh mesh in temporary) Object.DestroyImmediate(mesh);
            }
        }

        public static void Verify()
        {
            Create();
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            TerrainIntegrationControls controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
            TerrainGenerationResult result = controls.Regenerate(false);
            if (!result.Success || !string.IsNullOrEmpty(controls.NavigationFailure))
                throw new InvalidOperationException(result.FailureReason + controls.NavigationFailure);

            foreach (TerrainPiece piece in result.Pieces)
            {
                Mesh mesh = piece.transform.Find("Visual").GetComponent<MeshFilter>().sharedMesh;
                if (!AssetDatabase.GetAssetPath(mesh).StartsWith(OutputFolder + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Kingdom 외형이 적용되지 않았습니다: " + piece.name);
                if (Vector3.Distance(mesh.bounds.min, piece.LocalOccupancy.min) > 0.02f
                    || Vector3.Distance(mesh.bounds.max, piece.LocalOccupancy.max) > 0.02f)
                    throw new InvalidOperationException("Kingdom 메시가 기존 점유 경계와 다릅니다: " + piece.name);
            }

            TerrainIntegrationValidation.Capture(controls, 1, "kingdom-overview");
            TerrainIntegrationValidation.Capture(controls, 0, "kingdom-player");
            Debug.Log($"KINGDOM_TERRAIN_PASS pieces={result.Pieces.Count} paths={controls.Generator.ReachableSamples}");
            MicroTerrainValidation.Run();
        }

        [MenuItem("Nomad/Terrain/Kingdom/Inspect Sources")]
        public static void Inspect()
        {
            const string folder = "Assets/ThirdParty/Synty_Kingdom/PolygonFantasyKingdom/Prefabs/Environments/";
            foreach (string name in new[] { "Ground_Flat_01", "Ground_Flat_02", "Ground_Raised_01", "Ground_Ramp_01", "Rock_Cliff_01" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "SM_Env_" + name + ".prefab");
                foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
                    Debug.Log($"KINGDOM_SOURCE {name} bounds={filter.sharedMesh.bounds} vertices={filter.sharedMesh.vertexCount} materials={filter.GetComponent<Renderer>().sharedMaterials.Length}");
            }
        }
    }
}
