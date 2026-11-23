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
        // 외형 재생 시간이며 PlayerMovement의 이동·판정 시간과는 독립입니다.
        public const float JumpRiseDuration = 0.35f;
        public const float JumpFallDuration = 0.35f;
        public const float LandingMotionDuration = 0.55f;
        public const float RollMotionDuration = 0.8f;

        [SerializeField] private Animator animator;
        private PlayerMovement movement;
        private CharacterController controller;
        private string state;
        private float motionUntil;
        private bool wasGrounded;
        private bool wasRolling;

        public string CurrentState => state;
        public Animator Animator => animator;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                // 카메라 가시성 변화로 포즈 갱신이 건너뛰어지지 않게 합니다.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private void OnEnable()
        {
            state = null;
            motionUntil = 0f;
            wasGrounded = false;
            wasRolling = false;
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled || Time.deltaTime <= 0f)
                return;

            Vector3 velocity = controller.velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            string next = movement.IsRolling ? "Roll" : !controller.isGrounded
                ? (velocity.y > 0.1f ? "Jump" : "Fall") : speed > 0.1f ? "Run" : "Idle";

            bool rollStarted = movement.IsRolling && !wasRolling;
            bool jumpStarted = !controller.isGrounded && wasGrounded && velocity.y > 0.1f;
            bool landed = controller.isGrounded && !wasGrounded && (state == "Jump" || state == "Fall");
            wasRolling = movement.IsRolling;
            wasGrounded = controller.isGrounded;

            // 점프는 실제 상승·낙하·착지를 따르고, 구르기와 착지 마무리만 끝까지 재생합니다.
            if (rollStarted || jumpStarted)
            {
                next = rollStarted ? "Roll" : "Jump";
                motionUntil = rollStarted ? Time.time + RollMotionDuration : 0f;
            }
            else if (landed)
            {
                next = "Land";
                motionUntil = Time.time + LandingMotionDuration;
            }
            else if ((state == "Roll" || (state == "Land" && controller.isGrounded)) && Time.time < motionUntil)
                next = state;

            animator.speed = next == "Run" ? Mathf.Clamp(speed / 5f, 0.15f, 2f) : 1f;
            if (next == state && !rollStarted && !jumpStarted) return;

            // 일회 동작은 처음부터 재생하고, 착지와 이동 전환은 짧게 혼합합니다.
            animator.CrossFadeInFixedTime(next, next == "Roll" || next == "Jump" ? 0.05f : 0.1f, 0, 0f);
            state = next;
        }
    }
}
