using UnityEngine;

namespace Nomad.Terrain
{
    // B 씬의 자동 왕복 확인 전용입니다. A의 플레이어 입력/이동 구현과 분리합니다.
    [RequireComponent(typeof(CharacterController))]
    public sealed class TerrainTraversalProbe : MonoBehaviour
    {
        [SerializeField] private Vector3 start = new Vector3(0f, 0f, -3f);
        [SerializeField] private Vector3 end = new Vector3(0f, 2f, 23f);

        private CharacterController controller;
        private bool returning;
        private float elapsed;
        private float verticalSpeed;

        public bool Complete { get; private set; }
        public bool Failed { get; private set; }

        private void FixedUpdate()
        {
            Step(Time.fixedDeltaTime);
        }

        public void Step(float deltaTime)
        {
            if (Complete || Failed)
                return;

            if (controller == null)
                controller = GetComponent<CharacterController>();

            elapsed += deltaTime;
            Vector3 target = returning ? start : end;
            Vector3 direction = target - transform.position;
            direction.y = 0f;

            if (controller.isGrounded)
                verticalSpeed = -2f;
            else
                verticalSpeed -= 20f * deltaTime;

            controller.Move((direction.normalized * 5f + Vector3.up * verticalSpeed) * deltaTime);

            if (direction.magnitude < 0.15f && Mathf.Abs(transform.position.y - target.y) < 0.12f)
            {
                if (returning)
                {
                    Complete = true;
                    Debug.Log("TERRAIN_TRAVERSAL_PASS: 평지 → 계단 → 높은 평지 왕복 완료");
                }

                returning = true;
            }

            if (elapsed > 20f || transform.position.y < -2f)
            {
                Failed = true;
                Debug.LogError($"TERRAIN_TRAVERSAL_FAIL: {transform.position}");
            }
        }
    }
}
