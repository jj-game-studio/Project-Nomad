using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class StairJunctionValidation
    {
        private static readonly string[] Names = { "StairsCorner", "StairsTee", "StairsCross" };

        [MenuItem("Nomad/Terrain/Validate Stair Junction Pieces")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            SimulationMode previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                for (int index = 0; index < Names.Length; index++)
                {
                    var piece = Object.Instantiate(Load(index));
                    SceneManager.MoveGameObjectToScene(piece.gameObject, scene);
                    Require(piece.Connections.Count == index + 2, "계단 연결점 수");
                    Bounds landing = piece.transform.Find("Visual/CentralLanding").GetComponentInChildren<Renderer>().bounds;
                    Require(Vector3.Distance(landing.center, new Vector3(0f, 1.875f, 0f)) < 0.01f
                        && Vector3.Distance(landing.size, new Vector3(4f, 0.25f, 4f)) < 0.01f,
                        "중앙 참 외형과 충돌체 일치");
                    Physics.SyncTransforms();
                    PhysicsScene physics = scene.GetPhysicsScene();

                    foreach (TerrainConnection point in piece.Connections)
                    {
                        Require(Mathf.Abs(point.Position.y) < 0.001f, "바깥 연결 바닥 높이 0m");
                        ValidateSurface(physics, point.Outward);
                        ValidateTraversal(scene, physics, point.Outward);
                    }

                    Object.DestroyImmediate(piece.gameObject);
                }

                Debug.Log("TERRAIN_STAIR_JUNCTION_PASS: 3 real prefabs, 9 arms; 2m center, surfaces/clearance/steps, capsule round trips");
            }
            finally
            {
                Physics.simulationMode = previousMode;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ValidateSurface(PhysicsScene physics, Vector3 outward)
        {
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            foreach (float lane in new[] { -0.99f, 0f, 0.99f })
            {
                float previousHeight = 0f;
                for (int sample = 0; sample <= 99; sample++)
                {
                    Vector3 position = outward * (4.95f - sample * 0.05f) + side * lane;
                    Require(physics.Raycast(position + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 5f),
                        "2m 통로 바닥 연속성");
                    Require(hit.point.y - previousHeight <= 0.251f && hit.point.y >= -0.01f,
                        "계단 단 높이 0.25m 이하");
                    var overlaps = new Collider[4];
                    Require(physics.OverlapCapsule(hit.point + Vector3.up * 0.65f,
                        hit.point + Vector3.up * 1.85f, 0.34f, overlaps, ~0,
                        QueryTriggerInteraction.Ignore) == 0, "머리 위 여유");
                    previousHeight = hit.point.y;
                }

                Require(Mathf.Abs(previousHeight - 2f) < 0.01f, "중앙 참 높이 2m");
            }
        }

        private static void ValidateTraversal(Scene scene, PhysicsScene physics, Vector3 outward)
        {
            var probe = new GameObject("JunctionTraversal");
            SceneManager.MoveGameObjectToScene(probe, scene);
            var controller = probe.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.02f;

            try
            {
                Vector3 start = outward * 4.5f;
                Require(physics.Raycast(start + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 5f), "왕복 시작 바닥");
                probe.transform.position = hit.point + Vector3.up * 0.03f;
                Physics.SyncTransforms();

                for (int leg = 0; leg < 2; leg++)
                {
                    Vector3 target = leg == 0 ? Vector3.zero : start;
                    float velocity = -2f;
                    bool reached = false;
                    for (int frame = 0; frame < 300; frame++)
                    {
                        Vector3 delta = target - probe.transform.position;
                        delta.y = 0f;
                        if (delta.magnitude < 0.08f)
                        {
                            reached = true;
                            break;
                        }

                        velocity = controller.isGrounded ? -2f : velocity - 20f / 60f;
                        controller.Move(Vector3.ClampMagnitude(delta, 5f / 60f)
                            + Vector3.up * (velocity / 60f));
                        physics.Simulate(1f / 60f);
                    }

                    Require(reached, "각 계단 팔의 캡슐 왕복 이동");
                    Require(leg != 0 || probe.transform.position.y > 1.9f, "계단으로 중앙 참 도달");
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static TerrainPiece Load(int index)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{TerrainPieceAuthoring.Output}/{Names[index]}.prefab").GetComponent<TerrainPiece>();
        }

        public static void CaptureOverview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            for (int index = 0; index < Names.Length; index++)
            {
                TerrainPiece piece = Object.Instantiate(Load(index));
                piece.transform.position = Vector3.right * ((index - 1) * 14f);
            }

            Camera camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = 19f;
            camera.transform.position = new Vector3(0f, 30f, -25f);
            camera.transform.LookAt(Vector3.up);
            var target = new RenderTexture(1280, 900, 24);
            var texture = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Artifacts");
                File.WriteAllBytes("Artifacts/stair-junctions.png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("TERRAIN_STAIR_JUNCTION_FAIL: " + message);
        }
    }
}
