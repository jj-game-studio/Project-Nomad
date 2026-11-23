using UnityEngine;

namespace Nomad.MainMenu
{
    [CreateAssetMenu(menuName = "Nomad/Main Menu Content")]
    public sealed class MainMenuContent : ScriptableObject
    {
        [Header("제목과 설명")]
        public string ProjectName = "PROJECT NOMAD";
        public string Title = "NOMAD";
        [TextArea(1, 3)] public string Description = "살아남아, 가치를 옮기다";
        public string Footer = "JJ GAME STUDIO";

        [Header("그림 — 로고를 비우면 제목 텍스트 표시")]
        public Texture2D Background;
        public Sprite Logo;

        [Header("메뉴 문구")]
        public string NewGame = "새 게임";
        public string Settings = "설정";
        public string Quit = "게임 종료";
        public string Back = "돌아가기";
        public string Volume = "전체 음량";
        public string Fullscreen = "전체 화면";
        public string Loading = "여정을 준비하고 있습니다…";
        public string LoadError = "게임을 시작할 수 없습니다. 다시 시도해 주세요.";
    }
}
