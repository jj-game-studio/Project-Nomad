using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class FullWidthStairValidation
    {
        public static void Validate()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            SimulationMode previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            int traversals = 0;
            try
            {
                foreach (string name in FullWidthStairAuthoring.Names)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{WfcCatalogAuthoring.Folder}/{name}.prefab");
                    var tile = Object.Instantiate(prefab).GetComponent<TerrainPiece>();
                    SceneManager.MoveGameObjectToScene(tile.gameObject, scene);
                    var shape = tile.GetComponent<FullWidthStairTile>();
                    for (int turn = 0; turn < 4; turn++)
                    {
                        tile.transform.rotation = Quaternion.Euler(0f, turn * 90f, 0f);
                        Physics.SyncTransforms();
                        TerrainIntegrationValidation.ValidateSingleSurface(tile);
                        for (int z = 0; z < 20; z++)
                        for (int x = 0; x < 20; x++)
                            Require(TerrainSurfaceChecks.Height(tile, tile.transform.position
                                + new Vector3(-4.75f + x * 0.5f, 0f, -4.75f + z * 0.5f)).HasValue, "full tile coverage");
                        Collider plateau = tile.transform.Find("Plateau").GetComponent<Collider>();
                        Require(Mathf.Abs(plateau.bounds.max.y - (shape.Inverted ? 0f : 2f)) < 0.001f, "red rectangle flat height");
                        foreach (TerrainConnection port in tile.Connections)
                        {
                            GameObject pad = ModularTilePreview.AddNeighbour(port);
                            SceneManager.MoveGameObjectToScene(pad, scene);
                            try
                            {
                                Traverse(scene, scene.GetPhysicsScene(), tile, port.Position + port.Outward * 1.2f);
                                traversals++;
                            }
                            finally
                            {
                                Object.DestroyImmediate(pad);
                            }
                        }
                        Vector3 corner = shape.Layout == FullWidthStairLayout.U ? new Vector3(-4.6f, 0f, -4.6f) : new Vector3(-4.6f, 0f, 4.6f);
                        Traverse(scene, scene.GetPhysicsScene(), tile, tile.transform.TransformPoint(corner));
                        traversals++;
                    }
                    Object.DestroyImmediate(tile.gameObject);
                }
                Debug.Log($"FULL_WIDTH_STAIRS_PASS: U/L high/low x4 rotations, full coverage/single surface, {traversals} capsule round trips including diagonal corners");
            }
            finally
            {
                Physics.simulationMode = previous;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Traverse(Scene scene, PhysicsScene physics, TerrainPiece tile, Vector3 start)
        {
            var probe = new GameObject("FullWidthTraversal");
            SceneManager.MoveGameObjectToScene(probe, scene);
            var controller = probe.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.02f;
            controller.slopeLimit = 45f;
            try
            {
                Require(physics.Raycast(start + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 5f), "start support");
                probe.transform.position = hit.point + Vector3.up * 0.03f;
                Physics.SyncTransforms();
                for (int leg = 0; leg < 2; leg++)
                {
                    Vector3 target = leg == 0 ? tile.transform.position : start;
                    bool reached = false;
                    float velocity = -2f;
                    for (int frame = 0; frame < 360; frame++)
                    {
                        Vector3 delta = target - probe.transform.position;
                        delta.y = 0f;
                        if (delta.magnitude < 0.06f) { reached = true; break; }
                        velocity = controller.isGrounded ? -2f : velocity - 20f / 60f;
                        controller.Move(Vector3.ClampMagnitude(delta, 5f / 60f) + Vector3.up * (velocity / 60f));
                        physics.Simulate(1f / 60f);
                    }
                    Require(reached && probe.transform.position.y > -0.1f, "stair/diagonal round trip " + tile.name + " from " + start);
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("FULL_WIDTH_STAIRS_FAIL: " + message);
        }
    }
}
