using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class TerrainSurfaceChecks
    {
        public static float? Height(TerrainPiece piece, Vector3 position)
        {
            float? top = null;
            foreach (Collider collider in piece.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.Raycast(new Ray(position + Vector3.up * 5f, Vector3.down), out RaycastHit hit, 10f)) continue;
                top = top.HasValue ? Mathf.Max(top.Value, hit.point.y) : hit.point.y;
            }
            return top;
        }

        public static void ValidateField(IReadOnlyList<TerrainPiece> pieces, int width, int height)
        {
            int columns = width * 20;
            int rows = height * 20;
            var empty = new bool[columns * rows];
            for (int cell = 0; cell < pieces.Count; cell++)
            {
                TerrainPiece piece = pieces[cell];
                for (int z = 0; z < 20; z++)
                for (int x = 0; x < 20; x++)
                {
                    Vector3 point = piece.transform.position + new Vector3(-4.75f + x * 0.5f, 0f, -4.75f + z * 0.5f);
                    int column = cell % width * 20 + x;
                    int row = cell / width * 20 + z;
                    empty[row * columns + column] = !Height(piece, point).HasValue;
                }
                foreach (int side in new[] { 0, 1 })
                {
                    int neighbour = WfcTerrainSolver.Neighbour(cell, side, width, height);
                    if (neighbour < 0) continue;
                    for (int sample = 0; sample < 20; sample++)
                    {
                        float coordinate = -4.75f + sample * 0.5f;
                        Vector3 edge = side == 0 ? new Vector3(coordinate, 0f, 5f) : new Vector3(5f, 0f, coordinate);
                        Vector3 point = piece.transform.position + edge;
                        float? a = Height(piece, point - WfcTileRules.Outward[side] * 0.05f);
                        float? b = Height(pieces[neighbour], point + WfcTileRules.Outward[side] * 0.05f);
                        Require(!a.HasValue || !b.HasValue || Mathf.Abs(a.Value - b.Value) <= 0.251f,
                            "boundary elevation mismatch: " + piece.name + " / " + pieces[neighbour].name);
                    }
                }
            }

            var reached = new bool[empty.Length];
            var queue = new Queue<int>();
            for (int cell = 0; cell < empty.Length; cell++)
            {
                if (!empty[cell] || (cell % columns != 0 && cell % columns != columns - 1
                    && cell / columns != 0 && cell / columns != rows - 1)) continue;
                reached[cell] = true;
                queue.Enqueue(cell);
            }
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                for (int side = 0; side < 4; side++)
                {
                    int neighbour = WfcTerrainSolver.Neighbour(cell, side, columns, rows);
                    if (neighbour < 0 || !empty[neighbour] || reached[neighbour]) continue;
                    reached[neighbour] = true;
                    queue.Enqueue(neighbour);
                }
            }
            Require(!empty.Where((value, index) => value && !reached[index]).Any(), "enclosed interior hole");
        }

        public static void ValidateFixtures()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                for (int scenario = 0; scenario < 3; scenario++)
                {
                    var root = new GameObject("SurfaceRegressionFixture");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    try
                    {
                        var pieces = new List<TerrainPiece>();
                        for (int cell = 0; cell < 9; cell++)
                        {
                            bool special = cell == (scenario == 0 ? 3 : 4);
                            string source = special ? (scenario == 2 ? "ModularCross" : "ModularTee") : "WfcLow";
                            string folder = special ? ModularTileAuthoring.Output : WfcCatalogAuthoring.Folder;
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{source}.prefab");
                            var piece = Object.Instantiate(prefab, root.transform).GetComponent<TerrainPiece>();
                            piece.transform.position = new Vector3(cell % 3 * 10f, 0f, cell / 3 * 10f);
                            if (special) piece.GetComponent<ModularTerrainTile>().ApplyCombination(scenario == 2 ? 41 : 12);
                            pieces.Add(piece);
                        }
                        Physics.SyncTransforms();
                        bool rejected = false;
                        try { ValidateField(pieces, 3, 3); }
                        catch (InvalidOperationException) { rejected = true; }
                        Require(rejected == (scenario != 0), "boundary allowed / interior tee void and wrong corner rejected");
                    }
                    finally
                    {
                        Object.DestroyImmediate(root);
                    }
                }
                Debug.Log("TERRAIN_SURFACE_FIXTURES_PASS: boundary tee cutout allowed; enclosed tee hole and high corner beside low floor rejected");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("TERRAIN_SURFACE_FAIL: " + message);
        }
    }
}
