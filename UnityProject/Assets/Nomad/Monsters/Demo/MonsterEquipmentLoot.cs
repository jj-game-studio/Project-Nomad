using System.Collections;
using Nomad.Inventory;
using Nomad.Loot;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [RequireComponent(typeof(MonsterDeathDrop))]
    public sealed class MonsterEquipmentLoot : MonoBehaviour
    {
        private MonsterDeathDrop death;
        private PlayerInventory recipient;
        private MonsterEquipmentDropTable table;

        public void Configure(PlayerInventory inventory)
        {
            recipient = inventory;
            table = Resources.Load<MonsterEquipmentDropTable>("MonsterEquipmentDrops");
        }

        private void Awake() => death = GetComponent<MonsterDeathDrop>();
        private void OnEnable() => death.DropCreated += OnDrop;
        private void OnDisable() => death.DropCreated -= OnDrop;

        private void OnDrop(PlaceholderEquipmentDrop drop)
        {
            if (recipient == null || table == null) return;
            LootStack[] contents = table.Roll(drop.SourceRank, recipient, GetEntityId().GetHashCode() ^ Time.frameCount);
            if (contents.Length == 0) return;
            drop.name = "Dropped equipment - " + drop.SourceRank;
            StartCoroutine(Activate(drop, contents));
        }

        private static IEnumerator Activate(PlaceholderEquipmentDrop drop, LootStack[] contents)
        {
            while (drop != null && !drop.Landed) yield return null;
            if (drop == null) yield break;
            var trigger = drop.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 0.25f;
            trigger.size = new Vector3(0.8f, 0.5f, 0.8f);
            LootChest chest = drop.gameObject.AddComponent<LootChest>();
            chest.Initialize("monster-drop-" + drop.GetEntityId().GetHashCode(), contents);
            chest.ConfigureVisuals(drop.transform, null, null, null);
        }
    }
}
