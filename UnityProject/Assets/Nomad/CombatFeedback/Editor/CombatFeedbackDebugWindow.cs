using System;
using Nomad.Monsters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nomad.CombatFeedback.Editor
{
    public sealed class CombatFeedbackDebugWindow : EditorWindow
    {
        private PlayerDamageFeedback player;
        private MonsterHitFeedback monster;
        private int previewDamage = 20;
        private bool critical;
        private Label status;
        private IVisualElementScheduledItem burst;

        [MenuItem("Nomad/Debug/전투 피드백")]
        public static void Open() => GetWindow<CombatFeedbackDebugWindow>("전투 피드백");

        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            burst?.Pause();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            burst?.Pause();
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
                CreateGUI();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            minSize = new Vector2(400, 560);
            var scroll = new ScrollView();
            scroll.style.paddingLeft = scroll.style.paddingRight = 12;
            rootVisualElement.Add(scroll);
            scroll.Add(new HelpBox("Play 중 실시간 조절합니다. 미리보기는 HP를 소모하지 않습니다. 설정은 Play 재시작 시 기본값으로 돌아갑니다.", HelpBoxMessageType.Info));

            var settings = CombatFeedbackSettings.Current;
            var playerField = new ObjectField("플레이어") { objectType = typeof(PlayerDamageFeedback), allowSceneObjects = true, value = player };
            playerField.RegisterValueChangedCallback(e => player = e.newValue as PlayerDamageFeedback);
            scroll.Add(playerField);
            var monsterField = new ObjectField("몬스터") { objectType = typeof(MonsterHitFeedback), allowSceneObjects = true, value = monster };
            monsterField.RegisterValueChangedCallback(e => monster = e.newValue as MonsterHitFeedback);
            scroll.Add(monsterField);
            scroll.Add(new Button(() =>
            {
                playerField.value = FindFirstObjectByType<PlayerDamageFeedback>();
                monsterField.value = FindFirstObjectByType<MonsterHitFeedback>();
            }) { text = "현재 씬에서 대상 찾기" });

            var damage = new IntegerField("테스트 피해량") { value = previewDamage };
            damage.RegisterValueChangedCallback(e =>
            {
                previewDamage = Mathf.Clamp(e.newValue, 1, 9999);
                damage.SetValueWithoutNotify(previewDamage);
            });
            scroll.Add(damage);
            AddToggle(scroll, "치명타 미리보기", critical, value => critical = value);
            var previews = new VisualElement();
            previews.style.flexDirection = FlexDirection.Row;
            previews.Add(new Button(() => Preview(true)) { text = "플레이어 피격" });
            previews.Add(new Button(() => Preview(false)) { text = "몬스터 적중" });
            previews.Add(new Button(PreviewBurst) { text = "플레이어 연속 3회" });
            scroll.Add(previews);

            var playerOptions = new Foldout { text = "플레이어 피격", value = true };
            scroll.Add(playerOptions);
            AddToggle(playerOptions, "붉은 비네트", settings.PlayerVignette, value => settings.PlayerVignette = value);
            AddToggle(playerOptions, "데미지 숫자", settings.PlayerNumbers, value => settings.PlayerNumbers = value);
            AddToggle(playerOptions, "피격음", settings.PlayerSound, value => settings.PlayerSound = value);
            AddToggle(playerOptions, "카메라 충격", settings.PlayerShake, value => settings.PlayerShake = value);

            AddSlider(playerOptions, "비네트 등장 (초)", settings.VignetteAttack, 0.01f, 0.2f, value => settings.VignetteAttack = value);
            AddSlider(playerOptions, "비네트 유지 (초)", settings.VignetteHold, 0, 0.3f, value => settings.VignetteHold = value);
            AddSlider(playerOptions, "비네트 복귀 (초)", settings.VignetteRelease, 0.05f, 1f, value => settings.VignetteRelease = value);
            AddSlider(playerOptions, "비네트 최대 강도", settings.VignetteMaximum, 0, 0.85f, value => settings.VignetteMaximum = value);

            var color = new ColorField("비네트 색") { value = settings.VignetteColor, showAlpha = false };
            color.RegisterValueChangedCallback(e => settings.VignetteColor = e.newValue);
            playerOptions.Add(color);

            AddSlider(playerOptions, "카메라 충격 강도", settings.PlayerShakeStrength, 0, 0.2f, value => settings.PlayerShakeStrength = value);
            AddSlider(playerOptions, "피격음 크기", settings.PlayerVolume, 0, 1, value => settings.PlayerVolume = value);

            var monsterOptions = new Foldout { text = "몬스터 적중", value = true };
            scroll.Add(monsterOptions);
            AddToggle(monsterOptions, "피격 반짝임", settings.MonsterFlash, value => settings.MonsterFlash = value);
            AddToggle(monsterOptions, "데미지 숫자", settings.MonsterNumbers, value => settings.MonsterNumbers = value);
            AddToggle(monsterOptions, "파편", settings.MonsterShards, value => settings.MonsterShards = value);
            AddToggle(monsterOptions, "타격음", settings.MonsterSound, value => settings.MonsterSound = value);
            AddToggle(monsterOptions, "카메라 충격", settings.MonsterShake, value => settings.MonsterShake = value);

            AddSlider(monsterOptions, "일반 반짝임 (초)", settings.FlashSeconds, 0.02f, 0.2f, value => settings.FlashSeconds = value);
            AddSlider(monsterOptions, "치명타 반짝임 (초)", settings.CriticalFlashSeconds, 0.02f, 0.25f, value => settings.CriticalFlashSeconds = value);
            AddSlider(monsterOptions, "카메라 충격 강도", settings.MonsterShakeStrength, 0, 0.15f, value => settings.MonsterShakeStrength = value);
            AddSlider(monsterOptions, "치명타 충격 배율", settings.CriticalShakeMultiplier, 1, 2, value => settings.CriticalShakeMultiplier = value);
            AddSlider(monsterOptions, "타격음 크기", settings.MonsterVolume, 0, 1, value => settings.MonsterVolume = value);

            scroll.Add(new Button(ClearAll) { text = "재생 중인 효과 지우기" });
            scroll.Add(new Button(() => { ClearAll(); CombatFeedbackSettings.Reset(); CreateGUI(); }) { text = "설정 기본값 복원" });
            scroll.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(CombatFeedbackSettings.Current, true)) { text = "현재 설정 JSON 복사" });
            status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(status);
            status.schedule.Execute(UpdateStatus).Every(100);
        }

        private static void AddToggle(VisualElement parent, string label, bool initial, Action<bool> apply)
        {
            var field = new Toggle(label) { value = initial };
            field.RegisterValueChangedCallback(e => apply(e.newValue));
            parent.Add(field);
        }

        private static void AddSlider(VisualElement parent, string label, float initial, float low, float high, Action<float> apply)
        {
            var field = new Slider(label, low, high) { value = initial, showInputField = true };
            field.RegisterValueChangedCallback(e => apply(Mathf.Clamp(e.newValue, low, high)));
            parent.Add(field);
        }

        private void Preview(bool hurtPlayer)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.timeScale <= 0) return;
            if (hurtPlayer) { if (player != null && player.gameObject.scene.IsValid()) player.PreviewDamage(previewDamage); }
            else if (monster != null && monster.gameObject.scene.IsValid()) monster.PreviewHit(previewDamage, critical);
        }

        private void PreviewBurst()
        {
            burst?.Pause();
            int remaining = 3;
            burst = rootVisualElement.schedule.Execute(() =>
            {
                Preview(true);
                if (--remaining == 0) burst.Pause();
            }).Every(120);
        }

        private void ClearAll()
        {
            burst?.Pause();
            foreach (var item in FindObjectsByType<PlayerDamageFeedback>(FindObjectsSortMode.None)) item.ClearFeedback();
            foreach (var item in FindObjectsByType<MonsterHitFeedback>(FindObjectsSortMode.None)) item.ClearFeedback();
            foreach (var item in FindObjectsByType<HitCameraImpulse>(FindObjectsSortMode.None)) item.ClearFeedback();
        }

        private void UpdateStatus()
        {
            if (!EditorApplication.isPlaying) { status.text = "Play 후 대상 찾기를 누르세요."; return; }
            var overlay = player != null ? player.GetComponent<CombatFeedbackOverlay>() : null;
            status.text = $"플레이어: {(player != null ? player.name : "선택 필요")} · 비네트 {(overlay != null ? overlay.VignetteAlpha : 0):0.00}\n" +
                $"몬스터: {(monster != null ? monster.name : "선택 필요")} · 최근 피해 {(monster != null ? monster.LastDamage : 0)} · 숫자 {(monster != null ? monster.ActiveNumberCount : 0)}";
        }
    }
}
