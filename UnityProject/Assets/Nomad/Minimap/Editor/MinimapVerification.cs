using System;
using System.IO;
using System.Linq;
using Nomad.Game;
using Nomad.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nomad.Minimap.Editor
{
    public static class MinimapVerification
    {
        private const string SessionKey = "Nomad.Minimap.Verify";
        private static double deadline;
        private static int frame = -1;
        private static int stage;
        private static RegionMinimap minimap;
        private static GameManager manager;
        private static Texture previousTexture;
        private static int revision;
        private static Vector3 playerStart;
        private static Quaternion playerRotation;
        private static Vector2 previousPlayer;
        private static Rect previousUv;
        private static Keyboard keyboard;
        private static string error;

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("배치 전용 검증입니다.");
            EditorSceneManager.OpenScene("Assets/Nomad/Game/Game.unity");
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 900;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
        }

        private static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && trace.Contains("UnityEditor.Search.SearchDatabase") && !trace.Contains("Nomad.")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message;
        }

        private static bool Ready(string region) => manager != null && !manager.IsLoading &&
            manager.CurrentRegion.IsValid() && manager.CurrentRegion.name == region && minimap != null &&
            minimap.RegionName == region && minimap.MapReady && minimap.IsVisible;

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, $"시간 초과 stage={stage}, region={minimap?.RegionName}, ready={minimap?.MapReady}, visible={minimap?.IsVisible}");
                Require(error == null, error);
                if (!Application.isPlaying || Time.frameCount == frame) return;
                frame = Time.frameCount;
                manager = GameManager.Instance;
                minimap = Object.FindAnyObjectByType<RegionMinimap>();
                switch (stage)
                {
                    case 0:
                        if (!Ready("Village")) return;
                        Require(minimap.MarkerCount >= 2, "마을 출구·상점 표시");
                        Require(minimap.EnemyMarkerCount == 0, "마을에 필드 적 잔상 없음");
                        Require(minimap.GetComponentInChildren<GraphicRaycaster>(true) == null &&
                            minimap.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), "입력 비차단 HUD");
                        Capture("minimap-village.png");
                        Require(((Texture2D)minimap.MapTexture).GetPixels32().Distinct().Count() > 2, "마을 지형·도로·장애물 지도");
                        playerStart = manager.Player.transform.position;
                        playerRotation = manager.Player.transform.rotation;
                        previousPlayer = minimap.PlayerPosition;
                        previousUv = minimap.MapUvRect;
                        previousTexture = minimap.MapTexture;
                        revision = minimap.Revision;
                        manager.PlacePlayer(playerStart + Vector3.right * 2, Quaternion.Euler(0, 90, 0));
                        stage++;
                        break;
                    case 1:
                        Require(minimap.MapUvRect.x > previousUv.x && Mathf.Abs(minimap.MapUvRect.y - previousUv.y) < 0.001f &&
                            Vector2.Distance(minimap.PlayerPosition, previousPlayer) < 0.1f && minimap.MapUvRect.width < 1,
                            "주변 지도 이동·플레이어 중심·북쪽 고정");
                        float angle = minimap.GetComponentInChildren<MinimapArrow>().rectTransform.localEulerAngles.z;
                        Require(Mathf.Abs(Mathf.DeltaAngle(angle, -manager.Player.transform.eulerAngles.y)) < 1, "플레이어 방향 반영");
                        Require(minimap.MapTexture == previousTexture && minimap.Revision == revision, "매 프레임 지도 재생성 없음");
                        manager.PlacePlayer(playerStart, playerRotation);
                        Application.runInBackground = true;
                        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        keyboard = InputSystem.AddDevice<Keyboard>();
                        keyboard.MakeCurrent();
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                        stage++;
                        break;
                    case 2:
                        Require(!minimap.IsVisible, "인벤토리 창에서 미니맵 숨김");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        stage++;
                        break;
                    case 3:
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                        stage++;
                        break;
                    case 4:
                        if (!Ready("Village")) return;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Require(manager.LoadRegion("Field"), "필드 전환 요청");
                        stage++;
                        break;
                    case 5:
                        if (!Ready("Field")) return;
                        Require(previousTexture == null, "마을 지도 Texture 해제");
                        int exits = manager.CurrentRegion.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<FieldExit>().Length);
                        Require(exits > 0 && minimap.MarkerCount == exits,
                            "필드의 실제 출구 수 반영·상점 잔상 없음");
                        Require(minimap.EnemyMarkerCount == 0, "필드 적 마커 표시 안 함");
                        Require(Object.FindObjectsByType<Nomad.Monsters.Demo.BossRouteGuide>(FindObjectsSortMode.None).Length == 0,
                            "보스 위치 바닥 화살표 생성 안 함");
                        Require(((Texture2D)minimap.MapTexture).GetPixels32().Distinct().Count() > 1, "NavMesh 지도 생성");
                        Capture("minimap-field.png");
                        previousTexture = minimap.MapTexture;
                        revision = minimap.Revision;
                        TerrainIntegrationControls controls = manager.CurrentRegion.GetRootGameObjects()
                            .SelectMany(root => root.GetComponentsInChildren<TerrainIntegrationControls>()).First();
                        controls.Regenerate(false);
                        Require(!minimap.MapReady, "재생성 시작 시 이전 지도 즉시 무효화");
                        stage++;
                        break;
                    case 6:
                        if (!Ready("Field") || minimap.Revision == revision) return;
                        Require(previousTexture == null, "이전 세대 Texture 해제");
                        Require(Object.FindObjectsByType<RegionMinimap>(FindObjectsSortMode.None).Length == 1, "HUD 중복 없음");
                        Require(minimap.EnemyMarkerCount == 0, "재생성 후에도 적 마커 없음");
                        Capture("minimap-field-regenerated.png");
                        previousTexture = minimap.MapTexture;
                        Require(manager.LoadRegion("Village"), "마을 복귀 요청");
                        stage++;
                        break;
                    case 7:
                        if (!Ready("Village")) return;
                        Require(previousTexture == null && minimap.MarkerCount >= 2, "마을 복귀 시 지도·마커 교체");
                        Require(minimap.EnemyMarkerCount == 0, "마을 복귀 시 적 마커 해제");
                        Capture("minimap-village-return.png");
                        Debug.Log("MINIMAP_PLAYMODE_PASS village=pass field=pass regeneration=pass roundtrip=pass markers=pass input=pass resources=pass");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"MINIMAP_PLAYMODE_FAIL stage={stage}: {exception}");
                Finish(1);
            }
        }

        internal static void Capture(string name)
        {
            Camera camera = Camera.main;
            Canvas[] overlays = Resources.FindObjectsOfTypeAll<Canvas>().Where(canvas => canvas.isActiveAndEnabled &&
                canvas.gameObject.scene.IsValid() && canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var original = overlays.Select(canvas => (canvas, canvas.worldCamera, canvas.planeDistance)).ToArray();
            var target = new RenderTexture(1440, 900, 24);
            var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            RenderTexture active = RenderTexture.active;
            try
            {
                foreach (Canvas canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1;
                }
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                pixels.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                foreach (var state in original)
                {
                    state.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    state.canvas.worldCamera = state.worldCamera;
                    state.canvas.planeDistance = state.planeDistance;
                }
                RenderTexture.active = active;
                Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Log;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            EditorApplication.Exit(code);
        }
    }
}
