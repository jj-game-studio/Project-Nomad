using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class TerrainGenerationValidation
    {
        private static readonly Bounds Area = new Bounds(Vector3.zero, new Vector3(400f, 100f, 400f));
        private const string PlayKey = "Nomad.Terrain.GenerationPlayCheck";

        [MenuItem("Nomad/Terrain/Validate Random Generation")]
        public static void Validate()
        {
            // 별도 임시 씬에서 실행해 열려 있는 사용자 씬을 변경하지 않습니다.
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                ValidateGeneration(100);
                Debug.Log("TERRAIN_GENERATION_PASS: 100 seeds, deterministic replay, heights/rotations, overlap/seam, bounded failure, regeneration/events");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static TerrainPiece[] LoadPieces()
        {
            return new[] { "Flat", "Stairs", "Raised" }.Select(name =>
                AssetDatabase.LoadAssetAtPath<GameObject>($"{TerrainPieceAuthoring.Output}/{name}.prefab")
                    ?.GetComponent<TerrainPiece>()).ToArray();
        }

        private static void ValidateGeneration(int seeds)
        {
            TerrainPiece[] sources = LoadPieces();
            Require(sources.All(p => p != null), "#12 지형 프리팹 필요");
            var host = new GameObject("GenerationValidation");
            var generator = host.AddComponent<TerrainGenerator>();
            int starts = 0;
            int completions = 0;
            generator.GenerationStarted += id =>
            {
                starts++;
                Require(generator.LastResult == null && generator.State == TerrainGenerationState.Generating,
                    "재생성 시작 시 이전 결과 무효화");
            };
            generator.GenerationCompleted += result =>
            {
                completions++;
                Require(ReferenceEquals(result, generator.LastResult), "늦은 구독자용 마지막 결과");
            };

            try
            {
                string previousId = null;
                bool sawHeight = false;
                for (int seed = 0; seed < seeds; seed++)
                {
                    host.transform.rotation = Quaternion.Euler(0f, seed % 4 * 90f, 0f);
                    generator.Configure(sources, 12, seed, Area);
                    TerrainGenerationResult first = generator.Generate();
                    ValidateResult(first, 12);
                    Require(first.GenerationId != previousId, "세대 ID 갱신");
                    previousId = first.GenerationId;
                    string signature = Signature(first);
                    TerrainPiece oldPiece = first.Pieces[0];
                    sawHeight |= first.Pieces.Any(p => Mathf.Abs(p.Connections[0].Position.y) > 0.1f);

                    TerrainGenerationResult second = generator.Generate();
                    ValidateResult(second, 12);
                    Require(signature == Signature(second), "동일 seed와 원점의 재현성");
                    Require(oldPiece == null || !oldPiece.gameObject.activeInHierarchy, "이전 지형 즉시 비활성/정리");
                    Require(first.GenerationId != second.GenerationId, "같은 seed도 새 세대");
                }

                Require(sawHeight, "높낮이 연결 포함");
                host.transform.rotation = Quaternion.identity;
                generator.Configure(sources, 2, 14, new Bounds(Vector3.zero, new Vector3(10.01f, 10f, 10.01f)));
                TerrainGenerationResult failure = generator.Generate();
                Require(!failure.Success && failure.Attempts == 4 && failure.CandidateChecks == 200,
                    "공간 부족: 50회 × (최초 + 재생성3회) 종료");
                Require(failure.Pieces.Count == 0 && failure.Links.Count == 0
                    && !string.IsNullOrEmpty(failure.FailureReason), "실패한 부분 필드 비공개");
                Require(generator.State == TerrainGenerationState.Failed, "실패 상태");

                generator.Configure(null, 0, 14, Area);
                failure = generator.Generate();
                Require(!failure.Success && failure.Attempts == 0, "잘못된 설정은 생성 전 실패");
                generator.Configure(sources, 1, 14, Area);
                ValidateResult(generator.Generate(), 1);
                Require(starts == completions && starts == seeds * 2 + 3, "세대당 완료/실패 통지 한 번");

                ValidateTurns(generator);
                ValidateOverlap(sources);
            }
            finally
            {
                Dispose(host);
            }
        }

        private static void ValidateResult(TerrainGenerationResult result, int count)
        {
            Require(result.Success && result.Pieces.Count == count && result.Links.Count == count - 1,
                $"seed={result.Seed} 조각/연결 수: {result.FailureReason}");
            Require(result.Pieces.All(p => p.gameObject.activeInHierarchy), "성공 지형 활성화");
            Require(result.Links.All(link => link.From.Matches(link.To)), "연결 위치·방향·높이·규격");

            for (int i = 0; i < result.Pieces.Count; i++)
            {
                for (int j = i + 1; j < result.Pieces.Count; j++)
                    Require(!TerrainPlacement.Overlaps(result.Pieces[i], result.Pieces[j]), "생성 필드 내부 겹침 없음");
            }
        }

        private static string Signature(TerrainGenerationResult result)
        {
            return string.Join(";", result.Pieces.Select(p =>
                $"{p.Kind}:{p.transform.position:F3}:{p.transform.eulerAngles:F3}"));
        }

        private static void ValidateTurns(TerrainGenerator generator)
        {
            // 기존 프리팹은 남북 연결뿐이므로 직교 회전·분기는 합성 조각으로 검증합니다.
            TerrainPiece corner = CreateSynthetic(new[] { Vector3.back, Vector3.right });
            TerrainPiece junction = CreateSynthetic(new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left });
            try
            {
                generator.Configure(new[] { corner, junction }, 20, 23, Area);
                TerrainGenerationResult result = generator.Generate();
                ValidateResult(result, 20);
                Require(result.Pieces.Select(p => Mathf.RoundToInt(p.transform.eulerAngles.y)).Distinct().Count() > 1,
                    "90도 회전 배치");

                generator.Configure(new[] { corner }, 100, 1,
                    new Bounds(Vector3.zero, new Vector3(20.01f, 20f, 20.01f)));
                result = generator.Generate();
                Require(!result.Success && result.Attempts == 4 && result.CandidateChecks <= 50 * 99 * 4,
                    "닫힌 경로/후보 고갈 유한 종료");
            }
            finally
            {
                Dispose(corner.gameObject);
                Dispose(junction.gameObject);
            }
        }

        private static TerrainPiece CreateSynthetic(Vector3[] directions)
        {
            var root = new GameObject("SyntheticPiece");
            var points = directions.Select(direction =>
            {
                var point = new GameObject("Port");
                point.transform.SetParent(root.transform, false);
                point.transform.localPosition = direction * 5f;
                point.transform.localRotation = Quaternion.LookRotation(direction);
                return point.AddComponent<TerrainConnection>();
            }).ToArray();

            var piece = root.AddComponent<TerrainPiece>();
            piece.Configure(TerrainPieceKind.Flat,
                new Bounds(Vector3.down * 0.125f, new Vector3(10f, 0.25f, 10f)), points);
            return piece;
        }

        private static void ValidateOverlap(TerrainPiece[] sources)
        {
            TerrainPiece a = Object.Instantiate(sources[0]);
            TerrainPiece b = Object.Instantiate(sources[0]);
            try
            {
                a.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                TerrainPlacement.Align(b, b.Connections[0], a.Connections[1]);
                Require(a.Connections[1].Matches(b.Connections[0]), "경계 정렬");
                Require(!TerrainPlacement.Overlaps(a, b), "경계 접촉 허용");
                b.transform.position -= Vector3.forward * 0.005f;
                Require(TerrainPlacement.Overlaps(a, b, a.Connections[1], b.Connections[0]),
                    "전체 폭의 5mm 내부 겹침은 좁은 연결부로 면제하지 않음");
                b.transform.position = a.transform.position;
                Require(TerrainPlacement.Overlaps(a, b), "동일 위치 거절");

                // 연결부 안에만 존재하는 작은 점유 영역은 명시된 예외를 허용합니다.
                a.Configure(a.Kind, new Bounds(new Vector3(0f, 1f, 5f), new Vector3(1f, 1f, 0.005f)),
                    a.Connections.ToArray());
                b.transform.position = Vector3.forward * 10f;
                b.Configure(b.Kind, new Bounds(new Vector3(0f, 1f, -5f), new Vector3(1f, 1f, 0.005f)),
                    b.Connections.ToArray());
                Require(TerrainPlacement.Overlaps(a, b), "연결 지정 없는 관통 거절");
                Require(!TerrainPlacement.Overlaps(a, b, a.Connections[1], b.Connections[0]),
                    "양쪽 연결부 내부만 예외 허용");
            }
            finally
            {
                Dispose(a.gameObject);
                Dispose(b.gameObject);
            }
        }

        [MenuItem("Nomad/Terrain/Open Random Generation Preview")]
        public static void OpenPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var host = new GameObject("B_RandomTerrain");
            var generator = host.AddComponent<TerrainGenerator>();
            generator.Configure(LoadPieces(), 12, 14, Area);
            var settings = new SerializedObject(generator);
            settings.FindProperty("generateOnStart").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            generator.Generate();

            Camera camera = Camera.main;
            camera.transform.position = new Vector3(70f, 100f, -80f);
            camera.transform.LookAt(Vector3.zero);
            Selection.activeGameObject = host;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, new Vector3(130f, 20f, 130f)), false);
            Debug.Log("지형 전용 미저장 미리보기입니다. Inspector의 seed/조각 수 변경 후 Play로 재생성합니다. 최종 Ready는 제공하지 않습니다.");
        }

        public static void VerifyPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetInt(PlayKey, 1);
            SessionState.SetFloat(PlayKey + "Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update += CheckPlayMode;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void ResumePlayMode()
        {
            if (SessionState.GetInt(PlayKey, 0) != 0)
                EditorApplication.update += CheckPlayMode;
        }

        private static void CheckPlayMode()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(PlayKey + "Start", 0f) > 120f)
                    throw new TimeoutException("Play 검증 시간 초과");

                if (!Application.isPlaying || Time.frameCount < 3)
                    return;

                if (SessionState.GetInt(PlayKey, 0) == 1)
                {
                    ValidateGeneration(3);
                    SessionState.SetInt(PlayKey, Time.frameCount + 2);
                    return;
                }

                if (Time.frameCount < SessionState.GetInt(PlayKey, 0))
                    return;

                Require(!Object.FindObjectsByType<TerrainPiece>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any(), "프레임 경과 후 지연 삭제 완료");
                Debug.Log($"TERRAIN_GENERATION_PLAYMODE_PASS: frame={Time.frameCount}, time={Time.time:F2}, deferred cleanup verified");
                FinishPlayMode(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FinishPlayMode(1);
            }
        }

        private static void FinishPlayMode(int code)
        {
            SessionState.SetInt(PlayKey, 0);
            EditorApplication.update -= CheckPlayMode;
            EditorApplication.Exit(code);
        }

        private static void Dispose(GameObject value)
        {
            value.SetActive(false);
            if (Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("TERRAIN_GENERATION_FAIL: " + message);
        }
    }
}
