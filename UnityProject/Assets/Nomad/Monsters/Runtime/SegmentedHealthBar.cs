using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters
{
    [RequireComponent(typeof(Health))]
    public sealed class SegmentedHealthBar : MonoBehaviour
    {
        [SerializeField, Range(1, 30)] private int segments = 10;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.4f, 0f);
        public bool Compact { get; set; }
        public bool Boss { get; set; }
        public Vector3 WorldOffset { set => worldOffset = value; }
        private Health health;
        private GUIStyle title;
        private float delayedRatio = 1f;
        private float holdTrail;
        private float lastRatio = 1f;
        public float DamageTrailRatio => delayedRatio;

        public int SegmentCount => segments;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (!Boss) return;
            float ratio = (float)health.CurrentHealth / health.MaxHealth;
            if (ratio < lastRatio) holdTrail = 0.5f;
            lastRatio = ratio;
            holdTrail -= Time.deltaTime;
            if (holdTrail <= 0f) delayedRatio = Mathf.MoveTowards(delayedRatio, ratio, Time.deltaTime * 0.45f);
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

            if (Boss)
            {
                DrawBoss();
                return;
            }

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

            float width = Compact ? 78f : 150f;
            float x = screen.x - width * 0.5f;
            float y = Screen.height - screen.y - (Compact ? 16f : 32f);
            GUI.Box(new Rect(x - 4f, y, width + 8f, Compact ? 16f : 32f), GUIContent.none);
            if (!Compact)
                GUI.Label(new Rect(x, y, width, 19f), $"{health.CurrentHealth} / {health.MaxHealth}", title);

            const float gap = 2f;
            float cellWidth = (width - gap * (segments - 1)) / segments;
            float ratio = (float)health.CurrentHealth / health.MaxHealth;
            Color color = ratio > 0.5f ? new Color(0.7f, 0.35f, 0.95f) :
                ratio > 0.25f ? new Color(1f, 0.7f, 0.2f) : new Color(1f, 0.25f, 0.2f);
            for (int i = 0; i < segments; i++)
            {
                Rect cell = new Rect(x + i * (cellWidth + gap), y + (Compact ? 3f : 20f), cellWidth, 9f);
                Paint(cell, new Color(0.12f, 0.12f, 0.15f));
                float fill = GetSegmentFill(i);
                if (fill > 0f)
                    Paint(new Rect(cell.x + 1f, cell.y + 1f, (cell.width - 2f) * fill, cell.height - 2f), color);
            }
        }

        private void DrawBoss()
        {
            float width = Mathf.Min(760f, Screen.width * 0.58f);
            float x = (Screen.width - width) * 0.5f;
            Color gold = new Color(0.85f, 0.65f, 0.28f);
            Paint(new Rect(x - 10f, 12f, width + 20f, 74f), new Color(0.05f, 0.04f, 0.06f, 0.94f));
            Paint(new Rect(x - 10f, 12f, width + 20f, 2f), gold);
            if (title == null) title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            title.fontSize = Mathf.Clamp(Mathf.RoundToInt(width / 32f), 14, 22);
            title.normal.textColor = gold;
            GUI.Label(new Rect(x, 16f, width, 28f), "ASHEN TYRANT  -  FINAL GUARDIAN", title);
            Rect bar = new Rect(x, 48f, width, 17f);
            Paint(bar, new Color(0.16f, 0.08f, 0.1f));
            Paint(new Rect(bar.x, bar.y, width * delayedRatio, bar.height), gold);
            Paint(new Rect(bar.x, bar.y, width * health.CurrentHealth / health.MaxHealth, bar.height), new Color(0.72f, 0.09f, 0.08f));
            for (int i = 1; i < 4; i++) Paint(new Rect(x + width * i / 4f, bar.y, 2f, bar.height), new Color(0.12f, 0.05f, 0.07f));
            title.fontSize = 12;
            title.normal.textColor = Color.white;
            GUI.Label(new Rect(x, 64f, width, 19f), $"{health.CurrentHealth:N0} / {health.MaxHealth:N0}", title);
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
