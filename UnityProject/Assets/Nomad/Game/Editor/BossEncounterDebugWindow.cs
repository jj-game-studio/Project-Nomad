using Nomad.Combat;
using Nomad.Monsters.Demo;
using UnityEditor;
using UnityEngine;

namespace Nomad.Game.Editor
{
    [InitializeOnLoad]
    public sealed class BossEncounterDebugWindow : EditorWindow
    {
        private BossDebugBootstrap debug;
        private DragonBossCombat boss;
        private Vector2 scroll;
        private int selected;
        private bool utilityReady;
        private static readonly DragonPattern[] Choices = { DragonPattern.Circle, DragonPattern.RadialFireballs,
            DragonPattern.Breath, DragonPattern.Charge, DragonPattern.Rain,
            DragonPattern.BodySweep, DragonPattern.RedSweep, DragonPattern.Rockfall, DragonPattern.OrbitFlames, DragonPattern.LavaEruption,
            DragonPattern.TrackingFireball, DragonPattern.AimedBreath, DragonPattern.InnerBlast, DragonPattern.OuterBlast };
        private static readonly string[] Names = { "점프 강타", "전방향 화염탄", "백스텝 브레스", "돌진", "화염우",
            "노랑 근접 패링", "빨강 근접 회피", "독립 바위 낙하", "3회 거리 압박", "파괴 후 용암 분출",
            "추적 화염구", "조준 고정 직선 브레스", "바깥 안전 폭발", "안쪽 안전 폭발" };

        [MenuItem("Nomad/Debug/보스 전투")]
        public static void Open()
        {
            var window = System.Array.Find(Resources.FindObjectsOfTypeAll<BossEncounterDebugWindow>(), item => item != null);
            if (window == null || !window.utilityReady)
            {
                if (window != null) window.Close();
                window = CreateInstance<BossEncounterDebugWindow>();
                window.titleContent = new GUIContent("보스 전투 디버거");
                window.position = new Rect(40, 100, 410, 740);
                window.ShowUtility();
                window.utilityReady = true;
            }
            window.Focus();
        }

        [MenuItem("Window/Nomad/보스 전투 디버거")]
        private static void OpenFromWindow() => Open();

