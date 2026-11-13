using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    // 고정 테스트 씬 전용입니다. 생성 필드의 NavMesh 준비는 #18에서 담당합니다.
    [DefaultExecutionOrder(-100)]
    public sealed class TrackingNavigation : MonoBehaviour
    {
        private NavMeshData data;
        private NavMeshDataInstance instance;

        private void OnEnable()
        {
            var sources = new List<NavMeshBuildSource>();
            foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>())
            {
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center),
                    size = box.size,
                    area = 0
                });
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.3f;
            settings.agentSlope = 45f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.08f;
            data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(new Vector3(0f, 3f, 8f), new Vector3(40f, 14f, 45f)),
                Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogError("MONSTER_NAVIGATION_FAIL: 고정 테스트 경로 생성 실패");
                return;
            }

            instance = NavMesh.AddNavMeshData(data);
        }

        private void OnDisable()
        {
            if (instance.valid)
                instance.Remove();

            if (data != null)
                Destroy(data);
        }
    }
}
