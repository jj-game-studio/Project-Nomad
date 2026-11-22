using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nomad.Game
{
    // 메인 게임의 유일한 씬 오브젝트입니다. 모듈을 한 번 생성해 유지하고, 지역 씬을 additive로 전환합니다.
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        public const string PlayerTag = "Player";

        // 처음부터 다시 시작할 때 불러오는 부트스트랩 씬입니다(Build Settings 첫 씬).
        public const string GameSceneName = "Game";

        public static GameManager Instance { get; private set; }

        public GameConfig Config { get; private set; }
        public Scene CurrentRegion { get; private set; }
        public string PreviousRegion { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsRestarting { get; private set; }

        // 모듈 중 Player 태그가 붙은 오브젝트입니다. 지역이 바뀌어도 같은 인스턴스를 유지합니다.
        public GameObject Player { get; private set; }

        // 지역 씬이 로드되어 활성 씬이 된 뒤 호출됩니다. 모듈은 직렬화 참조 대신 이 이벤트로 연결합니다.
        public event Action<Scene> RegionLoaded;

        // 도메인 리로드를 끈 Play 진입에서도 이전 Play의 인스턴스가 남지 않게 합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        // 지역 씬을 단독 Play하면 GameManager를 만들고 그 씬을 현재 지역으로 삼습니다.
        // 지역 목록에 없는 테스트·검증 씬에서는 아무것도 하지 않습니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapOnPlay() => BootstrapRegion();

        internal static GameManager BootstrapRegion()
        {
            if (Instance != null)
                return null;

            GameConfig config = GameConfig.Load();
            Scene active = SceneManager.GetActiveScene();
            if (config == null || !config.IsRegion(active.name))
                return null;

            var manager = new GameObject("GameManager").AddComponent<GameManager>();
            manager.CurrentRegion = active;
            return manager;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Config = GameConfig.Load();
            if (Config == null)
            {
                Debug.LogError($"Resources/{GameConfig.ResourcePath} 구성을 찾지 못했습니다.", this);
                return;
            }

            // 모듈은 GameManager 자식으로 두어 지역 전환 후에도 함께 유지됩니다.
            foreach (GameObject module in Config.Modules)
            {
                if (module != null)
                    Instantiate(module, transform).name = module.name;
            }

            // 지역이 플레이어 위치를 정하기 전까지는 숨겨 떨어지거나 잘못된 곳에서 움직이지 않게 합니다.
            Player = FindTagged(transform, PlayerTag);
            if (Player != null)
                Player.SetActive(false);
        }

        private void Start()
        {
            if (Instance != this || Config == null)
                return;

            // 지역 씬 단독 Play는 이미 열린 씬을 그대로 쓰고, Game 씬에서는 시작 지역을 불러옵니다.
            if (CurrentRegion.IsValid())
                EnterRegion(CurrentRegion);
            else
                LoadRegion(Config.StartRegion);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // 지역 씬을 전환합니다. 전환 중이면 false를 반환하고 무시합니다.
        public bool LoadRegion(string sceneName)
        {
            if (IsLoading || string.IsNullOrEmpty(sceneName))
                return false;

            StartCoroutine(LoadRegionRoutine(sceneName));
            return true;
        }

        // 영속 모듈(Player·가방·장비 포함)과 현재 지역을 모두 버리고 Game 씬부터 새로 시작합니다.
        // 지역 씬을 단독 Play한 경우에도 Game 씬으로 시작해 마을 입구에서 출발합니다.
        public void Restart()
        {
            if (IsRestarting)
                return;

            IsRestarting = true;
            StopAllCoroutines();

            // 새 Game 씬의 GameManager가 Awake에서 자신을 Instance로 삼도록 먼저 비웁니다.
            Instance = null;
            Destroy(gameObject);
            SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
        }

        // CharacterController는 활성 상태에서 위치를 바꾸면 되돌아가므로 끈 상태에서 옮깁니다.
        public void PlacePlayer(Vector3 position, Quaternion rotation)
        {
            if (Player == null)
                return;

            Player.SetActive(false);
            Player.transform.SetPositionAndRotation(position, rotation);
            Player.SetActive(true);
            Physics.SyncTransforms();
        }

        private IEnumerator LoadRegionRoutine(string sceneName)
        {
            IsLoading = true;
            Scene previous = CurrentRegion;

            // 언로드 후에는 Scene 핸들이 무효가 되어 이름을 읽을 수 없으므로 먼저 기억합니다.
            string previousName = previous.IsValid() ? previous.name : null;
            if (Player != null)
                Player.SetActive(false);

            // 씬이 하나만 남으면 언로드할 수 없으므로 새 지역을 먼저 불러온 뒤 이전 지역을 내립니다.
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"지역 씬 {sceneName}을 불러오지 못했습니다. Build Settings 등록을 확인해 주세요.", this);
                IsLoading = false;
                yield break;
            }

            yield return load;

            // 같은 이름의 지역을 다시 불러와도 구분되도록 가장 마지막에 로드된 씬을 사용합니다.
            Scene next = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

            // 조명·환경 설정은 활성 씬을 따르므로 지역 씬을 활성화합니다.
            SceneManager.SetActiveScene(next);

            if (previous.IsValid() && previous.isLoaded)
                yield return SceneManager.UnloadSceneAsync(previous);

            PreviousRegion = previousName;
            CurrentRegion = next;
            IsLoading = false;
            EnterRegion(next);
        }

        // 지역 전용 모듈을 그 씬에 만들고, 입구가 있으면 플레이어를 세운 뒤 알립니다.
        // 입구가 없는 지역(필드)은 지형 생성이 끝난 뒤 지역 스스로 PlacePlayer를 호출합니다.
        private void EnterRegion(Scene region)
        {
            foreach (GameObject prefab in Config.GetRegionModules(region.name))
            {
                GameObject module = Instantiate(prefab);
                module.name = prefab.name;
                SceneManager.MoveGameObjectToScene(module, region);
            }

            RegionEntrance entrance = RegionEntrance.Find(region, PreviousRegion);
            if (entrance != null)
                PlacePlayer(entrance.transform.position, entrance.transform.rotation);

            RegionLoaded?.Invoke(region);
        }

        private static GameObject FindTagged(Transform root, string tag)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.CompareTag(tag))
                    return child.gameObject;
            }

            return null;
        }
    }
}