        static BossEncounterDebugWindow()
        {
            EditorApplication.delayCall += OpenForBossScene;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) => OpenForBossScene();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += OpenForBossScene;
            };
        }

        private static void OpenForBossScene()
        {
            if (Application.isBatchMode) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.EndsWith("/BossDebug.unity")) Open();
        }
        private void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }

        private void OnGUI()
        {
            minSize = new Vector2(380, 600);
            EditorGUILayout.LabelField("보스 전투 디버거", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("현재 씬", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            EditorGUILayout.LabelField("프로젝트", new System.IO.DirectoryInfo(Application.dataPath).Parent.FullName, EditorStyles.miniLabel);
            if (!EditorApplication.isPlaying) { EditorGUILayout.HelpBox("BossDebug 또는 필드 보스전 Play 중 조작합니다.", MessageType.Info); return; }
            if (boss == null) boss = FindFirstObjectByType<DragonBossCombat>();
            if (debug == null) debug = FindFirstObjectByType<BossDebugBootstrap>();
            if (boss == null) { EditorGUILayout.HelpBox("보스 조우를 기다립니다.", MessageType.Info); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("보스 상태", boss.Pattern + " / " + boss.Phase);
            boss.AutomaticPatterns = EditorGUILayout.Toggle("자동 패턴", boss.AutomaticPatterns);
            boss.AutomaticRockfall = EditorGUILayout.Toggle("독립 바위 자동 낙하", boss.AutomaticRockfall);
            if (debug != null && debug.Ready)
            {
                debug.GodMode = EditorGUILayout.Toggle("플레이어 무적", debug.GodMode);
                debug.UnlimitedResources = EditorGUILayout.Toggle("마나·스태미나 무제한", debug.UnlimitedResources);
                if (GUILayout.Button("체력·마나 회복")) debug.RestorePlayer();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("검 +1")) debug.Equip("demo-sword");
                if (GUILayout.Button("활 +1")) debug.Equip("weapon-shortbow");
                if (GUILayout.Button("지팡이 +1")) debug.Equip("weapon-apprentice-staff");
                GUILayout.EndHorizontal();
            }
            selected = GUILayout.SelectionGrid(selected, Names, 2);
            DrawParameters(Choices[selected]);
            using (new EditorGUI.DisabledScope(!boss.Introduced || boss.GetComponent<Health>().IsDead ||
                Choices[selected] == DragonPattern.LavaEruption && boss.GetComponent<BossArmor>().BreakCount == 0))
                if (GUILayout.Button("선택 공격 실행")) boss.BeginPattern(Choices[selected]);
            var arena = FindFirstObjectByType<BossArena>();
            EditorGUILayout.LabelField("바위 (낙하 중 포함)", ((arena != null ? arena.RockCount : 0) + boss.PendingRocks) + "/6");
            if (GUILayout.Button("갑옷 1층 파괴 시험")) boss.GetComponent<BossArmor>().DamageArmor(BossArmor.DurabilityPerLayer);
            if (debug != null)
            {
                if (GUILayout.Button("광폭 시험")) debug.EnterEnrage();
                if (GUILayout.Button("보스전 재시작")) debug.Restart();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawParameters(DragonPattern pattern)
        {
            BossCombatTuning t = boss.Tuning;
            switch (pattern)
            {
                case DragonPattern.Circle:
                    t.SlamRadius = Slider("범위 (m)", t.SlamRadius, 6, 20);
                    t.SlamWindup = Slider("준비 (초)", t.SlamWindup, 0.5f, 3);
                    break;
                case DragonPattern.RadialFireballs:
                    t.RadialCount = Mathf.RoundToInt(Slider("개수", t.RadialCount, 6, 32));
                    t.RadialRadius = Slider("탄 반경 (m)", t.RadialRadius, 1, 3);
                    t.RadialSpeed = Slider("속도 (m/s)", t.RadialSpeed, 15, 65);
                    t.RadialRange = Slider("사거리 (m)", t.RadialRange, 15, 42);
                    break;
                case DragonPattern.Breath:
                    t.BreathPreparation = Slider("착지 후 브레스 준비 (초)", t.BreathPreparation, 0.7f, 2.5f);
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
                    t.ChargeInterval = Slider("간격 (초)", t.ChargeInterval, 25, 55);
                    break;
                case DragonPattern.Rockfall:
                    t.RockfallInterval = Slider("낙하 간격 (초)", t.RockfallInterval, 8, 25);
                    t.RockfallRadius = Slider("낙하 피해 범위 (m)", t.RockfallRadius, 2, 4);
                    t.RockfallWarning = Slider("낙하 전조 (초)", t.RockfallWarning, 0.8f, 2);
                    GUILayout.Label("낙하 바위 " + boss.GetComponentInParent<BossArena>().RockCount + "/" + BossArena.MaximumRockCount);
                    break;
                case DragonPattern.OrbitFlames:
                    t.OrbitRadius = Slider("밀착 공전 거리 (m)", t.OrbitRadius, 2.4f, 4);
                    t.OrbitKnockback = Slider("구체 밀침 (m)", t.OrbitKnockback, 0.5f, 3);
                    t.OrbitSpeed = Slider("공전 속도 (도/초)", t.OrbitSpeed, 8, 28);
                    break;
                case DragonPattern.LavaEruption:
                    t.LavaEruptionRadius = Slider("분출 범위 (m)", t.LavaEruptionRadius, 1.5f, 4);
                    t.LavaEruptionWarning = Slider("분출 전조 (초)", t.LavaEruptionWarning, 0.6f, 2);
                    break;
                case DragonPattern.BodySweep:
                    t.BodySweepRadius = Slider("견제 범위 (m)", t.BodySweepRadius, 4, 10);
                    t.BodySweepWindup = Slider("전조 (초)", t.BodySweepWindup, 0.8f, 2);
                    break;
                case DragonPattern.Rain:
                    t.RainWaves = Mathf.RoundToInt(Slider("낙하 횟수", t.RainWaves, 8, 30));
                    t.RainPerWave = Mathf.RoundToInt(Slider("동시 개수", t.RainPerWave, 1, 5));
                    t.RainRadius = Slider("범위 (m)", t.RainRadius, 1, 3);
                    t.RainFallSeconds = Slider("낙하 시간 (초)", t.RainFallSeconds, 0.6f, 1.5f);
                    t.RainInterval = Slider("낙하 간격 (초)", t.RainInterval, 0.25f, 0.7f);
                    break;
            }
            t.RecoverySeconds = Slider("패턴 사이 휴식 (초)", t.RecoverySeconds, 0.3f, 2);
            if (pattern == DragonPattern.Rain) t.EnragedRainPoolChance = Slider("광폭 장판 확률", t.EnragedRainPoolChance, 0, 1);
            GUILayout.Label("다음 패턴 실행부터 적용 · R로 초기화");
        }

        private static float Slider(string label, float value, float minimum, float maximum)
        {
            GUILayout.Label(label + "  " + value.ToString("0.00"));
            return GUILayout.HorizontalSlider(value, minimum, maximum);
        }
    }
}
