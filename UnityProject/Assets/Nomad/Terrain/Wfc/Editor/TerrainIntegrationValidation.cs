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
    public static class TerrainIntegrationValidation
    {
        public static void ValidateSingleSurface(TerrainPiece piece)
        {
            Collider[] colliders = piece.GetComponentsInChildren<Collider>();
            for (int x = -4; x <= 4; x++)
            for (int z = -4; z <= 4; z++)
            {
                Vector3 origin = piece.transform.TransformPoint(new Vector3(x * 1.1f + 0.03f, 5f, z * 1.1f + 0.07f));
                var heights = new List<float>();
                foreach (Collider collider in colliders)
                    if (!collider.isTrigger && collider.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, 10f))
                        heights.Add(hit.point.y);
                Require(heights.Count < 2 || heights.Max() - heights.Min() <= 0.251f,
                    "stacked walkable surfaces in " + piece.name);
            }
        }

        [MenuItem("Nomad/Terrain/Run Integration Validation")]
        public static void Run()
        {
            try
            {
                EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
                var controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
                TerrainGenerator generator = controls.Generator;
                var catalog = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(WfcCatalogAuthoring.CatalogPath);
                Require(catalog.variants.Length == 444, "108 combinations x4 + wide x4 + floors x8");
                Require(TerrainIntegrationAuthoring.LoadLegacy().All(p => p != null), "9 legacy prefabs loaded");
                RuleCases(catalog);
                int starts = 0;
                int completions = 0;
                generator.GenerationStarted += _ => starts++;
                generator.GenerationCompleted += _ => completions++;
                var seen = new HashSet<string>();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                int maxChecks = 0;
                for (int seed = 0; seed < 30; seed++)
                {
                    var data = new SerializedObject(controls);
                    data.FindProperty("seed").intValue = seed;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    TerrainGenerationResult first = controls.Regenerate(false);
                    Validate(first, seen);
                    Require(Object.FindAnyObjectByType<Nomad.Props.PropScatter>().Positions.Count > 0, "prop consumer actually places props");
                    string signature = Signature(first);
                    string props = string.Join(";", Object.FindAnyObjectByType<Nomad.Props.PropScatter>().Positions);
                    TerrainGenerationResult second = controls.Regenerate(false);
                    Validate(second, seen);
                    Require(first.GenerationId != second.GenerationId && signature == Signature(second), "same seed replay");
                    Require(props == string.Join(";", Object.FindAnyObjectByType<Nomad.Props.PropScatter>().Positions), "prop consumer replay");
                    maxChecks = Mathf.Max(maxChecks, first.CandidateChecks);
                }
                Require(starts == 60 && completions == 60, "one result per generation");
                generator.ConfigureWfc(catalog, 6, 6, 51, new Bounds(Vector3.zero, Vector3.one));
                TerrainGenerationResult failure = generator.Generate();
                Require(!failure.Success && failure.Attempts == 4 && failure.Pieces.Count == 0 && failure.Links.Count == 0,
                    "bounded small area failure cleanup");
                generator.ConfigureWfc(catalog, 0, 6, 51, new Bounds(Vector3.zero, Vector3.one * 400f));
                Require(!generator.Generate().Success && generator.LastResult.Attempts == 0, "invalid grid fails before solve");
                Require(TerrainGenerator.FootprintsOverlap(new Bounds(Vector3.zero, Vector3.one * 10f),
                    new Bounds(Vector3.up * 100f, Vector3.one * 10f)), "legacy vertical overlap forbidden");
                Require(!TerrainGenerator.FootprintsOverlap(new Bounds(Vector3.zero, Vector3.one * 10f),
                    new Bounds(Vector3.right * 10f, Vector3.one * 10f)), "adjacent footprints allowed");

                // 기존 연결 생성의 seed/이벤트/실패/분기 회귀도 같은 통합 진입점에서 실행합니다.
                TerrainGenerationValidation.Validate();
                ModularTileValidation.Validate();
                var finalData = new SerializedObject(controls);
                finalData.FindProperty("seed").intValue = 51;
                finalData.ApplyModifiedPropertiesWithoutUndo();
                Validate(controls.Regenerate(false), seen);
                Capture(controls, 0, "terrain-integration-player");
                Capture(controls, 1, "terrain-integration-overview");
                Capture(controls, 2, "terrain-integration-gallery");
                controls.SetView(0);
                string report = $"TERRAIN_INTEGRATION_PASS: 30 seeds x2 WFC, 444 candidates, {seen.Count} distinct instantiated variants, max checks {maxChecks}, {watch.Elapsed.TotalSeconds:F2}s; same scene, prop replay, no XZ stacking, required exits, adjacency/wide exception, failure cleanup, legacy 100x2, modular 436/228";
                File.WriteAllText("Artifacts/terrain-integration-results.txt", report);
                Debug.Log(report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void RuleCases(WfcTerrainCatalog catalog)
        {
            WfcTileVariant regular = catalog.variants.First(v => v.prefab.name == "ModularCross" && v.combination == 40 && v.turn == 0);
            WfcTileVariant wide = catalog.variants.First(v => v.wide && v.turn == 0);
            WfcTileVariant low = catalog.variants.First(v => v.prefab.name == "WfcLow");
            Require(!WfcTileRules.Compatible(regular, regular, 0), "ordinary stairs adjacent rejected");
            Require(WfcTileRules.Compatible(wide, wide, 1), "wide stairs side adjacency permitted");
            Require(WfcTileRules.Compatible(regular, low, 0), "ordinary exit supported by low floor");
            var empty = new WfcTileVariant { surfaces = Enumerable.Repeat(-99, 12).ToArray(), required = Enumerable.Repeat(-99, 12).ToArray() };
            Require(!WfcTileRules.Compatible(regular, empty, 0), "unsupported exit rejected");
            int checks = 0;
            Require(!new WfcTerrainSolver(new[] { regular }).Solve(1, 1, new System.Random(1), out _, ref checks), "boundary exit rejected");
        }

        private static void Validate(TerrainGenerationResult result, HashSet<string> seen)
        {
            Require(result.Success, "WFC generation: " + result.FailureReason + " seed=" + result.Seed);
            Require(result.Pieces.Count == 36 && result.Pieces.Any(p => p.Kind == TerrainPieceKind.Stairs), "36 cells with height variation");
            Physics.SyncTransforms();
            for (int index = 0; index < result.Pieces.Count; index++)
            {
                TerrainPiece piece = result.Pieces[index];
                Require(Mathf.Abs(piece.transform.position.y) < 0.001f, "common base elevation");
                if (seen.Add(piece.name)) ValidateSingleSurface(piece);
                for (int other = index + 1; other < result.Pieces.Count; other++)
                {
                    TerrainPiece neighbour = result.Pieces[other];
                    Require(!TerrainGenerator.FootprintsOverlap(piece.WorldOccupancy, neighbour.WorldOccupancy), "no XZ overlap");
                    Vector3 delta = neighbour.transform.position - piece.transform.position;
                    bool adjacent = Mathf.Abs(Mathf.Abs(delta.x) + Mathf.Abs(delta.z) - 10f) < 0.01f;
                    Require(!adjacent || piece.GetComponent<ModularTerrainTile>() == null
                        || neighbour.GetComponent<ModularTerrainTile>() == null, "no ordinary stair edge adjacency");
                }
                if (piece.Kind != TerrainPieceKind.Stairs) continue;
                foreach (TerrainConnection port in piece.Connections)
                {
                    if (port.transform.parent != piece.transform) continue;
                    Vector3 tangent = Vector3.Cross(Vector3.up, port.Outward);
                    foreach (float lane in new[] { -0.9f, 0f, 0.9f })
                    {
                        Vector3 origin = port.Position + port.Outward * 0.1f + tangent * lane + Vector3.up * 4f;
                        Require(Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f)
                            && hit.collider.GetComponentInParent<TerrainPiece>() != piece,
                            "required exit neighbouring floor");
                        Require(hit.point.y >= port.Position.y - 0.01f && hit.point.y <= port.Position.y + 0.251f, "exit floor elevation");
                    }
                }
            }
        }

        public static string Signature(TerrainGenerationResult result) => string.Join(";",
            result.Pieces.Select(p => p.name + p.transform.position.ToString("F3") + p.transform.eulerAngles.ToString("F1")));

        public static void Capture(TerrainIntegrationControls controls, int view, string name)
        {
            controls.SetView(view);
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            Camera camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Artifacts");
                File.WriteAllBytes($"Artifacts/{name}.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("TERRAIN_INTEGRATION_FAIL: " + message);
        }
    }
}
