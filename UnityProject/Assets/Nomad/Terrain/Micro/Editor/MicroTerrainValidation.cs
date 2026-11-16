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
    public static class MicroTerrainValidation
    {
        public static void Smoke()
        {
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            var controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
            TerrainGenerationResult result = controls.Regenerate(false);
            Debug.Log($"MICRO_SMOKE success={result.Success} pieces={result.Pieces.Count} attempts={result.Attempts} samples={controls.Generator.ReachableSamples} error={result.FailureReason} nav={controls.Generator.WalkabilityFailure}");
            if (!result.Success) EditorApplication.Exit(1);
            else
            {
                TerrainIntegrationValidation.Capture(controls, 1, "micro-overview");
                TerrainIntegrationValidation.Capture(controls, 0, "micro-player");
            }
        }

        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
                var controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
                WfcTerrainCatalog catalog = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(MicroTerrainAuthoring.CatalogPath);
                Require(catalog.variants.Length == 29 && catalog.cellSize == 3f, "3m sources / 29 variants");
                Rules(catalog);
                NavigationFixtures(catalog);
                int maxChecks = 0;
                var seen = new HashSet<MicroTerrainShape>();
                int surfaces = 0;
                for (int seed = 0; seed < 20; seed++)
                {
                    var data = new SerializedObject(controls);
                    data.FindProperty("seed").intValue = seed;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    TerrainGenerationResult first = controls.Regenerate(false);
                    Validate(first, controls.Generator, seen);
                    Require(controls.Player.activeSelf && string.IsNullOrEmpty(controls.NavigationFailure), "props and post-placement walkability seed=" + seed + " active=" + controls.Player.activeSelf + " props=" + Object.FindAnyObjectByType<Nomad.Props.PropScatter>().Positions.Count + " error=" + controls.NavigationFailure);
                    string signature = TerrainIntegrationValidation.Signature(first);
                    TerrainGenerationResult second = controls.Regenerate(false);
                    Validate(second, controls.Generator, seen);
                    Require(signature == TerrainIntegrationValidation.Signature(second), "seed replay");
                    maxChecks = Mathf.Max(maxChecks, first.CandidateChecks);
                    surfaces += controls.Generator.ReachableSamples * 2;
                }
                Require(seen.Count == 5, "flat/straight/convex/concave/cliff all generated");
                foreach (Vector2Int size in new[] { new Vector2Int(12, 12), new Vector2Int(12, 18), new Vector2Int(24, 12), new Vector2Int(32, 32) })
                {
                    controls.Generator.ConfigureWfc(catalog, size.x, size.y, 51, new Bounds(Vector3.zero, new Vector3(400f, 100f, 400f)));
                    TerrainGenerationResult varied = controls.Generator.Generate();
                    Require(varied.Success && varied.Pieces.Count == size.x * size.y
                        && controls.Generator.ReachableSamples == size.x * size.y * 9,
                        "rectangular/min/max size paths: " + size + " / " + controls.Generator.WalkabilityFailure);
                }
                TerrainGenerationValidation.Validate();
                ModularTileValidation.Validate();
                FullWidthStairValidation.Validate();
                controls.Generator.ConfigureWfc(catalog, 12, 12, 51, new Bounds(Vector3.zero, Vector3.one));
                Require(!controls.Generator.Generate().Success && controls.Generator.LastResult.Attempts == 4, "bounded small area failure");
                var final = new SerializedObject(controls);
                final.FindProperty("seed").intValue = 51;
                final.ApplyModifiedPropertiesWithoutUndo();
                Validate(controls.Regenerate(false), controls.Generator, seen);
                TerrainIntegrationValidation.Capture(controls, 1, "micro-overview");
                TerrainIntegrationValidation.Capture(controls, 0, "micro-player");
                TerrainIntegrationValidation.Capture(controls, 2, "micro-gallery");
                controls.SetView(0);
                string report = $"MICRO_TERRAIN_PASS: 20 seeds x2, 324 3m tiles, levels0/1/2, {surfaces} NavMesh paths across all tile samples, 5 families, no opposing stairs, walls/cliffs, replay, max checks {maxChecks}; legacy/modular regressions";
                File.WriteAllText("Artifacts/micro-terrain-results.txt", report);
                Debug.Log(report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void Validate(TerrainGenerationResult result, TerrainGenerator generator, HashSet<MicroTerrainShape> seen)
        {
            Require(result.Success && result.Pieces.Count == 324, "generated reachable field: " + result.FailureReason + " / " + generator.WalkabilityFailure);
            Require(generator.ReachableSamples == result.Pieces.Count * 9, "every tile surface has path");
            var levels = new HashSet<int>();
            bool twoMetreDrop = false;
            foreach (TerrainPiece piece in result.Pieces)
            {
                var tile = piece.GetComponent<MicroTerrainTile>();
                seen.Add(tile.Shape);
                if (!tile.IsStair) levels.Add(tile.Level);
                Require(piece.LocalOccupancy.size.x == 3f && piece.LocalOccupancy.size.z == 3f, "3m footprint");
                foreach (float x in new[] { -1f, 0.03f, 1f })
                foreach (float z in new[] { -1f, 0.07f, 1f })
                    Require(TerrainSurfaceChecks.Height(piece, piece.transform.TransformPoint(new Vector3(x, 0f, z))).HasValue, "no missing floor");
            }
            for (int cell = 0; cell < result.Pieces.Count; cell++)
            for (int side = 0; side < 4; side++)
            {
                int neighbour = WfcTerrainSolver.Neighbour(cell, side, 18, 18);
                if (neighbour < 0) continue;
                var a = result.Pieces[cell].GetComponent<MicroTerrainTile>();
                var b = result.Pieces[neighbour].GetComponent<MicroTerrainTile>();
                twoMetreDrop |= a.IsCliff && a.Level == 2 && !b.IsStair && b.Level == 0;
            }
            Require(twoMetreDrop, "2m cliff actually adjacent to 0m floor");
            Require(levels.SetEquals(new[] { 0, 1, 2 }), "all three elevations exist");
            MicroTerrainWalls walls = result.Pieces[0].GetComponentInParent<Transform>().root.GetComponentInChildren<MicroTerrainWalls>();
            Require(walls != null && walls.SegmentCount > 0, "cliff and outside guards");
        }

        private static void Rules(WfcTerrainCatalog catalog)
        {
            WfcTileVariant Find(MicroTerrainShape shape, int level, int turn) => catalog.variants.First(v =>
                v.prefab.GetComponent<MicroTerrainTile>().Shape == shape && v.level == level && v.turn == turn);
            WfcTileVariant up = Find(MicroTerrainShape.Stair, 0, 0);
            WfcTileVariant down = Find(MicroTerrainShape.Stair, 0, 2);
            Require(!WfcTileRules.Compatible(up, down, 0), "crest ^ forbidden");
            Require(!WfcTileRules.Compatible(down, up, 0), "valley V forbidden");
            Require(!WfcTileRules.Compatible(up, down, 1), "sideways opposite stairs forbidden");
            Require(WfcTileRules.Compatible(up, Find(MicroTerrainShape.Stair, 1, 0), 0), "continuous 0-1-2 climb allowed");
            Require(!WfcTileRules.Compatible(up, Find(MicroTerrainShape.Cliff, 2, 0), 0), "stair exit cannot terminate at higher cliff wall");
            Require(WfcTileRules.Compatible(Find(MicroTerrainShape.Cliff, 2, 0), Find(MicroTerrainShape.Flat, 0, 0), 1), "2m cliff next to 0m floor allowed");
        }

        private static void NavigationFixtures(WfcTerrainCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            SimulationMode previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                for (int scenario = 0; scenario < 2; scenario++)
                {
                    var root = new GameObject("MicroReachabilityFixture");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    try
                    {
                        string[] names = scenario == 0 ? new[] { "Micro_Flat_0", "Micro_Cliff_2" }
                            : new[] { "Micro_Flat_0", "Micro_Stair_0", "Micro_Flat_1", "Micro_Stair_1", "Micro_Flat_2" };
                        var pieces = new List<TerrainPiece>();
                        var choices = new int[names.Length];
                        for (int index = 0; index < names.Length; index++)
                        {
                            choices[index] = Array.FindIndex(catalog.variants, v => v.prefab.name == names[index]
                                && (!v.stairs || v.turn == 1));
                            WfcTileVariant variant = catalog.variants[choices[index]];
                            TerrainPiece piece = Object.Instantiate(variant.prefab, root.transform);
                            piece.transform.SetPositionAndRotation(Vector3.right * index * 3f, Quaternion.Euler(0f, variant.turn * 90f, 0f));
                            pieces.Add(piece);
                        }
                        MicroTerrainWalls.Build(root.transform, pieces.Count, 1, choices, catalog);
                        bool connected = MicroTerrainWalkability.Validate(root, pieces, out _, out _);
                        Require(connected == (scenario == 1), "isolated cliff rejected / 0-1-2 staircase connected");
                        Physics.SyncTransforms();
                        if (scenario == 1) WalkStairs(scene, scene.GetPhysicsScene());
                        else CheckGuards(scene, scene.GetPhysicsScene());
                    }
                    finally
                    {
                        Object.DestroyImmediate(root);
                    }
                }
            }
            finally
            {
                Physics.simulationMode = previous;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static CharacterController Probe(Scene scene, Vector3 position)
        {
            var root = new GameObject("MicroCharacterProbe");
            SceneManager.MoveGameObjectToScene(root, scene);
            var body = root.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = Vector3.up * 0.9f;
            body.stepOffset = 0.3f;
            body.skinWidth = 0.02f;
            body.slopeLimit = 45f;
            root.transform.position = position + Vector3.up * 0.03f;
            Physics.SyncTransforms();
            return body;
        }

        private static void WalkStairs(Scene scene, PhysicsScene physics)
        {
            CharacterController body = Probe(scene, Vector3.zero);
            try
            {
                for (int leg = 0; leg < 2; leg++)
                {
                    Vector3 target = leg == 0 ? new Vector3(12f, 2f, 0f) : Vector3.zero;
                    bool reached = false;
                    float velocity = -2f;
                    for (int step = 0; step < 400; step++)
                    {
                        Vector3 delta = target - body.transform.position;
                        delta.y = 0f;
                        if (delta.magnitude < 0.05f) { reached = true; break; }
                        velocity = body.isGrounded ? -2f : velocity - 20f / 60f;
                        body.Move(Vector3.ClampMagnitude(delta, 5f / 60f) + Vector3.up * (velocity / 60f));
                        physics.Simulate(1f / 60f);
                    }
                    Require(reached && Mathf.Abs(body.transform.position.y - target.y) < 0.3f, "actual 0-1-2 capsule round trip");
                }
            }
            finally { Object.DestroyImmediate(body.gameObject); }
        }

        private static void CheckGuards(Scene scene, PhysicsScene physics)
        {
            foreach (bool cliff in new[] { false, true })
            {
                CharacterController body = Probe(scene, cliff ? new Vector3(3f, 2f, 0f) : Vector3.zero);
                try
                {
                    float velocity = Mathf.Sqrt(2f * 20f * 1.2f);
                    for (int step = 0; step < 180; step++)
                    {
                        velocity = body.isGrounded && step > 30 ? -2f : velocity - 20f / 60f;
                        body.Move(Vector3.left * (5f / 60f) + Vector3.up * (velocity / 60f));
                        physics.Simulate(1f / 60f);
                    }
                    Require(cliff ? body.transform.position.x > 1.85f && body.transform.position.y > 1.8f
                        : body.transform.position.x > -1.1f && body.transform.position.y > -0.1f,
                        "cliff/outside guard blocks running jump");
                }
                finally { Object.DestroyImmediate(body.gameObject); }
            }
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("MICRO_TERRAIN_FAIL: " + message);
        }
    }
}
