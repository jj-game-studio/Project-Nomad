using Nomad.Combat;
using Nomad.Visibility;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossArena
    {
        private Health encounterPlayer;
        private PlayerVisibility encounterSight;

        public void ObserveEncounter(Health player) => encounterPlayer = player;

        private void Update()
        {
            if (encounterSight == null) encounterSight = FindFirstObjectByType<PlayerVisibility>();
            if (encounterSight == null) return;
            bool inside = false;
            if (!IsLocked && encounterPlayer != null && !encounterPlayer.IsDead && encounterPlayer.gameObject.activeInHierarchy)
            {
                Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(Forward)) * (encounterPlayer.transform.position - Center);
                inside = Mathf.Abs(local.x) <= HalfSize && Mathf.Abs(local.z) <= HalfSize && Mathf.Abs(local.y) < 4;
            }
            if (inside) encounterSight.OpenAreaOwner = this;
            else ClearEncounter();
        }

        private void ClearEncounter()
        {
            if (encounterSight != null && encounterSight.OpenAreaOwner == this) encounterSight.OpenAreaOwner = null;
        }
    }
}
