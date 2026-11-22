using Nomad.Player;
using UnityEngine;

namespace Nomad.PlayerVisual
{
    // 이동 결과만 읽어 외형을 갱신합니다. 실제 위치와 충돌은 PlayerMovement가 담당합니다.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement), typeof(CharacterController))]
    public sealed class PlayerAnimation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private PlayerMovement movement;
        private CharacterController controller;
        private string state;

        public string CurrentState => state;
        public Animator Animator => animator;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable() => state = null;

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled || Time.deltaTime <= 0f)
                return;

            Vector3 velocity = controller.velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            string next = movement.IsRolling ? "Roll" : !controller.isGrounded
                ? (velocity.y > 0.1f ? "Jump" : "Fall") : speed > 0.1f ? "Run" : "Idle";

            animator.speed = next == "Run" ? Mathf.Clamp(speed / 5f, 0.15f, 2f) : 1f;
            if (next == state) return;

            // 일회 동작은 처음부터 재생하고, 착지와 이동 전환은 짧게 혼합합니다.
            animator.CrossFadeInFixedTime(next, next == "Roll" || next == "Jump" ? 0.02f : 0.08f, 0, 0f);
            state = next;
        }
    }
}
