using UnityEngine;

namespace Nomad.Game
{
    // 하단 HUD 표시 여부입니다. 설정 메뉴가 바꾸고 PlayerHud가 매 프레임 읽습니다.
    public static class HudVisibilitySettings
    {
        public const string PreferenceKey = "Nomad.HudVisible";
        public static bool Visible => PlayerPrefs.GetInt(PreferenceKey, 1) != 0;

        public static void SetVisible(bool value)
        {
            PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
