using Nomad.CombatFeedback;
using Nomad.Monsters;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(300)]
    public sealed class BossJumpSlam : MonoBehaviour
    {
        public const float LeapSeconds = 0.8f;
        public int Landings { get; private set; }
        public bool Airborne { get; private set; }
        private DragonBossCombat boss;
        private BossArena arena;
        private Transform body;
        private Vector3 resting;
        private CapsuleCollider capsule;
        private Vector3 restingCollider;
        private Behaviour feet;
        private float since;
        private bool jumping, landed;

        public void Configure(DragonBossCombat owner, BossArena room)
        {
            boss = owner;
            arena = room;
            body = transform.Find("Body");
            resting = body.localPosition;
            capsule = GetComponent<CapsuleCollider>();
            restingCollider = capsule.center;
            feet = GetComponentInChildren<DragonLegMotion>();
        }

        private void LateUpdate()
        {
            if (boss == null || body == null) return;
            bool active = boss.Pattern == DragonPattern.Circle && boss.Phase == DragonPhase.Active;
            if (!active) { ResetPose(); return; }
            if (!jumping) { jumping = true; landed = false; since = Time.time; }
            float progress = Mathf.Clamp01((Time.time - since) / LeapSeconds);
            Airborne = progress < 1;
            if (feet != null) feet.enabled = !Airborne;
            Vector3 lift = Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 2.8f);
            body.localPosition = resting + lift;
            capsule.center = restingCollider + lift;
            if (progress < 1 || landed) return;

            landed = true;
            Landings++;
            BossGroundCracks.Create(boss.Effects, arena, transform.position, DragonBossCombat.CircleRadius);
            HitCameraImpulse.Pulse(0.2f, 0.2f);
            CombatImpactAudio.Play(GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>(), true, 0.5f);
        }

        private void ResetPose()
        {
            jumping = false;
            Airborne = false;
            if (body != null) body.localPosition = resting;
            if (capsule != null) capsule.center = restingCollider;
            if (feet != null) feet.enabled = true;
        }

        private void OnDisable() => ResetPose();
    }
}
