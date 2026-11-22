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

        public void Configure(Transform parent) => dropParent = parent;

        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(Health dead)
        {
            if (dropped) return;
            dropped = true;

            // TODO: 장비 담당자가 이 지점에서 등급별 드롭테이블 결과를 전달합니다.
            // 현재는 실제 장비 데이터/획득 기능이 없는 표시물 하나만 생성합니다.
            var root = new GameObject("Equipment drop (placeholder, no pickup)");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            root.transform.SetParent(dropParent, true);
            root.transform.position = transform.position;
            SpawnedDrop = root.AddComponent<PlaceholderEquipmentDrop>();
            SpawnedDrop.Configure(GetComponent<MonsterIdentity>()?.Rank ?? MonsterRank.Normal);
        }
    }
}
