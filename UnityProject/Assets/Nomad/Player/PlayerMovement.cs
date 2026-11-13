using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private const float MoveSpeed = 5f;
        private const float Gravity = 20f;
        private const float JumpHeight = 1.2f;
        private const float GroundSpeed = -2f;
        private const float RollDuration = 0.3f;
        private const float RollSpeed = 3f / RollDuration;
        private const float RollCooldown = 1f;
        private const float MaxStepTime = 1f / 60f;

        [SerializeField] private Camera viewCamera;

        private CharacterController controller;
        private float verticalSpeed;
        private float rollTime;
        private float cooldownTime;
        private Vector3 rollDirection;
        private bool rollBlocked;

        public bool IsRolling => rollTime > 0f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void OnDisable()
        {
            verticalSpeed = 0f;
            rollTime = 0f;
            cooldownTime = 0f;
        }

        private void Update()
        {
            float remainingTime = Time.deltaTime;
            if (remainingTime <= 0f)
                return;

            Keyboard keyboard = Keyboard.current;
            Vector2 input = keyboard == null ? Vector2.zero : new Vector2(
                Axis(keyboard.dKey.isPressed, keyboard.aKey.isPressed),
                Axis(keyboard.wKey.isPressed, keyboard.sKey.isPressed));
            Vector3 direction = CameraRelativeDirection(input);

            if (controller.isGrounded && !IsRolling)
            {
                if (cooldownTime <= 0f && keyboard?.leftShiftKey.wasPressedThisFrame == true)
                    StartRoll(direction);
                else if (keyboard?.spaceKey.wasPressedThisFrame == true)
                    verticalSpeed = Mathf.Sqrt(2f * Gravity * JumpHeight);
            }

            // 긴 프레임에서도 낙하와 구르기 충돌을 작은 이동 단위로 처리합니다.
            while (remainingTime > 0f)
            {
                float stepTime = Mathf.Min(remainingTime, MaxStepTime);
                if (IsRolling)
                    stepTime = Mathf.Min(stepTime, rollTime);

                Move(direction, stepTime);
                remainingTime -= stepTime;
            }
        }

        private static float Axis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private Vector3 CameraRelativeDirection(Vector2 input)
        {
            Transform cameraTransform = viewCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
        }

        private void StartRoll(Vector3 direction)
        {
            rollDirection = direction.sqrMagnitude > 0f ? direction.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(rollDirection);
            rollTime = RollDuration;
            cooldownTime = RollCooldown;
            rollBlocked = false;
        }

        private void Move(Vector3 direction, float deltaTime)
        {
            cooldownTime = Mathf.Max(0f, cooldownTime - deltaTime);

            float verticalDistance;
            if (controller.isGrounded && verticalSpeed <= 0f)
            {
                verticalSpeed = GroundSpeed;
                verticalDistance = verticalSpeed * deltaTime;
            }
            else
            {
                verticalDistance = (verticalSpeed - 0.5f * Gravity * deltaTime) * deltaTime;
                verticalSpeed -= Gravity * deltaTime;
            }

            Vector3 planarVelocity = direction * MoveSpeed;
            if (IsRolling)
                planarVelocity = rollBlocked ? Vector3.zero : rollDirection * RollSpeed;
            else if (direction.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(direction);

            Vector3 displacement = planarVelocity * deltaTime + Vector3.up * verticalDistance;
            CollisionFlags contacts = controller.Move(displacement);
            if ((contacts & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;

            if (!IsRolling)
                return;

            // 벽에 막히면 이동만 중단하고, 접지를 잃었을 때는 구르기 상태도 종료합니다.
            rollBlocked |= (contacts & CollisionFlags.Sides) != 0;
            rollTime = controller.isGrounded ? Mathf.Max(0f, rollTime - deltaTime) : 0f;
        }
    }
}
