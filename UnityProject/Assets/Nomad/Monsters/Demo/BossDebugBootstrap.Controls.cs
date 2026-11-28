using Nomad.Combat;
using Nomad.Player;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossDebugBootstrap
    {
        private bool godMode;
        public bool UnlimitedResources { get; set; }

        private void LateUpdate()
        {
            if (Player == null) return;
            Player.GetComponent<PlayerMana>().Unlimited = UnlimitedResources;
            Player.GetComponent<PlayerStamina>().Unlimited = UnlimitedResources;
        }
        public bool GodMode
        {
            get => godMode;
            set { godMode = value; if (Player != null) Player.GetComponent<Health>().IsInvulnerable = value; }
        }

        public void Restart() { if (!restarting) StartCoroutine(Spawn()); }
        public void RestorePlayer()
        {
            if (Player == null) return;
            Player.GetComponent<Health>().RestoreFull();
            Player.GetComponent<PlayerMana>().RestoreFull();
        }

        public void EnterEnrage()
        {
            if (Boss == null) return;
            var health = Boss.GetComponent<Health>();
            int damage = health.CurrentHealth - Mathf.FloorToInt(health.MaxHealth * DragonBossCombat.EnrageHealthRatio);
            if (damage > 0) health.TryApplyDamage(damage, DamageKind.Fall);
        }
    }
}
