using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(375)]
    public sealed class BossBodySweepMotion : MonoBehaviour
    {
        private DragonBossCombat boss;
        private Transform root;
        private Quaternion original, applied;
        private bool posed;
        private float weight, releaseAt;
        private bool released;

        public void Configure(DragonBossCombat owner)
        {
            boss = owner;
            root = System.Array.Find(GetComponentsInChildren<Transform>(), bone => bone.name == "base");
        }

        private void Update() => Restore();

        private void Restore()
        {
            if (posed && root != null && Quaternion.Angle(root.localRotation, applied) < 0.05f) root.localRotation = original;
            posed = false;
        }

        private void LateUpdate()
        {
            if (boss == null || root == null) return;
            bool sweep = boss.Pattern == DragonPattern.BodySweep || boss.Pattern == DragonPattern.RedSweep;
            bool winding = sweep && boss.Phase == DragonPhase.Windup;
            if (winding) released = false;
            if (sweep && boss.Phase == DragonPhase.Active && !released) { releaseAt = Time.time; released = true; }
            bool swinging = sweep && released && Time.time - releaseAt < 0.45f;
            if (boss.Phase == DragonPhase.Stunned || boss.Phase == DragonPhase.Dead || boss.EnrageTransitioning)
            { weight = 0; return; }
            weight = Mathf.MoveTowards(weight, winding || swinging ? 1 : 0, Time.deltaTime * 6);
            if (weight <= 0) return;
            float twist = winding ? -28 * Mathf.Clamp01(1 - boss.Remaining / boss.Tuning.BodySweepWindup)
                : Mathf.Lerp(-28, 65, Mathf.SmoothStep(0, 1, (Time.time - releaseAt) / 0.35f));
            original = root.localRotation;
            root.rotation = Quaternion.AngleAxis(twist * weight, transform.up) * root.rotation;
            applied = root.localRotation;
            posed = true;
        }

        private void OnDisable() { Restore(); weight = 0; }
    }
}
