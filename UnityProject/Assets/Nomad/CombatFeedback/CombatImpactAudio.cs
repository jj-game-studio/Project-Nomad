using UnityEngine;

namespace Nomad.CombatFeedback
{
    public static class CombatImpactAudio
    {
        public static void Play(AudioSource source, bool armorBreak, float volume = 0.3f)
        {
            Nomad.Monsters.HitImpactAudio.Play(source, armorBreak, volume);
        }
    }
}
