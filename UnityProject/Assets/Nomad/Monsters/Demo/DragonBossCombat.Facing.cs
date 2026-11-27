using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        public const float TurnSpeed = 150;
        public const float ChargeTurnSpeed = 130;
        public const float ChargeLockSeconds = 0.45f;
        public const float ChargeWindup = 2.8f;
        public const float ChargeInterval = 10;
        public bool ChargeAimLocked { get; private set; }
        public int ChargesReleased { get; private set; }
        public float ChargeRemaining => Mathf.Max(0, nextCharge - Time.time);
        private float nextCharge;
        private bool engaged;
        public float PlannedChargeDistance { get; private set; }
        private BossChargeWarning chargeWarning;

        private void UpdateFacing()
        {
            if (!engaged) { engaged = true; nextCharge = Time.time + ChargeInterval; }
            if (Phase == DragonPhase.Windup)
            {
                if (Pattern == DragonPattern.Charge)
                {
                    if (Remaining > ChargeLockSeconds)
                    {
                        Vector3 targetDirection = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
                        TurnTowards(targetDirection, ChargeTurnSpeed);
                        direction = transform.forward;
                        PlannedChargeDistance = Mathf.Clamp(targetDirection.magnitude + 2, 5, BossArena.HalfSize * 2.9f);
                    }
                    else if (!ChargeAimLocked) { ChargeAimLocked = true; direction = transform.forward; }
                    if (chargeWarning != null) chargeWarning.Refresh(transform.position, direction, PlannedChargeDistance,
                        Mathf.Clamp01((Time.time - phaseStarted) / phaseDuration), ChargeAimLocked);
                }
                else TurnTowards(direction, TurnSpeed);
                return;
            }
            if ((Phase == DragonPhase.Recovery || Phase == DragonPhase.Waiting) && agent.isOnNavMesh && agent.hasPath)
            {
                Vector3 travel = agent.desiredVelocity;
                if (travel.sqrMagnitude < 0.01f) return;
                float angle = Vector3.Angle(transform.forward, travel);
                // 크게 방향을 바꿀 때는 속도를 낮춰 옆으로 미끄러지지 않게 합니다.
                agent.speed = Mathf.Lerp(1.2f, 6, Mathf.InverseLerp(90, 15, angle));
                TurnTowards(travel, TurnSpeed);
            }
        }

        private void TurnTowards(Vector3 heading, float degreesPerSecond)
        {
            if (heading.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(heading), degreesPerSecond * Time.deltaTime);
        }
    }
}
