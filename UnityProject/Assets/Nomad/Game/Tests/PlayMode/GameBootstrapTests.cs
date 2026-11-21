#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nomad.Game.Tests
{
    public sealed class GameBootstrapTests
    {
        private const string GameScenePath = "Assets/Nomad/Game/Game.unity";
        private const string VillagePath = "Assets/Nomad/Village/Village.unity";
        private const string FieldPath = "Assets/Nomad/Game/Field.unity";
        private const float Timeout = 20f;
        private const float FieldTimeout = 90f;

        private Scene previousScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            yield return DestroyManager();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyManager();

            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);

            // 테스트가 연 Game·Village 씬만 내립니다.
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != previousScene && (scene.path == GameScenePath || scene.path == VillagePath || scene.path == FieldPath))
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator GameScene_LoadsVillageAsActiveRegion()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));

            Scene game = SceneManager.GetSceneByPath(GameScenePath);
            Assert.That(game.GetRootGameObjects().Length, Is.EqualTo(0), "GameManager는 영속 씬으로 이동하고 Game 씬에는 다른 오브젝트가 없음");

            GameManager manager = GameManager.Instance;
            Assert.That(manager, Is.Not.Null, "Game 씬의 GameManager");

            yield return WaitForRegion(manager);
            Assert.That(manager.CurrentRegion.path, Is.EqualTo(VillagePath), "시작 지역");
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(manager.CurrentRegion), "지역 씬이 활성 씬(조명)");
        }

        [UnityTest]
        public IEnumerator LoadRegion_UnloadsPreviousAndKeepsModules()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            GameManager manager = GameManager.Instance;
            yield return WaitForRegion(manager);

            // 모듈은 GameManager 자식으로 붙어 지역 전환 후에도 남아야 합니다.
            Transform module = new GameObject("Test Module").transform;
            module.SetParent(manager.transform);

            // 활성 지역 씬에 새로 만든 오브젝트는 지역과 함께 사라집니다(런타임 UI 주의점 확인).
            GameObject regionObject = new GameObject("Region Object");
            Assert.That(regionObject.scene, Is.EqualTo(manager.CurrentRegion), "새 오브젝트는 활성 지역 씬에 생성");

            Scene first = manager.CurrentRegion;
            int loadedEvents = 0;
            manager.RegionLoaded += _ => loadedEvents++;

            Assert.That(manager.LoadRegion("Village"), Is.True, "전환 시작");
            Assert.That(manager.LoadRegion("Village"), Is.False, "전환 중 중복 요청 무시");
            yield return WaitForRegion(manager);

            Assert.That(loadedEvents, Is.EqualTo(1), "RegionLoaded 1회");
            Assert.That(first.isLoaded, Is.False, "이전 지역 언로드");
            Assert.That(manager.CurrentRegion.isLoaded && manager.CurrentRegion != first, Is.True, "새 지역 로드");
            Assert.That(CountLoaded(VillagePath), Is.EqualTo(1), "지역 씬 중복 없음");
            Assert.That(module != null, Is.True, "모듈 유지");
            Assert.That(regionObject == null, Is.True, "지역 오브젝트는 함께 언로드");
        }

        [UnityTest]
        public IEnumerator RegionScenePlayedAlone_CreatesManagerForThatRegion()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(VillagePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            Scene village = SceneManager.GetSceneByPath(VillagePath);
            SceneManager.SetActiveScene(village);

            Scene reported = default;
            GameManager manager = GameManager.BootstrapRegion();
            Assert.That(manager, Is.Not.Null, "지역 씬 단독 Play에서 생성");
            manager.RegionLoaded += scene => reported = scene;

            yield return null;
            Assert.That(manager.CurrentRegion, Is.EqualTo(village), "이미 열린 씬을 현재 지역으로 사용");
            Assert.That(reported, Is.EqualTo(village), "RegionLoaded 통지");
            Assert.That(CountLoaded(VillagePath), Is.EqualTo(1), "시작 지역을 다시 불러오지 않음");
            Assert.That(GameManager.BootstrapRegion(), Is.Null, "이미 있으면 다시 만들지 않음");
            AssertAtEntrance(manager, null);
        }

        [UnityTest]
        public IEnumerator GameStart_PlayerKeepsInventoryAndUiThroughFieldRoundTrip()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            GameManager manager = GameManager.Instance;
            yield return WaitForRegion(manager);

            // 마을 기본 입구에 같은 Player 프리팹이 서고, HUD·가방 UI는 Player와 같은 영속 씬에 있습니다.
            GameObject player = manager.Player;
            Assert.That(player, Is.Not.Null, "Player 모듈");
            AssertAtEntrance(manager, null);
            AssertUiKept(player);
            Assert.That(CallInventory(player, "Add", "scrap", 5), Is.EqualTo(5), "가방에 넣기");
            Camera camera = manager.GetComponentInChildren<Camera>();
            CameraClearFlags villageClear = camera.clearFlags;

            // 필드: 지형·경로 준비 후 Field가 플레이어를 배치합니다.
            Assert.That(manager.LoadRegion("Field"), Is.True);
            yield return WaitForRegion(manager);
            Assert.That(manager.CurrentRegion.path, Is.EqualTo(FieldPath));
            yield return WaitForActive(player);
            Assert.That(manager.Player, Is.SameAs(player), "같은 Player 유지");
            AssertUiKept(player);
            Assert.That(Terrain.activeTerrain != null && Terrain.activeTerrain.gameObject.scene == manager.CurrentRegion, Is.True, "TerrainTest 지면(#94)");
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor), "필드 카메라 배경");

            // 필드 출구에 들어가면 마을의 필드 쪽 입구로 돌아옵니다.
            Transform exit = FindInRegion(manager, "FieldExit_");
            Assert.That(exit, Is.Not.Null, "필드 출구");
            manager.PlacePlayer(exit.position + Vector3.up * 0.1f, Quaternion.identity);
            float end = Time.realtimeSinceStartup + Timeout;
            while (manager.CurrentRegion.path != VillagePath || manager.IsLoading)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "출구로 마을 전환 시간 초과");
                if (player.activeInHierarchy)
                    player.GetComponent<CharacterController>().Move(Vector3.down * 0.01f);
                yield return new WaitForFixedUpdate();
            }

            AssertAtEntrance(manager, "Field");
            AssertUiKept(player);
            Assert.That(camera.clearFlags, Is.EqualTo(villageClear), "필드를 떠나면 카메라 복원");
            Assert.That(CallInventory(player, "Remove", "scrap", int.MaxValue), Is.EqualTo(5), "가방 내용 유지");
        }

        [UnityTest]
        public IEnumerator FieldScenePlayedAlone_PlacesSamePlayerPrefab()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(FieldPath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(FieldPath));

            GameManager manager = GameManager.BootstrapRegion();
            Assert.That(manager, Is.Not.Null, "Field 단독 Play에서 생성");
            yield return WaitForActive(manager.Player);
            Assert.That(manager.Player.GetComponent("PlayerInventory"), Is.Not.Null, "메인 게임 Player 프리팹");
        }

        [UnityTest]
        public IEnumerator NonRegionScene_DoesNotCreateManager()
        {
            // 테스트 러너 씬처럼 지역 목록에 없는 씬에서는 기존 동작을 바꾸지 않습니다.
            yield return null;
            Assert.That(GameManager.BootstrapRegion(), Is.Null);
            Assert.That(GameManager.Instance, Is.Null);
        }

        private static IEnumerator WaitForRegion(GameManager manager)
        {
            float end = Time.realtimeSinceStartup + Timeout;
            while (manager.IsLoading || !manager.CurrentRegion.IsValid())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "지역 로드 시간 초과");
                yield return null;
            }
        }

        private static void AssertAtEntrance(GameManager manager, string fromRegion)
        {
            Assert.That(manager.Player.activeSelf, Is.True, "입구에 배치되어 활성화");
            RegionEntrance entrance = RegionEntrance.Find(manager.CurrentRegion, fromRegion);
            Assert.That(entrance, Is.Not.Null, "지역 입구");
            Assert.That(Vector3.Distance(manager.Player.transform.position, entrance.transform.position), Is.LessThan(0.5f), "입구 위치");
        }

        // 지역 씬이 바뀌어도 HUD·가방 UI가 사라지지 않도록 Player와 같은 씬에 있어야 합니다.
        private static void AssertUiKept(GameObject player)
        {
            foreach (string name in new[] { "Player HUD", "NomadInventory" })
            {
                GameObject ui = FindRoot(player.scene, name);
                Assert.That(ui, Is.Not.Null, name + "가 Player와 같은 씬에 있음");
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }

            return null;
        }

        private static Transform FindInRegion(GameManager manager, string prefix)
        {
            foreach (GameObject root in manager.CurrentRegion.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>())
                {
                    if (child.name.StartsWith(prefix))
                        return child;
                }
            }

            return null;
        }

        // 테스트 어셈블리는 Player 쪽 스크립트를 참조할 수 없어 이름으로 호출합니다.
        private static int CallInventory(GameObject player, string method, string itemId, int count)
        {
            Component inventory = player.GetComponent("PlayerInventory");
            return (int)inventory.GetType().GetMethod(method, new[] { typeof(string), typeof(int) })
                .Invoke(inventory, new object[] { itemId, count });
        }

        private static IEnumerator WaitForActive(GameObject target)
        {
            float end = Time.realtimeSinceStartup + FieldTimeout;
            while (!target.activeSelf)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "필드 준비 후 플레이어 배치 시간 초과");
                yield return null;
            }
        }

        private static int CountLoaded(string path)
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).path == path && SceneManager.GetSceneAt(i).isLoaded)
                    count++;
            }

            return count;
        }

        private static IEnumerator DestroyManager()
        {
            if (GameManager.Instance == null)
                yield break;

            Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }
    }
}
#endif
