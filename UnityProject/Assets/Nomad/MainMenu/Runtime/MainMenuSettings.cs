using UnityEngine;

namespace Nomad.MainMenu
{
    public static class MainMenuSettings
    {
        public const string VolumeKey = "Nomad.MasterVolume";
        public const string FullscreenKey = "Nomad.Fullscreen";

        // 저장한 음량은 메뉴를 거치지 않고 Game을 직접 Play해도 동일하게 적용합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySaved()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            if (PlayerPrefs.HasKey(FullscreenKey))
                Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey) != 0;
        }

        public static void SetVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        }

        public static void SetFullscreen(bool value)
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }
    }
}
