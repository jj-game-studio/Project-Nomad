using System.Collections.Generic;
using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossArena
    {
        public bool IsLocked { get; private set; }
        private Health prerequisite;
        private Health sleepingBoss;
        private bool wasImmune;
        private GameObject seal;
        private readonly List<(Behaviour component, bool enabled)> sleeping = new();

        public void RequireElite(Health elite, Health boss)
        {
            prerequisite = elite;
            sleepingBoss = boss;
            if (elite == null || elite.IsDead) return;
            IsLocked = true;
            prerequisite.Died += Unlock;
            wasImmune = boss.IsCombatImmune;
            boss.IsCombatImmune = true;
            Suspend(boss.GetComponent<MonsterTracker>());
            Suspend(boss.GetComponent<MonsterMeleeAttack>());
            Suspend(boss.GetComponent<MonsterRangedAttack>());
            Suspend(boss.GetComponent<MonsterTactics>());
            NavMeshAgent agent = boss.GetComponent<NavMeshAgent>();
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;

            seal = new GameObject("Elite seal");
            seal.transform.SetParent(transform, false);
            Quaternion rotation = Quaternion.LookRotation(Forward);
            Material red = BossRouteGuide.QuietRedMaterial(0.18f);
            materials.Add(red);
            for (int side = 0; side < 4; side++)
            {
                Quaternion turn = rotation * Quaternion.Euler(0, side * 90, 0);
                var wall = new GameObject("Sealed boundary");
                wall.transform.SetParent(seal.transform, false);
                wall.transform.SetPositionAndRotation(Center + turn * new Vector3(0, 2.5f, -HalfSize + 0.4f), turn);
                wall.AddComponent<BoxCollider>().size = new Vector3(HalfSize * 2, 5, 0.45f);
                if (side != 0) continue;
                GameObject curtain = Decor("Sealed entrance", wall.transform.position,
                    new Vector3(4.5f, 5, 0.04f), turn, red);
                curtain.transform.SetParent(seal.transform, true);
            }
            sign.text = "엘리트를 처치하면 개방됩니다";
        }

        private void Suspend(Behaviour component)
        {
            if (component == null) return;
            sleeping.Add((component, component.enabled));
            component.enabled = false;
        }

        private void Unlock(Health defeated)
        {
            prerequisite.Died -= Unlock;
            IsLocked = false;
            // 이벤트가 발생한 프레임부터 충돌도 즉시 해제합니다.
            if (seal != null) { seal.SetActive(false); Destroy(seal); }
            sign.text = "용의 제단 · 개방";
            if (sleepingBoss != null && !sleepingBoss.IsDead)
            {
                sleepingBoss.IsCombatImmune = wasImmune;
                foreach (var entry in sleeping) if (entry.component != null) entry.component.enabled = entry.enabled;
                NavMeshAgent agent = sleepingBoss.GetComponent<NavMeshAgent>();
                if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
            }
            sleeping.Clear();
        }

        private void ClearProgression()
        {
            if (prerequisite != null) prerequisite.Died -= Unlock;
        }
    }
}
