using System;

namespace Nomad.Monsters.Demo
{
    [Serializable]
    public sealed class BossCombatTuning
    {
        public float SlamRadius = 12;
        public float SlamWindup = 1.15f;

        public int RadialCount = 12;
        public float RadialRadius = 1.25f;
        public float RadialSpeed = 42;
        public float RadialRange = 34;

        public float BreathRange = 18;
        public float BreathAngle = 110;
        public float BreathSafeRadius = 3.5f;
        public float BreathTravelSeconds = 1.3f;

        public int FirePoolCount = 4;
        public float FirePoolRadius = 2.8f;

        public int RainWaves = 20;
        public int RainPerWave = 3;
        public float RainRadius = 1.65f;
        public float RainFallSeconds = 0.85f;
        public float RainInterval = 0.35f;

        public float ChargeInterval = 22;
        public float ChargeWindup = 2.8f;
        public float ChargeSpeed = 15;
    }
}
