using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Terrain
{
    public sealed class MicroTerrainWalls : MonoBehaviour
    {
        public const float Height = 2.2f;
        public const float Thickness = 0.15f;
        private Mesh ownedMesh;
        public int SegmentCount { get; private set; }

        public static MicroTerrainWalls Build(Transform parent, int width, int height, int[] choices, WfcTerrainCatalog catalog)
        {
            var root = new GameObject("CliffAndBoundaryWalls");
            root.transform.SetParent(parent, false);
            var owner = root.AddComponent<MicroTerrainWalls>();
            var combine = new List<CombineInstance>();
            Material material = null;
            for (int cell = 0; cell < choices.Length; cell++)
            {
                WfcTileVariant tile = catalog.variants[choices[cell]];
                for (int side = 0; side < 4; side++)
                {
                    int neighbour = WfcTerrainSolver.Neighbour(cell, side, width, height);
                    WfcTileVariant other = neighbour < 0 ? null : catalog.variants[choices[neighbour]];
                    if (other != null && !tile.cliff && !other.cliff) continue;
                    int sample = 0;
                    while (sample < 20)
                    {
                        int top = tile.edgeHeights[side * 20 + sample];
                        bool NeedsWall(int index) => other == null || top - other.edgeHeights[((side + 2) % 4) * 20 + index] > 1;
                        if (!NeedsWall(sample)) { sample++; continue; }
                        int end = sample + 1;
                        while (end < 20 && tile.edgeHeights[side * 20 + end] == top && NeedsWall(end)) end++;
                        Vector3 outward = WfcTileRules.Outward[side];
                        Vector3 tangent = side % 2 == 0 ? Vector3.right : Vector3.forward;
                        Vector3 cellCenter = new Vector3(cell % width * 3f, 0f, cell / width * 3f);
                        Vector3 center = cellCenter + outward * (1.5f - Thickness * 0.5f)
                            + tangent * (-1.5f + (sample + end) * 0.075f) + Vector3.up * (top * 0.25f + Height * 0.5f);
                        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        box.name = "GuardWall_" + owner.SegmentCount++;
                        box.transform.SetParent(root.transform, false);
                        box.transform.localPosition = center;
                        box.transform.localRotation = Quaternion.LookRotation(outward);
                        box.transform.localScale = new Vector3((end - sample) * 0.15f, Height, Thickness);
                        material = box.GetComponent<Renderer>().sharedMaterial;
                        combine.Add(new CombineInstance { mesh = box.GetComponent<MeshFilter>().sharedMesh,
                            transform = root.transform.worldToLocalMatrix * box.transform.localToWorldMatrix });
                        DestroyImmediate(box.GetComponent<Renderer>());
                        DestroyImmediate(box.GetComponent<MeshFilter>());
                        sample = end;
                    }
                }
            }
            owner.ownedMesh = new Mesh { name = "BoundaryWalls" };
            owner.ownedMesh.CombineMeshes(combine.ToArray(), true, true);
            root.AddComponent<MeshFilter>().sharedMesh = owner.ownedMesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            return owner;
        }

        private void OnDestroy()
        {
            if (ownedMesh == null) return;
            if (Application.isPlaying) Destroy(ownedMesh);
            else DestroyImmediate(ownedMesh);
        }
    }
}
