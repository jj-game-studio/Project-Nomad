using Nomad.Combat;
using NUnit.Framework;

namespace Nomad.Tests
{
    public sealed class HealthTests : PlayModeFixture
    {
        private Health health;
        private int deaths;

        [SetUp]
        public void SetUp()
        {
            var owner = Inactive("Health");
            health = owner.AddComponent<Health>();
            TestKit.SetField(health, "maxHealth", 10);
            owner.SetActive(true);

            deaths = 0;
            health.Died += _ => deaths++;
        }

        [Test]
        public void StartsAtMaxHealth()
        {
            Assert.That(health.CurrentHealth, Is.EqualTo(10));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void DamageReducesHealth()
        {
            Assert.That(health.TryApplyDamage(3), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(7));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void NonPositiveDamageIsIgnored(int amount)
        {
            Assert.That(health.TryApplyDamage(amount), Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(10));
        }

        [Test]
        public void CombatImmunityBlocksCombatButNotFallDamage()
        {
            health.IsCombatImmune = true;

            Assert.That(health.TryApplyDamage(4), Is.False);
            Assert.That(health.TryApplyDamage(4, DamageKind.Fall), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(6));
        }

        [Test]
        public void LethalDamageClampsToZeroAndDiesOnce()
        {
            Assert.That(health.TryApplyDamage(25), Is.True);
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.IsDead, Is.True);

            // 사망 후 추가 피해는 무시하고 사망 알림도 반복하지 않습니다.
            Assert.That(health.TryApplyDamage(1), Is.False);
            Assert.That(health.TryApplyDamage(1, DamageKind.Fall), Is.False);
            Assert.That(deaths, Is.EqualTo(1));
        }
    }
}
