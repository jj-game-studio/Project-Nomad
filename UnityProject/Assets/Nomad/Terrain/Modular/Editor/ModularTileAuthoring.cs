using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nomad.Terrain.Editor
{
    public static class ModularTileAuthoring
    {
        public const string Output = "Assets/Nomad/Terrain/Modular/Prefabs";
        public static readonly string[] Names = { "ModularCross", "ModularTee", "WideStairs" };
        public static readonly Vector3[] CrossCenters =
        {
            new Vector3(-3.5f, 0f, -3.5f), new Vector3(3.5f, 0f, -3.5f),
            new Vector3(3.5f, 0f, 3.5f), new Vector3(-3.5f, 0f, 3.5f)
        };
        public static readonly Vector3[] TeeCenters =
        {
            new Vector3(-3.5f, 0f, 0f), new Vector3(3.5f, 0f, -3.5f),
            new Vector3(3.5f, 0f, 3.5f)
        };

        [MenuItem("Nomad/Terrain/Modular/Create Shared Tile Prefabs")]
        public static void Create()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            CreateJunction(false);
            CreateJunction(true);
            CreateWide();
            AssetDatabase.SaveAssets();
        }

        private static void CreateJunction(bool tee)
        {
            var root = new GameObject(tee ? Names[1] : Names[0]);
            try
            {
                AddBox(root.transform, "CentralLanding", new Vector3(0f, 1.875f, 0f), new Vector3(4f, 0.25f, 4f));
                var ports = new List<TerrainConnection>();
                Vector3[] directions = tee
                    ? new[] { Vector3.back, Vector3.right, Vector3.forward }
                    : new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left };

                foreach (Vector3 outward in directions)
                {
                    Transform arm = Group(root.transform, "StairArm");
                    arm.localRotation = Quaternion.LookRotation(-outward);
                    arm.localPosition = outward * 3.5f;
                    AddSteps(arm, 4f, 3f);
                    ports.Add(AddPort(root.transform, outward * 5f, outward));
                }

                Vector3[] centers = tee ? TeeCenters : CrossCenters;
                var regions = new TerrainFloorRegion[centers.Length];
                for (int index = 0; index < centers.Length; index++)
                {
                    Vector3 size = tee && index == 0 ? new Vector3(3f, 0.25f, 10f) : new Vector3(3f, 0.25f, 3f);
                    regions[index] = CreateRegion(root.transform, index, centers[index], size);
                }

                root.AddComponent<TerrainPiece>().Configure(TerrainPieceKind.Stairs, Occupancy(), ports.ToArray());
                root.AddComponent<ModularTerrainTile>().Configure(regions, ports.ToArray());
                PrefabUtility.SaveAsPrefabAsset(root, $"{Output}/{root.name}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TerrainFloorRegion CreateRegion(Transform root, int index, Vector3 center, Vector3 size)
        {
            var region = new TerrainFloorRegion();
            for (int level = 0; level < 2; level++)
            {
                float height = level * 2f;
                Transform group = Group(root, $"Region_{index}_{(level == 0 ? "Low" : "High")}");
                AddBox(group, "Floor", center + Vector3.up * (height - 0.125f), size);
                var ports = new List<TerrainConnection>();
                Vector3[] directions = { Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
                foreach (Vector3 direction in directions)
                {
                    Vector3 edge = center + Vector3.Scale(direction, size) * 0.5f;
                    if (Mathf.Abs(edge.x) < 4.99f && Mathf.Abs(edge.z) < 4.99f)
                        continue;

                    // 바닥이 실제로 닿는 외곽 변에만 2m 규격 연결점을 둡니다.
                    ports.Add(AddPort(group, edge + Vector3.up * height, direction));
                }

                if (level == 0)
                {
                    region.low = group.gameObject;
                    region.lowPorts = ports.ToArray();
                }
                else
                {
                    region.high = group.gameObject;
                    region.highPorts = ports.ToArray();
                }
            }

            return region;
        }

        private static void CreateWide()
        {
            var root = new GameObject(Names[2]);
            try
            {
                AddBox(root.transform, "LowerLanding", new Vector3(0f, -0.125f, -3.5f), new Vector3(10f, 0.25f, 3f));
                AddBox(root.transform, "UpperLanding", new Vector3(0f, 1.875f, 3.5f), new Vector3(10f, 0.25f, 3f));
                AddSteps(root.transform, 10f, 4f);
                var ports = new List<TerrainConnection>();
                // 폭은 전체 10m지만 기존 2m 연결 규격을 유지하는 세 통로를 제공합니다.
                foreach (float lane in new[] { -3.5f, 0f, 3.5f })
                {
                    ports.Add(AddPort(root.transform, new Vector3(lane, 0f, -5f), Vector3.back));
                    ports.Add(AddPort(root.transform, new Vector3(lane, 2f, 5f), Vector3.forward));
                }

                root.AddComponent<TerrainPiece>().Configure(TerrainPieceKind.Stairs, Occupancy(), ports.ToArray());
                PrefabUtility.SaveAsPrefabAsset(root, $"{Output}/{root.name}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AddSteps(Transform parent, float width, float length)
        {
            for (int index = 0; index < 8; index++)
            {
                float height = (index + 1) * 0.25f;
                AddBox(parent, $"Step_{index}", new Vector3(0f, height * 0.5f, -length * 0.5f + (index + 0.5f) * length / 8f),
                    new Vector3(width, height, length / 8f));
            }
        }

        private static void AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
        }

        private static TerrainConnection AddPort(Transform parent, Vector3 position, Vector3 outward)
        {
            Transform port = Group(parent, "Port");
            port.localPosition = position;
            port.localRotation = Quaternion.LookRotation(outward);
            return port.gameObject.AddComponent<TerrainConnection>();
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static Bounds Occupancy() => new Bounds(new Vector3(0f, 0.875f, 0f), new Vector3(10f, 2.25f, 10f));
    }

}
