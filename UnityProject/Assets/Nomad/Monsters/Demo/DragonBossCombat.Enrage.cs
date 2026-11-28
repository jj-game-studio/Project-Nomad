using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        public void BeginEnrage()
        {
            CancelEffects();
            pendingVolleyHits.Clear();
            parrySignal.Clear();
            pendingSlamCounter = false;
            queuedPattern = null;
            tactics.Stop();
            Pause();
            Phase = DragonPhase.Enraging;
            status.text = "";
            SetMotion(null, 1);
        }

        public void FinishEnrage()
        {
            if (health.IsDead) return;
            nextCharge = Mathf.Max(nextCharge, Time.time + 5);
            Recover();
        }
    }
}
