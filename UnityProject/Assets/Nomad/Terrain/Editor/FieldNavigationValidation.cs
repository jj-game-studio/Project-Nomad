using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nomad.Props;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class FieldNavigationValidation
    {
        [MenuItem("Nomad/Terrain/Validate Field Navigation")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
                var controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
                if (controls.Navigation == null) controls.gameObject.AddComponent<FieldNavigation>();
                // 런타임 지형을 저장하지 않고 연결 컴포넌트만 기존 씬에 반영합니다.
                EditorSceneManager.MarkSceneDirty(controls.gameObject.scene);
                EditorSceneManager.SaveScene(controls.gameObject.scene);
                var navigation = controls.Navigation;
                var scatter = Object.FindAnyObjectByType<PropScatter>();
                int notifications = 0;
                int starts = 0;
                navigation.GenerationStarted += _ =>
                {
                    Require(navigation.LastResult == null && !navigation.HasNavigation, "old result/mesh invalidated before start");
                    starts++;
                };
                navigation.GenerationCompleted += result =>
                {
                    Require(navigation.LastResult == result, "late subscriber snapshot");
                    notifications++;
                };

                int enemyTotal = 0;
                int chestTotal = 0;
                foreach (int seed in new[] { 0, 1, 7, 18, 51 })
                {
                    var serialized = new SerializedObject(controls);
                    serialized.FindProperty("seed").intValue = seed;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    TerrainGenerationResult first = controls.Regenerate(false);
                    Verify(controls, first);
                    string signature = Signature(navigation.LastResult);
                    enemyTotal += navigation.LastResult.EnemyPoints.Count;
                    chestTotal += navigation.LastResult.ChestPoints.Count;
                    Require(navigation.Prepare(first, scatter.gameObject) == navigation.LastResult, "idempotent finalization");
                    int before = notifications;
                    Require(controls.Regenerate(false).GenerationId != first.GenerationId, "new generation id");
                    Verify(controls, controls.Generator.LastResult);
                    Require(notifications == before + 1 && Signature(navigation.LastResult) == signature, "single event and replay slots");
                    Debug.Log($"FIELD_NAV_SEED {seed}: enemies={navigation.LastResult.EnemyPoints.Count}, chests={navigation.LastResult.ChestPoints.Count}");
                }
                Require(enemyTotal > 0 && chestTotal > 0, "nonempty spawn candidates across fields");
                Require(notifications == 10 && starts == 10, "one start/completion per generation");

                WfcTerrainCatalog catalog = AssetDatabase.LoadAssetAtPath<WfcTerrainCatalog>(MicroTerrainAuthoring.CatalogPath);
                controls.Player.SetActive(false);
                controls.Generator.ConfigureWfc(catalog, 12, 12, 18, new Bounds(Vector3.zero, Vector3.one));
                TerrainGenerationResult failed = controls.Generator.Generate();
                FieldGenerationResult failure = navigation.Prepare(failed, scatter.gameObject);
                Require(!failure.Success && navigation.State == FieldGenerationState.Failed && !navigation.HasNavigation
                    && failure.EnemyPoints.Count == 0 && failure.ChestPoints.Count == 0, "failed field never ready");

                controls.Generator.ConfigureWfc(catalog, 12, 12, 18, new Bounds(Vector3.zero, Vector3.one * 400f));
                TerrainGenerationResult blocked = controls.Generator.Generate();
                var wall = new GameObject("Blocked field fixture");
                wall.transform.SetParent(scatter.transform, false);
                wall.transform.position = new Vector3(18f, 4f, 18f);
                wall.AddComponent<BoxCollider>().size = new Vector3(100f, 10f, 100f);
                Physics.SyncTransforms();
                failure = navigation.Prepare(blocked, scatter.gameObject);
                Require(!failure.Success && !navigation.HasNavigation, "blocked start rejected after terrain Complete");
                Object.DestroyImmediate(wall);

                controls.Regenerate(false);
                Verify(controls, controls.Generator.LastResult);
                Vector3 oldStart = navigation.LastResult.PlayerPosition;
                navigation.Invalidate();
                Require(!navigation.HasNavigation && navigation.LastResult == null
                    && !NavMesh.SamplePosition(oldStart, out _, 0.25f, NavMesh.AllAreas), "owned NavMesh removed");
                controls.Regenerate(false);
                Verify(controls, controls.Generator.LastResult);

                TerrainIntegrationValidation.Capture(controls, 1, "field-navigation-overview");
                TerrainIntegrationValidation.Capture(controls, 0, "field-navigation-player");
                string report = $"FIELD_NAVIGATION_PASS: 5 seeds x2, {enemyTotal} enemy/{chestTotal} chest candidates; replay, immutable snapshots, single events, all tile/link paths, disjoint slots, reserved chests, failure/blocked start, cleanup/recovery";
                Directory.CreateDirectory("Artifacts");
                File.WriteAllText("Artifacts/field-navigation-results.txt", report);
                Debug.Log(report);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void Verify(TerrainIntegrationControls controls, TerrainGenerationResult terrain)
        {
            FieldNavigation navigation = controls.Navigation;
            FieldGenerationResult result = navigation.LastResult;
            Require(terrain.Success && result != null && result.Success && controls.Player.activeSelf
                && navigation.HasNavigation && navigation.State == FieldGenerationState.Ready,
                "Ready/playable: " + result?.FailureReason);
            Require(result.GenerationId == terrain.GenerationId && result.Seed == terrain.Seed, "same generation");
            var ids = new HashSet<string>();
            var all = result.EnemyPoints.Concat(result.ChestPoints).ToArray();
            var path = new NavMeshPath();
            foreach (FieldSpawnPoint slot in all)
            {
                Require(ids.Add(slot.Id), "unique id");
                Require(NavMesh.CalculatePath(result.PlayerPosition, slot.ApproachPosition, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete, "spawn/approach reachable");
                if (slot.Radius == 0.5f) Require(Vector3.Distance(slot.Position, result.PlayerPosition) >= 5f, "enemy start exclusion");
                else Require(Vector3.Distance(slot.Position, slot.ApproachPosition) <= 2f, "chest approach range");
                foreach (FieldSpawnPoint other in all)
                {
                    if (slot == other) continue;
                    Vector3 delta = slot.Position - other.Position;
                    Require(Mathf.Abs(delta.y) >= 1.8f || new Vector2(delta.x, delta.z).magnitude >= slot.Radius + other.Radius,
                        "disjoint reservations");
                }
            }
        }

        private static string Signature(FieldGenerationResult result)
        {
            return string.Join(";", result.EnemyPoints.Concat(result.ChestPoints)
                .Select(point => point.Id + ":" + point.Position.ToString("F3") + ":" + point.ApproachPosition.ToString("F3")));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("FIELD_NAVIGATION_FAIL: " + message);
        }
    }
}
