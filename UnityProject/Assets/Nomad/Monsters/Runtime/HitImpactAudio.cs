using UnityEngine;

namespace Nomad.Monsters
{
    public static class HitImpactAudio
    {
        private static AudioClip hit, crack;
        private static float nextMonsterSound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (hit != null) Object.Destroy(hit);
            if (crack != null) Object.Destroy(crack);
            hit = crack = null;
            nextMonsterSound = 0;
        }

        public static void Play(AudioSource source, bool armorBreak, float volume)
        {
            if (source == null || volume <= 0) return;
            if (hit == null) hit = Build(false);
            if (crack == null) crack = Build(true);
            source.PlayOneShot(armorBreak ? crack : hit, Mathf.Clamp01(volume));
        }

        public static void PlayMonster(AudioSource source, bool critical)
        {
            var settings = CombatFeedbackSettings.Current;
            if (!settings.MonsterSound || Time.time < nextMonsterSound) return;

            // 광역 적중으로 같은 프레임에 여러 소리가 겹쳐 커지는 것을 제한합니다.
            nextMonsterSound = Time.time + 0.04f;
            Play(source, false, settings.MonsterVolume * (critical ? 1.25f : 1f));
        }

        private static AudioClip Build(bool breaking)
        {
            const int rate = 22050;
            float seconds = breaking ? 0.3f : 0.12f;
            var samples = new float[Mathf.CeilToInt(rate * seconds)];
            var random = new System.Random(breaking ? 214 : 215);
            for (int index = 0; index < samples.Length; index++)
            {
                float time = index / (float)rate;
                float noise = (float)random.NextDouble() * 2 - 1;
                float bass = Mathf.Sin(time * Mathf.PI * 2 * (breaking ? 75 : 120));
                float metal = Mathf.Sin(time * Mathf.PI * 2 * 540) * Mathf.Exp(-time * 45);
                samples[index] = Mathf.Clamp((noise * 0.45f + bass * 0.4f + metal * 0.15f) * Mathf.Exp(-time * (breaking ? 15 : 35)), -1, 1);
            }

            AudioClip clip = AudioClip.Create(breaking ? "Armor fracture" : "Combat impact", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
