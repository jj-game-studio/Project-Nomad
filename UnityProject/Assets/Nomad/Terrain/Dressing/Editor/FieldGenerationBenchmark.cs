using System.Diagnostics;
using System.Reflection;
using System.Text;
using Nomad.Props;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    // 26×26 필드(TerrainTest)를 같은 seed 묶음으로 다시 생성하며 소요 시간과 결과 요약을 로그로 남깁니다.
    // 배치 실행: -executeMethod Nomad.Terrain.Editor.FieldGenerationBenchmark.Run
    public static class FieldGenerationBenchmark
    {
        private const string SessionKey = "Nomad.Terrain.GenerationBenchmark";
        private static readonly int[] Seeds = { 51, 1001, 2024, 31337, 777 };
        private static double deadline;
        private static int frame;

        [MenuItem("Nomad/Terrain/Field Generation Benchmark")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(FieldDressingAuthoring.ScenePath);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 900;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish("FIELD_BENCH_FAIL timeout", 1); return; }
            if (!Application.isPlaying || Time.frameCount < 5 || Time.frameCount == frame) return;
            frame = Time.frameCount;
            EditorApplication.update -= Update;

            int code = 0;
            var report = new StringBuilder("FIELD_BENCH_RESULT\n");
            var controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
            FieldInfo seedField = typeof(TerrainIntegrationControls).GetField("seed", BindingFlags.NonPublic | BindingFlags.Instance);
            double total = 0;

            // 첫 회는 셰이더·JIT 준비가 섞이므로 측정에서 뺍니다.
            seedField.SetValue(controls, Seeds[0]);
            controls.Regenerate(false);

            foreach (int seed in Seeds)
            {
                seedField.SetValue(controls, seed);
                MicroTerrainWalkability.ResetStats();
                var watch = Stopwatch.StartNew();
                TerrainGenerationResult result = controls.Regenerate(false);
                double seconds = watch.Elapsed.TotalSeconds;
                total += seconds;

                FieldGenerationResult field = controls.Navigation.LastResult;
                var dressing = Object.FindAnyObjectByType<FieldDressing>();
                var scatter = Object.FindAnyObjectByType<PropScatter>();
                if (field == null || !field.Success) code = 1;
                report.AppendLine($"seed={seed} total={seconds:F2}s builds={MicroTerrainWalkability.BuildCount}+{MicroTerrainWalkability.UpdateCount} "
                    + $"build={MicroTerrainWalkability.BuildSeconds:F2}s pathRuns={MicroTerrainWalkability.PathChecks} "
                    + $"path={MicroTerrainWalkability.PathSeconds:F2}s attempts={result.Attempts} ready={field?.Success} "
                    + $"| blockers={dressing?.Blockers} removed={dressing?.RemovedBlockers} props={scatter?.Positions.Count} "
                    + $"enemy={field?.EnemyPoints.Count} chest={field?.ChestPoints.Count} exit={field?.ExitPoints.Count} "
                    + $"player={field?.PlayerPosition:F2} {field?.FailureReason}");
            }

            report.AppendLine($"average={total / Seeds.Length:F2}s");
            Finish(report.ToString(), code);
        }

        private static void Finish(string message, int code)
        {
            EditorApplication.update -= Update;
            SessionState.SetBool(SessionKey, false);
            Debug.Log(message);
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}
