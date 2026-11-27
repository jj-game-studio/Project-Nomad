using UnityEngine;

namespace Nomad.Monsters
{
    // 씬/에셋을 바꾸지 않는 세션 설정입니다. Play를 새로 시작하면 기본값으로 복원합니다.
    [System.Serializable]
    public sealed class CombatFeedbackSettings
    {
        public static CombatFeedbackSettings Current { get; private set; } = new();

        public bool MonsterFlash = true, MonsterNumbers = true, MonsterShards = true;
        public bool MonsterSound = true, MonsterShake = true;
        public float FlashSeconds = 0.09f, CriticalFlashSeconds = 0.13f;
        public float MonsterShakeStrength = 0.045f, CriticalShakeMultiplier = 1.6f;
        public float MonsterVolume = 0.22f;

        public bool PlayerVignette = true, PlayerNumbers = true, PlayerSound = true, PlayerShake = true;
        public float VignetteAttack = 0.04f, VignetteHold = 0.05f, VignetteRelease = 0.35f;
        public float VignetteMaximum = 0.6f;
        public Color VignetteColor = new(0.72f, 0.015f, 0.035f);
        public float PlayerShakeStrength = 0.1f, PlayerVolume = 0.32f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => Current = new CombatFeedbackSettings();
    }
}
