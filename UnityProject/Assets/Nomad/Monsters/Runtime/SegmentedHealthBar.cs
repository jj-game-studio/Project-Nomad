using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters
{
    [RequireComponent(typeof(Health))]
    public sealed class SegmentedHealthBar : MonoBehaviour
    {
        [SerializeField, Range(1, 30)] private int segments = 10;
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
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            float width = Mathf.Min(560f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 80f;
            GUI.Box(new Rect(x - 10f, y - 4f, width + 20f, 67f), GUIContent.none);
            GUI.Label(new Rect(x, y, width, 28f), $"ELITE  {health.CurrentHealth} / {health.MaxHealth}", title);

            const float gap = 3f;
            float cellWidth = (width - gap * (segments - 1)) / segments;
            float ratio = (float)health.CurrentHealth / health.MaxHealth;
            Color color = ratio > 0.5f ? new Color(0.7f, 0.35f, 0.95f) :
                ratio > 0.25f ? new Color(1f, 0.7f, 0.2f) : new Color(1f, 0.25f, 0.2f);
            for (int i = 0; i < segments; i++)
            {
                Rect cell = new Rect(x + i * (cellWidth + gap), y + 32f, cellWidth, 22f);
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
