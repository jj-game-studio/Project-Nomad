using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class ModularTileValidation
    {
        [MenuItem("Nomad/Terrain/Modular/Validate All Combinations")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            SimulationMode previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            int combinations = 0;
            int traversals = 0;
            try
            {
                foreach (string name in ModularTileAuthoring.Names)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModularTileAuthoring.Output}/{name}.prefab");
                    Require(prefab != null, "saved prefab " + name);
                    GameObject tile = Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(tile, scene);
                    TerrainPiece piece = tile.GetComponent<TerrainPiece>();
                    ModularTerrainTile modular = tile.GetComponent<ModularTerrainTile>();
                    PhysicsScene physics = scene.GetPhysicsScene();
                    int count = modular == null ? 1 : modular.CombinationCount;
                    Require(count == (name == "ModularCross" ? 81 : name == "ModularTee" ? 27 : 1), "combination count");

                    for (int turn = 0; turn < 4; turn++)
                    {
                        tile.transform.rotation = Quaternion.Euler(0f, turn * 90f, 0f);
                        for (int value = 0; value < count; value++)
                        {
                            modular?.ApplyCombination(value);
                            Physics.SyncTransforms();
                            ValidateRegions(tile, modular, physics);
                            foreach (TerrainConnection port in piece.Connections)
                            {
                                Require(port.gameObject.activeInHierarchy, "active connection only");
                                Require(physics.Raycast(port.Position - port.Outward * 0.05f + Vector3.up * 4f,
                                    Vector3.down, out RaycastHit hit, 5f), "port floor support");
                                Require(hit.point.y >= port.Position.y - 0.01f && hit.point.y <= port.Position.y + 0.251f,
                                    "port surface height");
                            }

                            int expectedPorts = name == "WideStairs" ? 6 : modular.RegionCount;
                            if (modular != null)
                            {
                                for (int region = 0; region < modular.RegionCount; region++)
                                    if (modular.GetState(region) != TerrainFloorState.None)
                                        expectedPorts += name == "ModularTee" && region == 0 ? 3 : 2;
                            }
                            Require(piece.Connections.Count == expectedPorts, "all active region ports registered");
                            combinations++;
                            // 전부 없음/전부 낮음/전부 높음에서 모든 연결점과 계단 팔을 왕복합니다.
                            if (value != 0 && value != (count - 1) / 2 && value != count - 1) continue;
                            foreach (TerrainConnection port in piece.Connections)
                            {
                                Traverse(scene, physics, port);
                                traversals++;
                            }
                        }
                    }

                    if (modular != null)
                    {
                        bool rejected = false;
                        try { modular.ApplyCombination(count); }
                        catch (ArgumentOutOfRangeException) { rejected = true; }
                        Require(rejected, "invalid combination rejected");
                        modular.ApplyCombination(0);
                        Require(piece.Connections.All(p => p.gameObject.activeInHierarchy), "reset removes optional ports");
                    }
                    Object.DestroyImmediate(tile);
                }

                Directory.CreateDirectory("Artifacts");
                string report = $"MODULAR_TILE_PASS: {combinations} rotated states (108+wide x4), {traversals} capsule round trips; floor state/height, absent collision, renderer/port activation, invalid/reset, wide full width";
                File.WriteAllText("Artifacts/modular-tiles-results.txt", report);
                Debug.Log(report);
            }
            finally
            {
                Physics.simulationMode = previous;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ValidateRegions(GameObject tile, ModularTerrainTile modular, PhysicsScene physics)
        {
            if (modular == null)
            {
                foreach (float lane in new[] { -4.9f, 0f, 4.9f })
                {
                    for (float z = -4.9f; z < 5f; z += 0.1f)
                    {
                        Vector3 point = tile.transform.TransformPoint(new Vector3(lane, 4f, z));
                        Require(physics.Raycast(point, Vector3.down, out _, 5f), "wide stair whole width");
                    }
                }
                return;
            }

            Vector3[] centers = modular.RegionCount == 4 ? ModularTileAuthoring.CrossCenters : ModularTileAuthoring.TeeCenters;
            for (int index = 0; index < modular.RegionCount; index++)
            {
                TerrainFloorState state = modular.GetState(index);
                foreach (float dx in new[] { -1.4f, 0f, 1.4f })
                foreach (float dz in new[] { -1.4f, 0f, 1.4f })
                {
                    Vector3 point = tile.transform.TransformPoint(centers[index] + new Vector3(dx, 4f, dz));
                    bool found = physics.Raycast(point, Vector3.down, out RaycastHit hit, 5f);
                    Require(found == (state != TerrainFloorState.None), "none/low/high collider state");
                    if (found) Require(Mathf.Abs(hit.point.y - (state == TerrainFloorState.Low ? 0f : 2f)) < 0.001f, "region height");
                }

                Require(tile.transform.Find($"Region_{index}_Low").gameObject.activeSelf == (state == TerrainFloorState.Low), "low visual active");
                Require(tile.transform.Find($"Region_{index}_High").gameObject.activeSelf == (state == TerrainFloorState.High), "high visual active");
            }
        }

        private static void Traverse(Scene scene, PhysicsScene physics, TerrainConnection port)
        {
            GameObject pad = ModularTilePreview.AddNeighbour(port);
            SceneManager.MoveGameObjectToScene(pad, scene);
            var probe = new GameObject("CapsuleProbe");
            SceneManager.MoveGameObjectToScene(probe, scene);
            var body = probe.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = Vector3.up * 0.9f;
            body.stepOffset = 0.3f;
            body.skinWidth = 0.02f;
            body.slopeLimit = 45f;
            Vector3 start = port.Position + port.Outward * 1.1f;
            try
            {
                probe.transform.position = start + Vector3.up * 0.03f;
                Physics.SyncTransforms();
                for (int leg = 0; leg < 2; leg++)
                {
                    Vector3 target = leg == 0 ? port.Position - port.Outward * 1.1f : start;
                    TerrainPiece piece = port.GetComponentInParent<TerrainPiece>();
                    if (leg == 0 && port.transform.parent == piece.transform)
                    {
                        target = piece.transform.position;
                        if (piece.GetComponent<ModularTerrainTile>() == null)
                            target = port.Position - port.Outward * 9.4f;
                    }
                    bool reached = false;
                    float verticalSpeed = -2f;
                    for (int frame = 0; frame < 180; frame++)
                    {
                        Vector3 delta = target - probe.transform.position;
                        delta.y = 0f;
                        if (delta.magnitude < 0.05f) { reached = true; break; }
                        verticalSpeed = body.isGrounded ? -2f : verticalSpeed - 20f / 60f;
                        body.Move(Vector3.ClampMagnitude(delta, 5f / 60f) + Vector3.up * (verticalSpeed / 60f));
                        physics.Simulate(1f / 60f);
                    }

                    for (int frame = 0; frame < 40; frame++)
                    {
                        verticalSpeed = body.isGrounded ? -2f : verticalSpeed - 20f / 60f;
                        body.Move(Vector3.up * (verticalSpeed / 60f));
                        physics.Simulate(1f / 60f);
                    }
                    Require(reached && probe.transform.position.y > -0.1f, "capsule round trip " + port.Position);
                    Require(physics.Raycast(probe.transform.position + Vector3.up * 0.1f, Vector3.down,
                        out RaycastHit support, 0.2f) && support.collider != body, "capsule settled on floor");
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
                Object.DestroyImmediate(pad);
            }
        }

        public static void Run()
        {
            try
            {
                ModularTileAuthoring.Create();
                Validate();
                ModularTilePreview.Cross();
                Capture("modular-cross");
                ModularTilePreview.Tee();
                Capture("modular-tee");
                ModularTilePreview.Wide();
                Capture("modular-wide");
                ModularTilePreview.Gallery();
                Capture("modular-gallery", true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(string name, bool gallery = false)
        {
            Camera camera = Camera.main;
            if (!gallery) camera.transform.position = new Vector3(15f, 19f, -18f);
            if (!gallery) camera.transform.LookAt(Vector3.zero);
            var target = new RenderTexture(1000, 800, 24);
            var pixels = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0);
                pixels.Apply();
                File.WriteAllBytes($"Artifacts/{name}.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("MODULAR_TILE_FAIL: " + message);
        }
    }
}
