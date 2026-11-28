using System;

namespace Nomad.Monsters.Demo
{
    [Serializable]
    public sealed class BossCombatTuning
    {
        public float SlamRadius = 18;
        public float SlamWindup = 1.25f;
        public float RecoverySeconds = 0.85f;
        [UnityEngine.Range(0, 1)] public float EnragedRainPoolChance = 0.3f;

        public int RadialCount = 12;
        public float RadialRadius = 1.25f;
        public float RadialSpeed = 42;
        public float RadialRange = 34;

        public float BreathRange = 18;
        public float BreathAngle = 90;
        public float BreathSafeRadius = 3.5f;
        public float BreathTravelSeconds = 1.3f;

        public int FirePoolCount = 4;
        public float FirePoolRadius = 2.8f;

        public int RainWaves = 17;
        public int RainPerWave = 3;
        public float RainRadius = 1.65f;
        public float RainFallSeconds = 1f;
        public float RainInterval = 0.35f;

        public float ChargeInterval = 34;
        public float ChargeWindup = 2.8f;
        public float ChargeSpeed = 15;

        public float BodySweepRadius = 7;
        public float BodySweepWindup = 0.85f;

        public float RockfallInterval = 9;
        public float RockfallWarning = 1.1f;
        public float RockfallRadius = 2.7f;
        public float BreathBackstep = 3f;
        public float OrbitSpeed = 18;
        public float LavaEruptionRadius = 2.7f;
        public float LavaEruptionWarning = 0.9f;
        public float BreathPreparation = 1.15f;
        public float OrbitRadius = 2.85f;
        public float OrbitKnockback = 1.5f;
    }
}
