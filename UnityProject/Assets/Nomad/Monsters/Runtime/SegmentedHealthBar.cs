using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters
{
    [RequireComponent(typeof(Health))]
    public sealed class SegmentedHealthBar : MonoBehaviour
    {
        [SerializeField, Range(1, 30)] private int segments = 10;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.4f, 0f);
        private Health health;
        private GUIStyle title;

        public int SegmentCount => segments;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        public float GetSegmentFill(int index)
        {
            if (index < 0 || index >= segments)
                return 0f;
            return Mathf.Clamp01((float)health.CurrentHealth * segments / health.MaxHealth - index);
        }

        private void OnGUI()
        {
            Camera camera = Camera.main;
            if (camera == null || health.IsDead)
                return;

            // 월드의 머리 위 위치를 화면 좌표로 바꾸어 이동하는 몬스터를 따라갑니다.
            Vector3 screen = camera.WorldToScreenPoint(transform.position + worldOffset);
            if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width ||
                screen.y < 0f || screen.y > Screen.height)
                return;

            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            const float width = 150f;
            float x = screen.x - width * 0.5f;
            float y = Screen.height - screen.y - 32f;
            GUI.Box(new Rect(x - 4f, y, width + 8f, 32f), GUIContent.none);
            GUI.Label(new Rect(x, y, width, 19f), $"{health.CurrentHealth} / {health.MaxHealth}", title);

            const float gap = 2f;
            float cellWidth = (width - gap * (segments - 1)) / segments;
            float ratio = (float)health.CurrentHealth / health.MaxHealth;
            Color color = ratio > 0.5f ? new Color(0.7f, 0.35f, 0.95f) :
                ratio > 0.25f ? new Color(1f, 0.7f, 0.2f) : new Color(1f, 0.25f, 0.2f);
            for (int i = 0; i < segments; i++)
            {
                Rect cell = new Rect(x + i * (cellWidth + gap), y + 20f, cellWidth, 9f);
                Paint(cell, new Color(0.12f, 0.12f, 0.15f));
                float fill = GetSegmentFill(i);
                if (fill > 0f)
                    Paint(new Rect(cell.x + 1f, cell.y + 1f, (cell.width - 2f) * fill, cell.height - 2f), color);
            }
        }

        private static void Paint(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
