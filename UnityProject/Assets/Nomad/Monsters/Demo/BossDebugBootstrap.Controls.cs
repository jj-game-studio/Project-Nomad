using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Player;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossDebugBootstrap
    {
        private bool godMode;
        private Vector2 scroll;
        private int selected;
        private Rect PanelRect => new Rect(12, 80, 312, Mathf.Min(740, Screen.height - 92));
        public bool GodMode
        {
            get => godMode;
            set { godMode = value; if (Player != null) Player.GetComponent<Health>().IsInvulnerable = value; }
        }

        private void OnGUI()
        {
            if (!Ready || Player == null) return;
            Font previousFont = GUI.skin.font;
            GUI.skin.font = Resources.Load<Font>("ShopFonts/Jua-Regular");
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("보스 전용 테스트");
            GUILayout.Label("F 패링 · Shift 구르기 · C 스킬 · R 재시작");
            GodMode = GUILayout.Toggle(GodMode, "플레이어 무적");
            Boss.AutomaticPatterns = GUILayout.Toggle(Boss.AutomaticPatterns, "자동 패턴 (끄면 단발 시험)");
            if (GUILayout.Button("체력·마나 회복"))
            { Player.GetComponent<Health>().RestoreFull(); Player.GetComponent<PlayerMana>().RestoreFull(); }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("검 +1")) Equip("demo-sword");
            if (GUILayout.Button("활 +1")) Equip("weapon-shortbow");
            if (GUILayout.Button("지팡이 +1")) Equip("weapon-apprentice-staff");
            GUILayout.EndHorizontal();

            selected = GUILayout.SelectionGrid(selected, new[] { "기본 공격", "점프 강타", "전방향 화염탄", "브레스", "불지대", "돌진", "화염우" }, 2);
            DrawParameters((DragonPattern)selected);
            GUI.enabled = Boss.Introduced && !Boss.GetComponent<Health>().IsDead;
            if (GUILayout.Button("선택 패턴 실행")) Boss.BeginPattern((DragonPattern)selected);
            GUI.enabled = true;
            GUILayout.Label(Boss.Enraged ? "광폭 · 속도 +30% / 피해 +40%" : "체력 25% 이하: 광폭");
            if (GUILayout.Button("광폭 시험 (보스 체력 25%)"))
            {
                var health = Boss.GetComponent<Health>();
                int damage = health.CurrentHealth - Mathf.FloorToInt(health.MaxHealth * 0.25f);
                if (damage > 0) health.TryApplyDamage(damage, DamageKind.Fall);
            }
            if (GUILayout.Button("보스전 재시작")) StartCoroutine(Spawn());
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.skin.font = previousFont;
        }

        private void DrawParameters(DragonPattern pattern)
        {
            BossCombatTuning t = Boss.Tuning;
            switch (pattern)
            {
                case DragonPattern.Basic:
                    t.BasicRange = Slider("사거리 (m)", t.BasicRange, 2, 8);
                    break;
                case DragonPattern.Circle:
                    t.SlamRadius = Slider("범위 (m)", t.SlamRadius, 6, 20);
                    t.SlamWindup = Slider("준비 (초)", t.SlamWindup, 0.5f, 3);
                    break;
                case DragonPattern.RadialFireballs:
                    t.RadialCount = Mathf.RoundToInt(Slider("개수", t.RadialCount, 24, 120));
                    t.RadialRadius = Slider("탄 반경 (m)", t.RadialRadius, 1, 3);
                    t.RadialSpeed = Slider("속도 (m/s)", t.RadialSpeed, 15, 65);
                    t.RadialRange = Slider("사거리 (m)", t.RadialRange, 15, 42);
                    break;
                case DragonPattern.Breath:
                    t.BreathRange = Slider("사거리 (m)", t.BreathRange, 10, 26);
                    t.BreathAngle = Slider("각도", t.BreathAngle, 60, 150);
                    t.BreathSafeRadius = Slider("근접 사각지대 (m)", t.BreathSafeRadius, 2, 6);
                    t.BreathTravelSeconds = Slider("불꽃 도달 시간 (초)", t.BreathTravelSeconds, 0.5f, 2);
                    break;
                case DragonPattern.FirePool:
                    t.FirePoolCount = Mathf.RoundToInt(Slider("개수", t.FirePoolCount, 1, 8));
                    t.FirePoolRadius = Slider("범위 (m)", t.FirePoolRadius, 1.5f, 5);
                    break;
                case DragonPattern.Charge:
                    t.ChargeSpeed = Slider("속도 (m/s)", t.ChargeSpeed, 10, 24);
                    t.ChargeWindup = Slider("준비 (초)", t.ChargeWindup, 1.5f, 5);
                    t.ChargeInterval = Slider("간격 (초)", t.ChargeInterval, 10, 40);
                    break;
                case DragonPattern.Rain:
                    t.RainWaves = Mathf.RoundToInt(Slider("낙하 횟수", t.RainWaves, 8, 30));
                    t.RainPerWave = Mathf.RoundToInt(Slider("동시 개수", t.RainPerWave, 1, 5));
                    t.RainRadius = Slider("범위 (m)", t.RainRadius, 1, 3);
                    t.RainFallSeconds = Slider("낙하 시간 (초)", t.RainFallSeconds, 0.6f, 1.5f);
                    t.RainInterval = Slider("낙하 간격 (초)", t.RainInterval, 0.25f, 0.7f);
                    break;
            }
            GUILayout.Label("다음 패턴 실행부터 적용 · R로 초기화");
        }

        private static float Slider(string label, float value, float minimum, float maximum)
        {
            GUILayout.Label(label + "  " + value.ToString("0.00"));
            return GUILayout.HorizontalSlider(value, minimum, maximum);
        }
    }
}
