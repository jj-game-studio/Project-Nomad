using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nomad.Terrain.Editor
{
    public static class PlanarTerrainAuthoring
    {
        [MenuItem("Nomad/Terrain/Create Planar Connection Pieces")]
        public static void Create()
        {
            CreatePiece("Corner", new[] { Vector3.back, Vector3.right });
            CreatePiece("Tee", new[] { Vector3.back, Vector3.right, Vector3.forward });
            CreatePiece("Cross", new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left });
            AssetDatabase.SaveAssets();
            Debug.Log("TERRAIN_PLANAR_AUTHORING_PASS: Corner/Tee/Cross saved; original pieces preserved");
        }

        private static void CreatePiece(string name, Vector3[] directions)
        {
            string source = TerrainPieceAuthoring.Output + "/Flat.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
                throw new InvalidOperationException("#12의 Flat 프리팹이 필요합니다.");

            GameObject root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                root.name = name;
                TerrainPiece piece = root.GetComponent<TerrainPiece>();
                foreach (TerrainConnection point in piece.Connections)
                    UnityEngine.Object.DestroyImmediate(point.gameObject);

                // 외형·충돌·점유 영역은 Flat 그대로 두고 연결 가능한 변만 정의합니다.
                TerrainConnection[] points = directions.Select(direction =>
                {
                    var point = new GameObject("Port_" + direction);
                    point.transform.SetParent(root.transform, false);
                    point.transform.localPosition = direction * 5f;
                    point.transform.localRotation = Quaternion.LookRotation(direction);
                    return point.AddComponent<TerrainConnection>();
                }).ToArray();

                piece.Configure(TerrainPieceKind.Flat, piece.LocalOccupancy, points);
                PrefabUtility.SaveAsPrefabAsset(root, $"{TerrainPieceAuthoring.Output}/{name}.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void CreateAndValidate()
        {
            Create();
            TerrainGenerationValidation.Validate();
            TerrainGenerationValidation.CapturePreview();
        }
    }
}
