#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.Monsters.Tests
{
    public sealed class MonsterTrackingTests
    {
        private const string ScenePath = "Assets/Nomad/Monsters/MonsterTracking.unity";
        private Scene previousScene;
        private Scene testScene;
        private MonsterTracker tracker;
        private NavMeshAgent agent;
        private Transform target;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            // 저장된 전용 씬을 읽고 검사할 때마다 다시 생성하거나 저장하지 않습니다.
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            testScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(testScene.IsValid() && testScene.isLoaded, Is.True, "테스트 씬 로드");

            GameObject[] roots = testScene.GetRootGameObjects();
            tracker = Array.Find(roots, root => root.name == "Monster").GetComponent<MonsterTracker>();
            agent = tracker.GetComponent<NavMeshAgent>();
            target = Array.Find(roots, root => root.name == "Player").transform;
            foreach (MonoBehaviour input in target.GetComponents<MonoBehaviour>())
                input.enabled = false;

            Assert.That(agent.isOnNavMesh, Is.True, "전용 NavMesh 초기화");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (tracker != null)
                tracker.BindTarget(null);

            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);

            if (testScene.IsValid() && testScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(testScene);
        }

        [UnityTest]
        public IEnumerator DetectsFrontTargetAndMoves()
        {
            yield return Until(() => tracker.IsTracking && tracker.HasLineOfSight &&
                tracker.transform.position.z > -1.9f, 2f, "정면 목표 탐지 및 실제 이동");
        }

        [UnityTest]
        public IEnumerator WallBlocksEvenProximityDetection()
        {
            Place(new Vector3(-3.7f, 0f, 0f), new Vector3(-2.3f, 0.05f, 0f), Vector3.right);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tracker.IsTracking || tracker.HasLineOfSight, Is.False, State());
        }

        [UnityTest]
        public IEnumerator RearTargetRequiresProximity()
        {
            Place(Vector3.zero, new Vector3(0f, 0.05f, -4f), Vector3.forward);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tracker.IsTracking, Is.False, "시야 밖 후방: " + State());

            MoveTarget(new Vector3(0f, 0.05f, -1f));
            yield return Until(() => tracker.IsTracking && tracker.HasLineOfSight,
                1f, "후방 근접 감지");
        }

        [UnityTest]
        public IEnumerator TraversesStairsUpAndDown()
        {
            Place(new Vector3(0f, 0f, 4f), new Vector3(0f, 2.05f, 11.5f), Vector3.forward);
            yield return Until(() => tracker.transform.position.y >= 1.9f &&
                tracker.transform.position.z >= 9.5f, 15f, "높은 평지 도착");
            Assert.That(tracker.IsTracking, Is.True, State());

            Place(tracker.transform.position, new Vector3(0f, 0.05f, 3f), Vector3.back);
            yield return Until(() => tracker.transform.position.y <= 0.15f &&
                tracker.transform.position.z <= 5f, 15f, "계단 하강 및 낮은 평지 도착");
            Assert.That(tracker.IsTracking, Is.True, State());
        }

        [UnityTest]
        public IEnumerator DisconnectedDestinationStopsMovement()
        {
            Place(new Vector3(4f, 0f, 0f), new Vector3(11.5f, 0.05f, 0f), Vector3.right);
            yield return Until(() => tracker.PathBlocked, 1f, "단절 경로 감지");
            Assert.That(agent.hasPath, Is.False, State());
        }

        [UnityTest]
        public IEnumerator InactiveTargetStopsTracking()
        {
            yield return Until(() => tracker.IsTracking, 1f, "비활성 전 탐지");
            target.gameObject.SetActive(false);
            yield return Until(() => !tracker.IsTracking && !agent.hasPath, 1f, "비활성 목표 정리");
        }

        [UnityTest]
        public IEnumerator DistantTargetStopsTracking()
        {
            yield return Until(() => tracker.IsTracking, 1f, "거리 이탈 전 탐지");
            MoveTarget(new Vector3(20f, 0.05f, 0f));
            yield return Until(() => !tracker.IsTracking && !agent.hasPath, 1f, "추적 거리 초과");
        }

        [UnityTest]
        public IEnumerator LostSightExpiresWithoutFollowingHiddenTarget()
        {
            Place(Vector3.zero, new Vector3(0f, 0.05f, 3f), Vector3.forward);
            yield return Until(() => tracker.IsTracking, 1f, "시야 상실 전 탐지");
            MoveTarget(new Vector3(-4.5f, 0.05f, 0f));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tracker.HasLineOfSight, Is.False, State());
            Assert.That(tracker.IsTracking, Is.True, "유예 중 마지막 목격 위치 추적: " + State());
            Assert.That(agent.destination.x, Is.EqualTo(0f).Within(0.1f),
                "벽 뒤 목표의 실시간 위치를 목적지로 사용하지 않음");

            yield return Until(() => !tracker.IsTracking && !agent.hasPath, 3f, "시야 상실 유예 종료");
        }

        private IEnumerator Until(Func<bool> condition, float seconds, string label)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(condition(), Is.True, label + ": " + State());
        }

        private void Place(Vector3 monster, Vector3 player, Vector3 forward)
        {
            tracker.BindTarget(null);
            Assert.That(agent.Warp(monster), Is.True, "NavMesh 시작점 배치");
            tracker.transform.forward = forward;
            MoveTarget(player);
            tracker.BindTarget(target);
        }

        private void MoveTarget(Vector3 position)
        {
            var controller = target.GetComponent<CharacterController>();
            controller.enabled = false;
            target.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private string State()
        {
            return $"monster={tracker.transform.position}, target={target.position}, " +
                $"tracking={tracker.IsTracking}, visible={tracker.HasLineOfSight}, " +
                $"blocked={tracker.PathBlocked}, remaining={agent.remainingDistance}, velocity={agent.velocity}";
        }
    }
}
#endif
