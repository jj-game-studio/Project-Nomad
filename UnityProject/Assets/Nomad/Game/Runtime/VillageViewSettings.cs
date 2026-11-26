using UnityEngine;

namespace Nomad.Game
{
    // 메뉴와 게임은 같은 값을 읽습니다. 필드에 가도 마을의 선택은 유지합니다.
    public static class VillageViewSettings
    {
        public const string PreferenceKey = "Nomad.VillageFirstPerson";
        public static bool FirstPerson => PlayerPrefs.GetInt(PreferenceKey, 0) != 0;

        public static void SetFirstPerson(bool value)
        {
            PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
