using System.Collections;
using Nomad.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nomad.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [Header("표시 내용")]
        public MainMenuContent Content;
        public UnityEngine.UI.RawImage Background;
        public UnityEngine.UI.Image Logo;
        public TMP_Text ProjectName;
        public TMP_Text Title;
        public TMP_Text Description;
        public TMP_Text Footer;

        [Header("메뉴와 설정")]
        public GameObject MenuPanel;
        public GameObject SettingsPanel;
        public UnityEngine.UI.Button NewGameButton;
        public UnityEngine.UI.Button SettingsButton;
        public UnityEngine.UI.Button QuitButton;
        public UnityEngine.UI.Button BackButton;
        public TMP_Text SettingsTitle;
        public TMP_Text VolumeLabel;
        public TMP_Text VolumeValue;
        public TMP_Text FullscreenLabel;
        public UnityEngine.UI.Slider VolumeSlider;
        public UnityEngine.UI.Toggle FullscreenToggle;

        [Header("전환")]
        public CanvasGroup Fade;
        public TMP_Text Status;

        public bool IsStarting { get; private set; }
        public bool IsShowingSettings => SettingsPanel.activeSelf;

        private void Awake()
        {
            AddVillageSetting();
            RefreshContent();
            NewGameButton.onClick.AddListener(StartNewGame);
            SettingsButton.onClick.AddListener(OpenSettings);
            QuitButton.onClick.AddListener(Quit);
            BackButton.onClick.AddListener(CloseSettings);
            VolumeSlider.onValueChanged.AddListener(ChangeVolume);
            FullscreenToggle.onValueChanged.AddListener(MainMenuSettings.SetFullscreen);
        }

        private void AddVillageSetting()
        {
            var setting = VillageViewSettingControl.Create(SettingsPanel.transform, FullscreenLabel.font,
                new Vector2(16, -260), new Vector2(466, 48));
            ((RectTransform)BackButton.transform).anchoredPosition = new Vector2(0, -326);
            RectTransform panel = (RectTransform)SettingsPanel.transform;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 430);
            RectTransform surface = (RectTransform)SettingsPanel.transform.Find("Surface");
            surface.sizeDelta = new Vector2(surface.sizeDelta.x, 442);

            LinkNavigation(FullscreenToggle, VolumeSlider, setting);
            LinkNavigation(setting, FullscreenToggle, BackButton);
            LinkNavigation(BackButton, setting, VolumeSlider);
        }

        private static void LinkNavigation(UnityEngine.UI.Selectable control, UnityEngine.UI.Selectable up,
            UnityEngine.UI.Selectable down)
        {
            control.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnUp = up, selectOnDown = down };
        }

        private void Start()
        {
            // Game의 일시정지 상태를 메뉴 진입에 가져오지 않습니다.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            CloseSettings();
        }

        private void Update()
        {
            if (!IsStarting && IsShowingSettings && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                CloseSettings();
        }

        public void RefreshContent()
        {
            if (Content == null)
                return;

            Background.texture = Content.Background;
            if (Content.Background != null)
                Background.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio =
                    (float)Content.Background.width / Content.Background.height;

            Logo.sprite = Content.Logo;
            Logo.gameObject.SetActive(Content.Logo != null);
            Title.gameObject.SetActive(Content.Logo == null);
            ProjectName.text = Content.ProjectName;
            Title.text = Content.Title;
            Title.textWrappingMode = TextWrappingModes.NoWrap;
            Description.text = Content.Description;
            Footer.text = Content.Footer;

            SetButtonText(NewGameButton, Content.NewGame);
            SetButtonText(SettingsButton, Content.Settings);
            SetButtonText(QuitButton, Content.Quit);
            SetButtonText(BackButton, Content.Back);
            SettingsTitle.text = Content.Settings;
            VolumeLabel.text = Content.Volume;
            FullscreenLabel.text = Content.Fullscreen;
        }

        public void OpenSettings()
        {
            if (IsStarting)
                return;

            MenuPanel.SetActive(false);
            SettingsPanel.SetActive(true);
            VolumeSlider.SetValueWithoutNotify(AudioListener.volume);
            VolumeValue.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
            FullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            EventSystem.current?.SetSelectedGameObject(VolumeSlider.gameObject);
        }

        public void CloseSettings()
        {
            if (IsStarting)
                return;

            PlayerPrefs.Save();
            SettingsPanel.SetActive(false);
            MenuPanel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(NewGameButton.gameObject);
        }

        private void ChangeVolume(float value)
        {
            MainMenuSettings.SetVolume(value);
            VolumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        public void StartNewGame()
        {
            if (IsStarting)
                return;

            if (!Application.CanStreamedLevelBeLoaded(GameManager.GameSceneName))
            {
                Status.text = Content.LoadError;
                return;
            }

            IsStarting = true;
            SetMenuInteractable(false);
            StartCoroutine(StartGameRoutine());
        }

        private IEnumerator StartGameRoutine()
        {
            PlayerPrefs.Save();
            Fade.blocksRaycasts = true;
            Status.text = Content.Loading;
            Status.transform.SetAsLastSibling();
            EventSystem.current?.SetSelectedGameObject(null);

            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                Fade.alpha = Mathf.Clamp01(elapsed / 0.25f);
                yield return null;
            }

            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (GameManager.Instance != null)
                GameManager.Instance.Restart();
            else
                yield return SceneManager.LoadSceneAsync(GameManager.GameSceneName, LoadSceneMode.Single);
        }

        private void SetMenuInteractable(bool value)
        {
            NewGameButton.interactable = value;
            SettingsButton.interactable = value;
            QuitButton.interactable = value;
        }

        private void Quit()
        {
            if (IsStarting)
                return;

            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            NewGameButton.onClick.RemoveListener(StartNewGame);
            SettingsButton.onClick.RemoveListener(OpenSettings);
            QuitButton.onClick.RemoveListener(Quit);
            BackButton.onClick.RemoveListener(CloseSettings);
            VolumeSlider.onValueChanged.RemoveListener(ChangeVolume);
            FullscreenToggle.onValueChanged.RemoveListener(MainMenuSettings.SetFullscreen);
        }

        private static void SetButtonText(UnityEngine.UI.Button button, string text) =>
            button.GetComponentInChildren<TMP_Text>(true).text = text;
    }
}
