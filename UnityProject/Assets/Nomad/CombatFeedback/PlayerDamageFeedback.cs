using Nomad.Combat;
using Nomad.Monsters;
using Nomad.Player;
using UnityEngine;

namespace Nomad.CombatFeedback
{
    [RequireComponent(typeof(Health))]
    public sealed class PlayerDamageFeedback : MonoBehaviour
    {
        public const string LayerName = "Damage Reaction";
        private Health health;
        private PlayerMovement movement;
        private Animator animator;
        private CombatFeedbackOverlay overlay;
        private AudioSource sound;
        private int layer = -1;
        private float reactUntil;
        public bool Reacting => Time.time < reactUntil;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
            animator = GetComponentInChildren<Animator>();
            if (animator != null) layer = animator.GetLayerIndex(LayerName);
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0;
        }

        private void OnEnable() => health.Damaged += OnDamaged;
        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            reactUntil = 0;
            if (layer >= 0 && animator != null) animator.SetLayerWeight(layer, 0);
        }

        private void OnDamaged(Health _, int amount, DamageKind kind)
        {
            if (overlay == null) overlay = GetComponent<CombatFeedbackOverlay>() ?? gameObject.AddComponent<CombatFeedbackOverlay>();
            overlay.ShowDamage(amount, transform.position + Vector3.up * 1.8f, new Color(1, 0.25f, 0.15f));
            overlay.Hurt(amount / (float)health.MaxHealth);
            HitCameraImpulse.Pulse(0.1f, 0.14f);
            CombatImpactAudio.Play(sound, false, 0.32f);
            if (movement != null && movement.IsRolling) return;
            reactUntil = Time.time + 0.24f;
            if (layer < 0 || animator == null) return;
            animator.Play("DamageFlinch", layer, 0);
            animator.SetLayerWeight(layer, 1);
        }

        private void LateUpdate()
        {
            if (layer < 0 || animator == null) return;
            animator.SetLayerWeight(layer, Mathf.Clamp01((reactUntil - Time.time) / 0.12f));
        }
    }
}
