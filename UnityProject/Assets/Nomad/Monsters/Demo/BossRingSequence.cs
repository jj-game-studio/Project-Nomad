using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    // 각 회차의 예고가 끝나면 짧고 밝은 폭발만 피해를 줍니다. 안/밖/안 세 번 교대합니다.
    public sealed class BossRingSequence : MonoBehaviour
    {
        public const float WarningSeconds = 1.05f, BlastSeconds = 0.4f, GapSeconds = 0.25f;
        public const float AfterFirstWarning = BlastSeconds * 3 + (GapSeconds + WarningSeconds) * 2;
        public int RoundsReleased { get; private set; }
        private DragonBossCombat boss;
        private BossArena arena;
        private Health player;
        private Vector3 center;
        private float next, warning;
        private int round;

        public static BossRingSequence Create(Transform parent, DragonBossCombat boss, BossArena arena, Health player, float windup)
        {
            var root = new GameObject("Three alternating distance blasts");
            root.transform.SetParent(parent, false);
            var sequence = root.AddComponent<BossRingSequence>();
            sequence.boss = boss; sequence.arena = arena; sequence.player = player;
            sequence.center = boss.transform.position;
            sequence.warning = windup;
            sequence.SpawnRound();
            return sequence;
        }

        private void SpawnRound()
        {
            bool outer = round % 2 == 1;
            Band(outer ? 3.8f : 0, outer ? 7 : 3.8f);
            Band(outer ? 10 : 7, outer ? 14 : 10);
            Band(outer ? 17 : 14, outer ? 21 : 17);
            next = Time.time + warning;
        }

        private void Band(float inner, float outer)
        {
            var band = BossHazard.Create(transform, boss.transform, arena.GroundPoint(center), Quaternion.identity,
                player, outer, 360, warning, BlastSeconds / boss.Tempo, boss.ScaleDamage(18));
            band.InnerRadius = inner;
            band.JumpClearance = 0.55f;
            band.GroundHeight = arena.GroundHeight;
            band.AllowIrregular = false; band.IgnoreCover = true; band.EmitsFlame = false;
            band.WarningSeconds = warning;
            band.WarningColor = new Color(1, 0.18f, 0.025f, 0.12f);
            band.HighlightActive = true;
            band.ActiveOpacity = 0.06f;
        }

        private void Update()
        {
            if (Time.time < next) return;
            if (RoundsReleased == round)
            {
                bool outer = round % 2 == 1;
                BossRingBurst.Create(transform, arena, center, outer ? 3.8f : 0, outer ? 7 : 3.8f, BlastSeconds / boss.Tempo);
                BossRingBurst.Create(transform, arena, center, outer ? 10 : 7, outer ? 14 : 10, BlastSeconds / boss.Tempo);
                BossRingBurst.Create(transform, arena, center, outer ? 17 : 14, outer ? 21 : 17, BlastSeconds / boss.Tempo);
                RoundsReleased++;
                next = Time.time + (BlastSeconds + GapSeconds) / boss.Tempo;
                return;
            }
            if (++round >= 3) { Destroy(gameObject); return; }
            warning = WarningSeconds / boss.Tempo;
            SpawnRound();
        }
    }
}
