using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nomad.Terrain.Editor
{
    public static class StairJunctionAuthoring
    {
        [MenuItem("Nomad/Terrain/Create Stair Junction Pieces")]
        public static void Create()
        {
            CreatePiece("StairsCorner", new[] { Vector3.back, Vector3.right });
            CreatePiece("StairsTee", new[] { Vector3.back, Vector3.right, Vector3.forward });
            CreatePiece("StairsCross", new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left });
            AssetDatabase.SaveAssets();
            Debug.Log("TERRAIN_STAIR_AUTHORING_PASS: stair Corner/Tee/Cross saved");
        }

        private static void CreatePiece(string name, Vector3[] directions)
        {
            string folder = TerrainPieceAuthoring.Output;
            GameObject stairs = PrefabUtility.LoadPrefabContents(folder + "/Stairs.prefab");
            GameObject raised = PrefabUtility.LoadPrefabContents(folder + "/Raised.prefab");
            var root = new GameObject(name);

            try
            {
                Transform visual = AddGroup(root.transform, "Visual");
                Transform collision = AddGroup(root.transform, "Collision");
                GameObject landing = UnityEngine.Object.Instantiate(
                    raised.transform.Find("Visual/Floor").gameObject, visual);
                landing.name = "CentralLanding";
                landing.transform.localScale = Vector3.Scale(landing.transform.localScale, new Vector3(0.4f, 1f, 0.4f));
                // 원본 메시의 피벗이 중앙이 아니므로 축소 후 실제 외형 중심도 참에 맞춥니다.
                Bounds landingBounds = landing.GetComponentInChildren<Renderer>().bounds;
                landing.transform.position += new Vector3(0f, 1.875f, 0f) - landingBounds.center;

                var center = new GameObject("CentralLanding");
                center.transform.SetParent(collision, false);
                var floor = center.AddComponent<BoxCollider>();
                floor.center = new Vector3(0f, 1.875f, 0f);
                floor.size = new Vector3(4f, 0.25f, 4f);

                TerrainConnection[] points = directions.Select(direction =>
                {
                    Transform artArm = AddArm(visual, direction);
                    UnityEngine.Object.Instantiate(stairs.transform.Find("Visual/Stairs").gameObject, artArm);
                    Transform physicsArm = AddArm(collision, direction);
                    foreach (Transform step in stairs.transform.Find("Collision"))
                    {
                        if (step.name.StartsWith("Step_", StringComparison.Ordinal))
                            UnityEngine.Object.Instantiate(step.gameObject, physicsArm);
                    }

                    Transform point = AddGroup(root.transform, "Port_" + direction);
                    point.localPosition = direction * 5f;
                    point.localRotation = Quaternion.LookRotation(direction);
                    return point.gameObject.AddComponent<TerrainConnection>();
                }).ToArray();

                root.AddComponent<TerrainPiece>().Configure(TerrainPieceKind.Stairs,
                    stairs.GetComponent<TerrainPiece>().LocalOccupancy, points);
                PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{name}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                PrefabUtility.UnloadPrefabContents(stairs);
                PrefabUtility.UnloadPrefabContents(raised);
            }
        }

        private static Transform AddArm(Transform parent, Vector3 outward)
        {
            Transform arm = AddGroup(parent, "Arm_" + outward);
            arm.localPosition = outward * 3.5f;
            arm.localRotation = Quaternion.LookRotation(-outward);
            // 기존 4m 길이 계단을 3m에 맞춰 바깥 0m → 중앙 2m로 연결합니다.
            arm.localScale = new Vector3(1f, 1f, 0.75f);
            return arm;
        }

        private static Transform AddGroup(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        public static void CreateAndValidate()
        {
            Create();
            StairJunctionValidation.Validate();
            TerrainGenerationValidation.Validate();
            TerrainGenerationValidation.CapturePreview();
            StairJunctionValidation.CaptureOverview();
        }
    }
}
