using UnityEngine;

namespace Nomad.CombatFeedback
{
    public static class CombatImpactAudio
    {
        private static AudioClip hit, crack;
        public static void Play(AudioSource source, bool armorBreak, float volume = 0.3f)
        {
            if (hit == null) hit = Build(false);
            if (crack == null) crack = Build(true);
            source.PlayOneShot(armorBreak ? crack : hit, volume);
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
