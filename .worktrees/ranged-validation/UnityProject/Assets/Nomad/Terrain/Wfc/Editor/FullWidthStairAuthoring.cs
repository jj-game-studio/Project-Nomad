using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class FullWidthStairAuthoring
    {
        public static readonly string[] Names = { "WideUHigh", "WideULow", "WideLHigh", "WideLLow" };

        public static void Create()
        {
            for (int index = 0; index < Names.Length; index++) CreatePiece(index);
        }

        private static void CreatePiece(int index)
        {
            bool u = index < 2;
            bool inverted = index % 2 == 1;
            var root = new GameObject(Names[index]);
            var combine = new List<CombineInstance>();
            Material stairMaterial = null;
            try
            {
                for (int step = 0; step < 8; step++)
                {
                    float outer = step * 0.375f;
                    float inner = outer + 0.375f;
                    float height = (step + 1) * 0.25f;
                    if (inverted) height = 2f - height;
                    if (u)
                    {
                        AddStrip(root, combine, "West_" + step, -5f + outer, -5f + inner, -5f + outer, 5f, height, ref stairMaterial);
                        AddStrip(root, combine, "East_" + step, 5f - inner, 5f - outer, -5f + outer, 5f, height, ref stairMaterial);
                        AddStrip(root, combine, "South_" + step, -5f + inner, 5f - inner, -5f + outer, -5f + inner, height, ref stairMaterial);
                    }
                    else
                    {
                        AddStrip(root, combine, "West_" + step, -5f + outer, -5f + inner, -5f, 5f - outer, height, ref stairMaterial);
                        AddStrip(root, combine, "North_" + step, -5f + inner, 5f, 5f - inner, 5f - outer, height, ref stairMaterial);
                    }
                }

                float floorHeight = inverted ? 0f : 2f;
                GameObject floor = Box(root, "Plateau", -2f, u ? 2f : 5f,
                    u ? -2f : -5f, u ? 5f : 2f, floorHeight);
                floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    $"{WfcCatalogAuthoring.Folder}/{(inverted ? "WfcLow" : "WfcHigh")}.mat");
                var mesh = new Mesh { name = root.name + "Steps" };
                mesh.CombineMeshes(combine.ToArray(), true, true);
                string meshPath = $"{WfcCatalogAuthoring.Folder}/{root.name}.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
                else
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    Object.DestroyImmediate(mesh);
                    mesh = existing;
                }
                var visual = new GameObject("ContinuousSteps", typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(root.transform, false);
                visual.GetComponent<MeshFilter>().sharedMesh = mesh;
                visual.GetComponent<MeshRenderer>().sharedMaterial = stairMaterial;
                var ports = new List<TerrainConnection>();
                for (int side = 0; side < 4; side++)
                {
                    bool stairEdge = u ? side != 0 : side == 0 || side == 3;
                    for (int lane = 0; lane < 3; lane++)
                    {
                        if (!stairEdge && lane != 1) continue;
                        var point = new GameObject($"Port_{side}_{lane}");
                        point.transform.SetParent(root.transform, false);
                        float height = stairEdge ? 2f - floorHeight : floorHeight;
                        point.transform.localPosition = WfcTileRules.EdgePoint(side, lane) + Vector3.up * height;
                        point.transform.localRotation = Quaternion.LookRotation(WfcTileRules.Outward[side]);
                        ports.Add(point.AddComponent<TerrainConnection>());
                    }
                }
                root.AddComponent<TerrainPiece>().Configure(TerrainPieceKind.Stairs,
                    new Bounds(new Vector3(0f, 0.875f, 0f), new Vector3(10f, 2.25f, 10f)), ports.ToArray());
                root.AddComponent<FullWidthStairTile>().Configure(u ? FullWidthStairLayout.U : FullWidthStairLayout.L, inverted);
                PrefabUtility.SaveAsPrefabAsset(root, $"{WfcCatalogAuthoring.Folder}/{root.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddStrip(GameObject root, List<CombineInstance> combine, string name,
            float minX, float maxX, float minZ, float maxZ, float height, ref Material material)
        {
            GameObject strip = Box(root, name, minX, maxX, minZ, maxZ, height);
            var filter = strip.GetComponent<MeshFilter>();
            material = strip.GetComponent<Renderer>().sharedMaterial;
            combine.Add(new CombineInstance { mesh = filter.sharedMesh, transform = strip.transform.localToWorldMatrix });
            Object.DestroyImmediate(strip.GetComponent<Renderer>());
            Object.DestroyImmediate(filter);
        }

        private static GameObject Box(GameObject root, string name, float minX, float maxX, float minZ, float maxZ, float top)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(root.transform, false);
            float height = top + 0.25f;
            box.transform.localPosition = new Vector3((minX + maxX) * 0.5f, top - height * 0.5f, (minZ + maxZ) * 0.5f);
            box.transform.localScale = new Vector3(maxX - minX, height, maxZ - minZ);
            return box;
        }
    }
}
