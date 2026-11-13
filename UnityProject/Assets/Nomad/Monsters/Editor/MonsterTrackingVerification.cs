using System;
using Nomad.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Editor
{
    public static class MonsterTrackingVerification
    {
        private const string SessionKey = "Nomad.Monsters.Verify";
        private static MonsterTracker tracker;
        private static NavMeshAgent agent;
        private static Transform target;
        private static int stage;
        private static float since;
        private static double deadline;

        public static void Run()
        {
            MonsterTrackingAuthoring.Create();
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false))
                return;

            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "검증 시간 초과");
                if (!Application.isPlaying)
                    return;

                if (tracker == null)
                {
                    tracker = UnityEngine.Object.FindAnyObjectByType<MonsterTracker>();
                    if (tracker == null)
                        return;

                    agent = tracker.GetComponent<NavMeshAgent>();
                    var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                    player.enabled = false;
                    target = player.transform;
                    since = Time.time;
                }

                if (Time.time - since < 0.5f)
                    return;

                switch (stage)
                {
                    case 0:
                        Require(tracker.IsTracking && tracker.HasLineOfSight && agent.isOnNavMesh,
                            "정면 플레이어 탐지와 NavMesh 연결");
                        Require(tracker.transform.position.z > -1.9f, "탐지 후 실제 이동");
                        Place(new Vector3(-3.7f, 0f, 0f), new Vector3(-2.3f, 0.05f, 0f), Vector3.right);
                        Advance();
                        break;
                    case 1:
                        Require(!tracker.IsTracking && !tracker.HasLineOfSight, "근접 범위라도 벽 너머 탐지 금지");
                        Place(Vector3.zero, new Vector3(0f, 0.05f, -4f), Vector3.forward);
                        Advance();
                        break;
                    case 2:
                        Require(!tracker.IsTracking, "시야 밖 후방 목표 제외");
                        MoveTarget(new Vector3(0f, 0.05f, -1f));
                        Advance();
                        break;
                    case 3:
                        Require(tracker.IsTracking && tracker.HasLineOfSight, "근접 범위 후방 탐지");
                        Place(new Vector3(0f, 0f, 3f), new Vector3(0f, 2.05f, 10f), Vector3.forward);
                        Advance();
                        break;
                    case 4:
                        if (tracker.transform.position.y < 1.9f)
                            return;
                        Require(tracker.IsTracking, "계단을 넘어 높은 평지 추적");
                        Place(tracker.transform.position, new Vector3(0f, 0.05f, 3f), Vector3.back);
                        Advance();
                        break;
                    case 5:
                        if (tracker.transform.position.y > 0.15f || tracker.transform.position.z > 4.5f)
                            return;
                        Require(tracker.IsTracking, "계단을 내려와 낮은 평지 추적");
                        Place(new Vector3(4f, 0f, 0f), new Vector3(11.5f, 0.05f, 0f), Vector3.right);
                        Advance();
                        break;
                    case 6:
                        Require(tracker.PathBlocked && !agent.hasPath, "분리된 지점의 불완전 경로 안전 중단");
                        target.gameObject.SetActive(false);
                        Advance();
                        break;
                    case 7:
                        Require(!tracker.IsTracking && !agent.hasPath, "비활성 목표 추적 중단");
                        target.gameObject.SetActive(true);
                        Place(Vector3.zero, new Vector3(0f, 0.05f, 3f), Vector3.forward);
                        Advance();
                        break;
                    case 8:
                        Require(tracker.IsTracking, "목표 재연결");
                        MoveTarget(new Vector3(20f, 0.05f, 0f));
                        Advance();
                        break;
                    case 9:
                        Require(!tracker.IsTracking && !agent.hasPath, "추적 한계 거리 초과 중단");
                        Place(Vector3.zero, new Vector3(0f, 0.05f, 3f), Vector3.forward);
                        Advance();
                        break;
                    case 10:
                        Require(tracker.IsTracking, "시야 상실 전 목표 탐지");
                        MoveTarget(new Vector3(-4.5f, 0.05f, 0f));
                        Advance();
                        break;
                    case 11:
                        if (Time.time - since < 2.5f)
                            return;
                        Require(!tracker.IsTracking && !agent.hasPath, "벽 뒤 목표를 잃으면 유예 후 중단");
                        Debug.Log("MONSTER_TRACKING_PLAYMODE_PASS: 시야/벽/근접/계단 왕복/불완전 경로/목표 비활성/거리 종료/시야 상실 종료");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"MONSTER_TRACKING_FAIL stage={stage} monster={tracker?.transform.position} " +
                    $"target={target?.position} tracking={tracker?.IsTracking} visible={tracker?.HasLineOfSight} " +
                    $"blocked={tracker?.PathBlocked}: {exception}");
                Finish(1);
            }
        }

        private static void Place(Vector3 monster, Vector3 player, Vector3 forward)
        {
            tracker.BindTarget(null);
            Require(agent.Warp(monster), "검증 시작점 NavMesh 배치");
            tracker.transform.forward = forward;
            MoveTarget(player);
            tracker.BindTarget(target);
        }

        private static void MoveTarget(Vector3 position)
        {
            var controller = target.GetComponent<CharacterController>();
            controller.enabled = false;
            target.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void Advance()
        {
            stage++;
            since = Time.time;
            Debug.Log($"MONSTER_TRACKING_STAGE {stage}: {tracker.transform.position}");
        }

        private static void Require(bool passed, string label)
        {
            if (!passed)
                throw new InvalidOperationException(label);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            EditorApplication.Exit(code);
        }
    }
}
