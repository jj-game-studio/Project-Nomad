using Nomad.Combat;
using UnityEngine;

namespace Nomad.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class PlayerMana : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximum = 100;
        [SerializeField, Min(0)] private float recoveryPerSecond = 8f;
        [SerializeField, Min(0)] private float recoveryDelay = 2f;

        private Health health;
        private float recoveryTime;

        public float Current { get; private set; }
        public int Maximum => maximum;

        private void Awake()
        {
            health = GetComponent<Health>();
            Current = maximum;
        }

        private void Update()
        {
            if (!health.IsDead && Time.time >= recoveryTime)
                Current = Mathf.Min(maximum, Current + recoveryPerSecond * Time.deltaTime);
        }

        public bool CanSpend(int amount) => isActiveAndEnabled && !health.IsDead && amount > 0 && Current >= amount;

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
                return false;

            Current -= amount;
            recoveryTime = Time.time + recoveryDelay;
            return true;
        }
    }
}
