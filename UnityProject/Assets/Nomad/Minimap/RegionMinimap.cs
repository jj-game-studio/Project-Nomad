using System.Collections;
using System.Collections.Generic;
using Nomad.Game;
using Nomad.Shop;
using Nomad.Terrain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nomad.Minimap
{
    public sealed class RegionMinimap : MonoBehaviour
    {
        private const float MapSize = 188;
        [SerializeField, Min(8), Tooltip("지도 한 변의 거리(m)입니다. 36이면 주변 약 18m를 표시합니다.")]
        private float mapDiameter = 36;
        private static RegionMinimap instance;
        private readonly List<(Transform world, RectTransform icon)> markers = new();
        private GameManager manager;
        private FieldNavigation navigation;
        private Scene region;
        private RectTransform view;
        private RectTransform playerArrow;
        private RawImage map;
        private Text title;
        private Text legend;
        private Font font;
        private MinimapProjection projection;
        private Coroutine rebuild;

        public bool MapReady => map != null && map.texture != null;
        public bool IsVisible => view != null && view.gameObject.activeSelf;
        public string RegionName { get; private set; }
        public int Revision { get; private set; }
        public int MarkerCount => markers.Count;
        public Texture MapTexture => map != null ? map.texture : null;
        public Vector2 PlayerPosition { get; private set; }
        public Rect MapUvRect => map.uvRect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameConfig config = GameConfig.Load();
            bool supported = config != null && (scene.name == GameManager.GameSceneName || config.IsRegion(scene.name));
            if (!supported)
            {
                if (mode == LoadSceneMode.Single && instance != null) Destroy(instance.gameObject);
                return;
            }
            if (instance == null) instance = FindAnyObjectByType<RegionMinimap>();
            if (instance == null) new GameObject("Region Minimap").AddComponent<RegionMinimap>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildView();
        }

        private void LateUpdate()
        {
            if (!object.ReferenceEquals(manager, GameManager.Instance))
            {
                if (manager != null) manager.RegionLoaded -= BindRegion;
                UnbindNavigation();
                manager = GameManager.Instance;
                ClearMap();
                if (manager != null)
                {
                    manager.RegionLoaded += BindRegion;
                    if (manager.CurrentRegion.IsValid() && manager.CurrentRegion.isLoaded) BindRegion(manager.CurrentRegion);
                }
            }
            if (navigation != null && navigation.State != FieldGenerationState.Ready && MapReady) ClearMap();
            bool visible = manager != null && !manager.IsLoading && manager.Player != null &&
                manager.Player.activeInHierarchy && MapReady && Time.timeScale > 0 && !HasWindow();
            view.gameObject.SetActive(visible);
            if (!visible) return;

            MinimapProjection window = projection.Window(manager.Player.transform.position, mapDiameter);
            Vector2 uv = projection.Normalize(window.Bounds.min);
            float scale = window.Bounds.size.x / projection.Bounds.size.x;
            map.uvRect = new Rect(uv.x, uv.y, scale, scale);
            PlayerPosition = window.Marker(manager.Player.transform.position, MapSize);
            playerArrow.anchoredPosition = PlayerPosition;
            playerArrow.localRotation = Quaternion.Euler(0, 0, -manager.Player.transform.eulerAngles.y);
            foreach (var marker in markers)
                if (marker.world != null)
                {
                    marker.icon.gameObject.SetActive(window.Contains(marker.world.position));
                    marker.icon.anchoredPosition = window.Marker(marker.world.position, MapSize);
                }
        }

        private void BindRegion(Scene next)
        {
            UnbindNavigation();
            ClearMap();
            region = next;
            RegionName = next.name;
            title.text = next.name == "Village" ? "마을" : next.name == "Field" ? "필드" : next.name;
            foreach (GameObject root in next.GetRootGameObjects())
                if (navigation == null) navigation = root.GetComponentInChildren<FieldNavigation>();

            if (navigation == null) Schedule();
            else
            {
                navigation.GenerationStarted += GenerationStarted;
                navigation.GenerationCompleted += GenerationCompleted;
                if (navigation.State == FieldGenerationState.Ready && navigation.LastResult?.Success == true) Schedule();
            }
        }

        private void GenerationStarted(string id) => ClearMap();
        private void GenerationCompleted(FieldGenerationResult result)
        {
            if (result.Success) Schedule();
            else ClearMap();
        }

        private void Schedule()
        {
            ClearMap();
            rebuild = StartCoroutine(BuildMap());
        }

        private IEnumerator BuildMap()
        {
            // 다른 Ready 소비자가 출구/구역을 만든 뒤 수집합니다.
            yield return null;
            while (manager != null && manager.IsLoading) yield return null;
            rebuild = null;
            if (!region.IsValid() || !region.isLoaded || manager == null || manager.IsLoading) yield break;
            if (navigation != null && navigation.State != FieldGenerationState.Ready) yield break;
            map.texture = navigation != null ? MinimapSurface.Field(out projection) : MinimapSurface.Village(region, out projection);
            if (!MapReady) yield break;
            Revision++;

            bool hasShop = false;
            foreach (GameObject root in region.GetRootGameObjects())
            {
                foreach (RegionExit exit in root.GetComponentsInChildren<RegionExit>())
                    if (exit.enabled) AddMarker(exit.transform, new Color(0.35f, 0.80f, 1), true);
                foreach (FieldExit exit in root.GetComponentsInChildren<FieldExit>())
                    if (exit.enabled) AddMarker(exit.transform, new Color(0.35f, 0.80f, 1), true);
                foreach (Shopkeeper shop in root.GetComponentsInChildren<Shopkeeper>())
                    if (shop.enabled) { AddMarker(shop.transform, new Color(1, 0.77f, 0.32f), false); hasShop = true; }
            }
            legend.text = "▲ 나  <color=#59CCFF>◆ 출구</color>" + (hasShop ? "  <color=#FFC452>■ 상점</color>" : "");
            playerArrow.SetAsLastSibling();
        }

        private void AddMarker(Transform world, Color color, bool diamond)
        {
            if (markers.Exists(marker => marker.world == world)) return;
            RectTransform icon = Rect(map.transform, diamond ? "Exit marker" : "Shop marker", 0, 0, 7, 7);
            icon.anchorMin = icon.anchorMax = Vector2.zero;
            icon.pivot = Vector2.one * 0.5f;
            Image graphic = icon.gameObject.AddComponent<Image>();
            graphic.color = color;
            graphic.raycastTarget = false;
            if (diamond) icon.localRotation = Quaternion.Euler(0, 0, 45);
            markers.Add((world, icon));
        }

        private void ClearMap()
        {
            if (rebuild != null) { StopCoroutine(rebuild); rebuild = null; }
            if (map != null && map.texture != null)
            {
                Texture previous = map.texture;
                map.texture = null;
                Destroy(previous);
            }
            foreach (var marker in markers) if (marker.icon != null) Destroy(marker.icon.gameObject);
            markers.Clear();
            if (view != null) view.gameObject.SetActive(false);
        }

        private void UnbindNavigation()
        {
            if (navigation == null) return;
            navigation.GenerationStarted -= GenerationStarted;
            navigation.GenerationCompleted -= GenerationCompleted;
            navigation = null;
        }

        private void OnDestroy()
        {
            if (manager != null) manager.RegionLoaded -= BindRegion;
            UnbindNavigation();
            ClearMap();
            if (instance == this) instance = null;
        }

        private static bool HasWindow()
        {
            // 카메라의 창 판정과 같은 기준입니다. 이 HUD는 Raycaster를 만들지 않습니다.
            foreach (BaseRaycaster raycaster in RaycasterManager.GetRaycasters())
            {
                if (raycaster is not GraphicRaycaster || !raycaster.isActiveAndEnabled) continue;
                Canvas canvas = raycaster.GetComponent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                var graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas);
                for (int index = 0; index < graphics.Count; index++)
                {
                    Graphic graphic = graphics[index];
                    if (graphic == null || !graphic.isActiveAndEnabled || graphic.canvasRenderer.cull) continue;
                    Vector2 center = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera,
                        graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center));
                    if (graphic.Raycast(center, raycaster.eventCamera)) return true;
                }
            }
            return false;
        }

        private void BuildView()
        {
            font = Resources.Load<Font>("ShopFonts/NanumGothic-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform canvasRoot = Rect(transform, "Minimap Canvas", 0, 0, 0, 0);
            Canvas canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            view = Rect(canvasRoot, "Minimap", -18, 18, 208, 244);
            view.anchorMin = view.anchorMax = view.pivot = Vector2.one;
            var frame = view.gameObject.AddComponent<ShopRoundedRect>();
            frame.color = new Color(0.08f, 0.14f, 0.11f, 0.96f);
            frame.Style(12, 1.5f, new Color(0.69f, 0.62f, 0.44f));
            frame.raycastTarget = false;
            title = Label(view, "Region", "", 12, 3, 150, 24, 14);
            Label(view, "North", "N", 177, 3, 20, 24, 14);
            map = Rect(view, "Map", 10, 30, MapSize, MapSize).gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            playerArrow = Rect(map.transform, "Player", 0, 0, 20, 20);
            playerArrow.anchorMin = playerArrow.anchorMax = Vector2.zero;
            playerArrow.pivot = Vector2.one * 0.5f;
            var arrow = playerArrow.gameObject.AddComponent<MinimapArrow>();
            arrow.font = font;
            arrow.text = "▲";
            arrow.fontSize = 18;
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.horizontalOverflow = HorizontalWrapMode.Overflow;
            arrow.verticalOverflow = VerticalWrapMode.Overflow;
            arrow.color = Color.white;
            arrow.raycastTarget = false;
            legend = Label(view, "Legend", "", 10, 222, 188, 18, 11);
            legend.alignment = TextAnchor.MiddleCenter;
            view.gameObject.SetActive(false);
        }

        private Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size)
        {
            Text text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
    }
}
