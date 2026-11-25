using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Nomad.Terrain.Editor
{
    public static class FieldNavigationPlayVerification
    {
        private const string SessionKey = "Nomad.Terrain.FieldNavigationPlay";
        private static double deadline;
        private static TerrainIntegrationControls controls;
        private static NavMeshAgent agent;
        private static Vector3 origin;
        private static int initialFrame;
        private static int stage;
        private static string previousId;

        public static void Run()
        {
            EditorSceneManager.OpenScene(TerrainIntegrationAuthoring.ScenePath);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "timeout stage=" + stage);
                if (!Application.isPlaying || Time.frameCount < 5) return;
                if (controls == null)
                {
                    Application.runInBackground = true;
                    Time.captureFramerate = 60;
                    controls = Object.FindAnyObjectByType<TerrainIntegrationControls>();
                    FieldGenerationResult result = controls.Navigation.LastResult;
                    Require(result.Success && controls.Navigation.HasNavigation, "persistent navigation in Play");
                    origin = result.PlayerPosition;
                    previousId = result.GenerationId;
                    initialFrame = Time.frameCount;
                    var walker = new GameObject("Navigation verification agent");
                    walker.transform.position = origin;
                    agent = walker.AddComponent<NavMeshAgent>();
                    agent.radius = 0.35f;
                    agent.height = 1.8f;
                    agent.speed = 12f;
                    agent.acceleration = 60f;
                    agent.stoppingDistance = 0.1f;
                    FieldSpawnPoint target = result.EnemyPoints.OrderByDescending(point => point.Position.y)
                        .ThenByDescending(point => Vector3.Distance(point.Position, origin)).First();
                    Require(agent.isOnNavMesh && agent.SetDestination(target.Position), "agent starts on mesh");
                    controls.Navigation.GenerationStarted += _ =>
                    {
                        if (agent != null) agent.gameObject.SetActive(false);
                    };
                    return;
                }
                if (stage == 0)
                {
                    if (Time.frameCount < initialFrame + 10 || agent.pathPending) return;
                    Require(agent.pathStatus == NavMeshPathStatus.PathComplete, "agent complete path");
                    if (agent.remainingDistance > 0.2f) return;
                    Require(Vector3.Distance(agent.transform.position, origin) > 5f, "agent actually moved");
                    Debug.Log($"FIELD_AGENT_REACHED frames={Time.frameCount - initialFrame} from={origin} to={agent.transform.position}");
                    agent.gameObject.SetActive(false);
                    Object.Destroy(agent.gameObject);
                    controls.Regenerate(false);
                    Require(controls.Navigation.LastResult.GenerationId != previousId
                        && controls.Navigation.State == FieldGenerationState.Ready, "mesh replacement in Play");
                    initialFrame = Time.frameCount;
                    stage++;
                    return;
                }
                if (Time.frameCount < initialFrame + 10) return;
                if (stage == 1)
                {
                    controls.Navigation.enabled = false;
                    Require(!controls.Navigation.HasNavigation && controls.Navigation.LastResult == null, "disable cleanup");
                    controls.Navigation.enabled = true;
                    bool staleRejected = false;
                    try { controls.Navigation.Prepare(controls.Generator.LastResult, null); }
                    catch (InvalidOperationException) { staleRejected = true; }
                    Require(staleRejected, "stale generation rejected after re-enable");
                    controls.Regenerate(false);
                    initialFrame = Time.frameCount;
                    stage++;
                    return;
                }
                Require(controls.Player.activeSelf && controls.Navigation.HasNavigation, "new generation remains playable");
                FieldDressing dressing = controls.Generator.GetComponent<FieldDressing>();
                Require(dressing.Structures > 0, "generated buildings remain present");
                foreach (Rect area in dressing.StructureAreas)
                    Require(controls.Navigation.LastResult.ChestPoints.Any(point => point.Id.StartsWith("building-") &&
                        area.Contains(new Vector2(point.Position.x, point.Position.z))), "mandatory chest in every building");
                TerrainIntegrationValidation.Capture(controls, 1, "field-navigation-play-overview");
                TerrainIntegrationValidation.Capture(controls, 0, "field-navigation-play-player");
                Rect room = dressing.StructureAreas[0];
                TerrainIntegrationValidation.Capture(controls, 2, "issue210-building", new Vector3(room.center.x, 0, room.center.y));
                Debug.Log($"BUILDING_CHESTS_PASS buildings={dressing.Structures} reserved={controls.Navigation.LastResult.ChestPoints.Count}");
                string report = $"FIELD_NAVIGATION_PLAY_PASS frame={Time.frameCount} time={Time.time:F2}: real agent reached destination, complete path, regeneration/replacement, active player, persistent mesh, disable/re-enable/stale rejection";
                File.WriteAllText("Artifacts/field-navigation-play-results.txt", report);
                Debug.Log(report);
                Finish(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("FIELD_NAVIGATION_PLAY_FAIL: " + message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}
