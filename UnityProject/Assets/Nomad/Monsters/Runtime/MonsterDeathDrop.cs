using System;
using Nomad.Combat;
using UnityEngine;

namespace Nomad.Monsters
{
    [RequireComponent(typeof(Health))]
    public sealed class MonsterDeathDrop : MonoBehaviour
    {
        private Health health;
        private bool dropped;
        private Transform dropParent;
        public PlaceholderEquipmentDrop SpawnedDrop { get; private set; }
        public event Action<PlaceholderEquipmentDrop> DropCreated;

        public void Configure(Transform parent) => dropParent = parent;

        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(Health dead)
        {
            if (dropped) return;
            dropped = true;

            // 시각적 낙하와 아이템/루팅 계약을 분리해 장비 모듈을 직접 참조하지 않습니다.
            var root = new GameObject("Equipment drop (placeholder, no pickup)");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            root.transform.SetParent(dropParent, true);
            root.transform.position = transform.position;
            SpawnedDrop = root.AddComponent<PlaceholderEquipmentDrop>();
            SpawnedDrop.Configure(GetComponent<MonsterIdentity>()?.Rank ?? MonsterRank.Normal);
            DropCreated?.Invoke(SpawnedDrop);
        }
    }
}
