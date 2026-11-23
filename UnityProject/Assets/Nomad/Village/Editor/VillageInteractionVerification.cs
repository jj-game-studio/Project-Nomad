using System;
using System.IO;
using Nomad.Combat;
using Nomad.Game;
using Nomad.Interaction;
using Nomad.Player;
using Nomad.Shop;
using Nomad.WorldHud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nomad.Village.Editor
{
    // 실제 Game 씬에서 마을 상점·기도 나무까지 걸어가 상호작용 UI와 기도 회복을 확인합니다.
    public static class VillageInteractionVerification
    {
        private const string SessionKey = "Nomad.Village.InteractionVerify";
        private static CharacterController player;
        private static InteractionHints hints;
        private static Shopkeeper shop;
        private static PrayerTree tree;
        private static Keyboard keyboard;
        private static double deadline;
        private static int frame;
        private static int stage;
        private static int walked;
        private static Transform vfx;
        private static string unexpectedError;

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("전용 checkout의 배치 모드에서 실행해 주세요.");

            EditorSceneManager.OpenScene(VillageAuthoring.ScenePath);
            Require(Object.FindObjectsByType<PrayerTree>(FindObjectsSortMode.None).Length == 1, "저장된 마을 기도 나무 하나");
            EditorSceneManager.OpenScene("Assets/Nomad/Game/Game.unity");

            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false))
                return;

            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && trace.Contains("UnityEditor.Search.SearchDatabase") && !trace.Contains("Nomad."))
                return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                unexpectedError = message;
        }

        private static void Update()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "마을 상호작용 검증 시간 초과");
                Require(unexpectedError == null, "Unity 오류: " + unexpectedError);
                if (!Application.isPlaying || Time.frameCount <= frame)
                    return;
                frame = Time.frameCount;

                GameManager manager = GameManager.Instance;
                if (manager == null || manager.IsLoading || manager.Player == null || !manager.Player.activeInHierarchy)
                    return;

                if (player == null)
                {
                    if (manager.CurrentRegion.name != "Village")
                        return;
                    shop = Object.FindAnyObjectByType<Shopkeeper>();
                    tree = Object.FindAnyObjectByType<PrayerTree>();
                    if (shop == null || tree == null)
                        return;

                    player = manager.Player.GetComponent<CharacterController>();
                    hints = manager.Player.GetComponent<InteractionHints>();
                    Require(hints != null, "영속 플레이어의 상호작용 UI");
                    Application.runInBackground = true;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    keyboard.MakeCurrent();
                    Queue();

                    Vector3 spawn = player.transform.position - new Vector3(0, 0.1f, -6.5f);
                    spawn.y = 0;
                    Require(spawn.magnitude < 0.5f, $"광장 나무 남쪽 시작 위치 ({player.transform.position})");
                    Require(Mathf.Approximately(player.GetComponent<PlayerMovement>().SpeedMultiplier, 1.5f), "마을 이동 속도 1.5배");
                    TextMesh sign = shop.GetComponentInChildren<TextMesh>();
                    Require(sign.text == "마을 상점" && sign.transform.localPosition.y > 3, "상점 간판 이름만 표시·위로 이동");
                }

                switch (stage)
                {
                    case 0:
                        // 순간이동 없이 입구에서 시장 상점까지 걸어갑니다.
                        if (WalkTo(new Vector3(-4, 0, -10), 0.2f))
                            stage++;
                        return;
                    case 1:
                        if (!WalkTo(shop.transform.position, 0.7f))
                            return;
                        stage++;
                        return;
                    case 2:
                        RequireLabel(shop, "거래");
                        Capture("village-interaction-shop.png");
                        stage++;
                        return;
                    case 3:
                        // 상점에서 광장 안쪽으로 걸어 나무 줄기 앞에 섭니다.
                        Require(tree.InteractionPoint == tree.transform.position + Vector3.up, "기도 아이콘 나무 중점 고정");
                        if (!WalkTo(tree.transform.position, tree.InteractionRange - 0.8f))
                            return;
                        stage++;
                        return;
                    case 4:
                        RequireLabel(tree, "기도");
                        Health health = player.GetComponent<Health>();
                        PlayerMana mana = player.GetComponent<PlayerMana>();
                        health.TryApplyDamage(40);
                        Require(mana.TrySpend(50), "기도 전 마나 소모");
                        Require(health.CurrentHealth < health.MaxHealth && mana.Current < mana.Maximum, "기도 전 체력·마나 감소");
                        Capture("village-interaction-tree.png");
                        Queue(Key.E);
                        stage++;
                        return;
                    case 5:
                        Queue();
                        health = player.GetComponent<Health>();
                        mana = player.GetComponent<PlayerMana>();
                        Require(health.CurrentHealth == health.MaxHealth && Mathf.Approximately(mana.Current, mana.Maximum),
                            "E 기도로 체력·마나 최대 회복");
                        vfx = player.transform.Find(PrayerVfx.RootName);
                        Require(vfx != null && vfx.GetComponentsInChildren<ParticleSystem>().Length > 0, "기도 VFX가 플레이어 자식으로 재생");
                        stage++;
                        return;
                    case 6:
                        // 이동 중에도 VFX가 플레이어를 따라가는지 확인합니다.
                        if (++walked < 8)
                        {
                            player.Move(Vector3.back * 0.15f);
                            return;
                        }
                        Require(vfx != null && vfx.parent == player.transform &&
                            (vfx.position - player.transform.position).sqrMagnitude < 0.0001f &&
                            vfx.GetComponentInChildren<ParticleSystem>().main.simulationSpace == ParticleSystemSimulationSpace.Local,
                            "이동 중 VFX 추종");
                        Capture("village-prayer-vfx.png");
                        stage++;
                        return;
                    case 7:
                        if (vfx != null)
                            return;
                        Require(manager.LoadRegion("Field"), "Field 로드 시작");
                        stage++;
                        return;
                    case 8:
                        Require(manager.CurrentRegion.name == "Field" &&
                            Mathf.Approximately(player.GetComponent<PlayerMovement>().SpeedMultiplier, 1f), "필드에서 이동 속도 원복");
                        Debug.Log("VILLAGE_INTERACTION_PLAYMODE_PASS spawn=tree-south speed=1.5 shop=label tree=center prayer=restore vfx=follow field=speed1");
                        Finish(0);
                        return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"VILLAGE_INTERACTION_PLAYMODE_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        private static bool WalkTo(Vector3 target, float tolerance)
        {
            Vector3 delta = target - player.transform.position;
            delta.y = 0;
            if (delta.magnitude <= tolerance)
                return true;

            player.Move(delta.normalized * Mathf.Min(0.15f, delta.magnitude));
            return false;
        }

        // 선택된 대상의 아이콘 위에 "E 행동" 문구가 보이는지 확인합니다.
        private static void RequireLabel(IInteractable target, string action)
        {
            hints.Refresh();
            Require(ReferenceEquals(hints.Selected, target) && hints.VisibleCount > 0,
                $"{action} 대상 선택과 아이콘 표시 (선택={hints.Selected}, 표시={hints.VisibleCount})");

            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text label in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (label.name != "E Key" || !label.gameObject.activeInHierarchy)
                    continue;

                label.ForceMeshUpdate();
                Require(label.text.Contains(action) && label.rectTransform.anchoredPosition.y > 0 &&
                    label.textInfo.characterCount > 0 && label.textInfo.characterInfo[label.textInfo.characterCount - 1].character != '□',
                    $"아이콘 위 E {action} 문구와 한글 글리프");
                return;
            }

            throw new InvalidOperationException($"E {action} 문구를 찾을 수 없습니다.");
        }

        private static void Capture(string filename)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts"));
            Directory.CreateDirectory(directory);
            Camera camera = Camera.main;
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                // 배치 모드는 Game View를 그리지 않으므로 같은 카메라로 오버레이 UI까지 렌더합니다.
                camera.targetTexture = target;
                foreach (Canvas canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1;
                }

                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, filename), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                foreach (Canvas canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }

                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        private static void Queue(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Update;
            Application.logMessageReceived -= OnLog;
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
