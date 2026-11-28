using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        private float pursuitStarted = -1;
        private float nextPursuitPath;

        private void PursuePlayer()
        {
            // 보스방 전투는 일반 몬스터의 시야 상실/귀환 상태에 의존하지 않습니다.
            tracker.enabled = false;
            if (!agent.enabled) agent.enabled = true;
            if (!agent.isOnNavMesh)
            {
                if (!NavMesh.SamplePosition(transform.position, out NavMeshHit nearby, 3, agent.areaMask) || !agent.Warp(nearby.position)) return;
            }
            agent.isStopped = false;
            agent.stoppingDistance = 3.2f;
            if (Time.time < nextPursuitPath) return;

            nextPursuitPath = Time.time + 0.2f;
            if (!NavMesh.SamplePosition(target.transform.position, out NavMeshHit destination, 8, agent.areaMask)) return;
            var path = new NavMeshPath();
            if (agent.CalculatePath(destination.position, path) && path.status == NavMeshPathStatus.PathComplete)
                agent.SetPath(path);
        }
    }
}
