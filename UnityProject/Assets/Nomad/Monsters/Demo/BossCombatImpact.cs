using Nomad.Monsters;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public static class BossCombatImpact
    {
        private static float next;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => next = 0;
        public static void Pulse(float strength)
        {
            // 화염우 여러 발이 한 프레임에 닿아도 흔들림은 중첩하지 않습니다.
            if (Time.time < next) return;
            next = Time.time + 0.18f;
            HitCameraImpulse.Pulse(strength, 0.13f);
        }
    }
}
