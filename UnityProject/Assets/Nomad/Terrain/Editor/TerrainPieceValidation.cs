using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nomad.Terrain.Editor
{
    public static class TerrainPieceValidation
    {
        [MenuItem("Nomad/Terrain/Validate Saved B Scene")]
        public static void Validate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(TerrainPieceAuthoring.Output + "/B_TerrainPieces.unity");
            var pieces = UnityEngine.Object.FindObjectsByType<TerrainPiece>()
                .OrderBy(p => p.transform.position.z).ToArray();
            Require(pieces.Length == 3, "지형 조각 수");
            Require(pieces[0].Connections[1].Matches(pieces[1].Connections[0]), "하단 연결");
            Require(pieces[1].Connections[1].Matches(pieces[2].Connections[0]), "상단 연결");

            foreach (var piece in pieces)
            {
                Require(piece.transform.lossyScale == Vector3.one, "루트 배율");
                Require(piece.LocalOccupancy.size.x == 10f && piece.LocalOccupancy.size.z == 10f, "10m 점유 영역");
                Require(piece.Connections.Count == 2, "연결점 수");
                Require(piece.GetComponentsInChildren<Collider>().All(c => c.transform.IsChildOf(piece.transform.Find("Collision"))),
                    "외형/충돌 분리");
            }

            // 경계 접촉, 실제 내부 겹침, 연결 위치/방향 오류를 각각 확인합니다.
            Require(!InteriorOverlap(pieces[0].WorldOccupancy, pieces[1].WorldOccupancy), "경계 접촉 허용");
            Vector3 saved = pieces[1].transform.position;
            pieces[1].transform.position -= Vector3.forward;
            Require(InteriorOverlap(pieces[0].WorldOccupancy, pieces[1].WorldOccupancy), "내부 겹침 감지");
            Require(!pieces[0].Connections[1].Matches(pieces[1].Connections[0]), "위치 오류 거절");
            pieces[1].transform.position = saved;

            Transform point = pieces[1].Connections[0].transform;
            Quaternion rotation = point.rotation;
            point.Rotate(0f, 1f, 0f);
            Require(!pieces[0].Connections[1].Matches(pieces[1].Connections[0]), "방향 오류 거절");
            point.rotation = rotation;

            var controller = UnityEngine.Object.FindAnyObjectByType<TerrainTraversalProbe>().GetComponent<CharacterController>();
            controller.enabled = false;
            Physics.SyncTransforms();
            ValidateSurfaces();
            controller.enabled = true;
            Physics.SyncTransforms();
            ValidateTraversal();
            CaptureOverview();
            // 검증 중 생성한 오브젝트나 변경을 씬에 저장하지 않습니다.
            Debug.Log("TERRAIN_VALIDATION_PASS: 규격/연결/겹침/2m 통로/계단 단차/캐릭터 왕복");
        }

        public static void VerifyPlayMode()
        {
            EditorSceneManager.OpenScene(TerrainPieceAuthoring.Output + "/B_TerrainPieces.unity");
            SessionState.SetBool("Nomad.Terrain.VerifyPlayMode", true);
            SessionState.SetFloat("Nomad.Terrain.PlayModeStart", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update += CheckPlayMode;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void ResumePlayModeVerification()
        {
            if (SessionState.GetBool("Nomad.Terrain.VerifyPlayMode", false))
                EditorApplication.update += CheckPlayMode;
        }

        private static void CheckPlayMode()
        {
            if (!SessionState.GetBool("Nomad.Terrain.VerifyPlayMode", false))
                return;

            var probe = Application.isPlaying
                ? UnityEngine.Object.FindAnyObjectByType<TerrainTraversalProbe>() : null;
            bool timedOut = EditorApplication.timeSinceStartup
                - SessionState.GetFloat("Nomad.Terrain.PlayModeStart", 0f) > 60f;

            if (probe != null && probe.Complete)
            {
                Debug.Log($"TERRAIN_PLAYMODE_PASS: frame={Time.frameCount}, time={Time.time:F2}");
                FinishPlayModeVerification(0);
            }
            else if (timedOut || (probe != null && probe.Failed))
            {
                Debug.LogError("TERRAIN_PLAYMODE_FAIL");
                FinishPlayModeVerification(1);
            }
        }

        private static void FinishPlayModeVerification(int exitCode)
        {
            SessionState.SetBool("Nomad.Terrain.VerifyPlayMode", false);
            EditorApplication.update -= CheckPlayMode;
            EditorApplication.Exit(exitCode);
        }

        private static bool InteriorOverlap(Bounds a, Bounds b)
        {
            Vector3 overlap = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);
            return overlap.x > 0.01f && overlap.y > 0.01f && overlap.z > 0.01f;
        }

        private static void ValidateSurfaces()
        {
            foreach (float x in new[] { -0.99f, 0f, 0.99f })
            {
                float previous = 0f;
                for (int i = 0; i <= 520; i++)
                {
                    float z = -3f + i * 0.05f;
                    Require(Physics.Raycast(new Vector3(x, 5f, z), Vector3.down, out var hit, 6f,
                        ~0, QueryTriggerInteraction.Ignore), $"바닥 누락 x={x}, z={z}");

                    Require(hit.point.y >= -0.01f && hit.point.y <= 2.01f, "바닥 높이");
                    Require(hit.point.y - previous <= 0.251f, $"단차 초과 z={z}");
                    Require(!Physics.CheckCapsule(hit.point + Vector3.up * 0.65f,
                        hit.point + Vector3.up * 1.85f, 0.34f, ~0, QueryTriggerInteraction.Ignore),
                        $"머리 위 여유 z={z}");
                    previous = hit.point.y;
                }
            }
        }

        private static void ValidateTraversal()
        {
            var probe = UnityEngine.Object.FindAnyObjectByType<TerrainTraversalProbe>();
            Require(probe != null, "왕복 검증 캐릭터");
            SimulationMode previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                for (int i = 0; i < 1201 && !probe.Complete && !probe.Failed; i++)
                {
                    probe.Step(1f / 60f);
                    Physics.Simulate(1f / 60f);
                }

                Require(probe.Complete && !probe.Failed, $"캐릭터 왕복 {probe.transform.position}");
            }
            finally
            {
                Physics.simulationMode = previous;
            }
        }

        private static void CaptureOverview()
        {
            Directory.CreateDirectory("Artifacts");
            Camera camera = Camera.main;
            var target = new RenderTexture(1280, 900, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            var texture = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes("Artifacts/terrain-overview.png", texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("TERRAIN_VALIDATION_FAIL: " + message);
        }
    }
}
