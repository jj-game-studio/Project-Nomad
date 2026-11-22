using Nomad.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Game.Modules
{
    // 영속 Player가 쓰러지면 안내를 띄우고, R 키로 Game 씬부터 처음부터 다시 시작합니다.
    // 가방·장비·필드는 모두 새로 만들어지고 시작 장비를 다시 받습니다. 안내 문구·연출은 임시입니다(#89 판단 필요).
    public sealed class DeathRestart : MonoBehaviour
    {
        [SerializeField] private string message = "쓰러졌습니다\nR 키를 눌러 처음부터 다시 시작";

        private Health health;
        private GUIStyle style;

        public bool IsShowing => health != null && health.IsDead;

        private void Update()
        {
            // GameManager가 모듈 생성 뒤 Player를 정하므로 첫 Update에서 연결합니다.
            if (health == null)
                health = FindPlayerHealth();

            if (!IsShowing || Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame)
                return;

            if (GameManager.Instance != null)
                GameManager.Instance.Restart();
        }

        private static Health FindPlayerHealth()
        {
            GameManager manager = GameManager.Instance;
            return manager != null && manager.Player != null ? manager.Player.GetComponent<Health>() : null;
        }

        private void OnGUI()
        {
            if (!IsShowing)
                return;

            style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            style.fontSize = Mathf.RoundToInt(Screen.height * 0.04f);

            // 화면 전체를 어둡게 덮고 가운데에 안내를 표시합니다.
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(screen, message, style);
        }
    }
}
