using Nomad.Combat;
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
        private const float SafeFallDistance = 3f;

        [SerializeField] private Camera viewCamera;
        [SerializeField] private float killHeight = -20f;

        private CharacterController controller;
        private Health health;
        private float verticalSpeed;
        private float rollTime;
        private float cooldownTime;
        private Vector3 rollDirection;
        private bool rollBlocked;

        private bool wasAirborne;
        private float peakHeight;
        private Vector3 lastPosition;

        public bool IsRolling => rollTime > 0f;
        internal bool BlocksAttack { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            TryGetComponent(out health);
        }

        private void OnEnable()
        {
            ResetFallTracking();
        }

        private void OnDisable()
        {
            verticalSpeed = 0f;
            rollTime = 0f;
            cooldownTime = 0f;
            BlocksAttack = false;
            if (health != null)
                health.IsCombatImmune = false;
        }

        private void Update()
        {
            float remainingTime = Time.deltaTime;
            BlocksAttack = IsRolling;
            if (remainingTime <= 0f || (health != null && health.IsDead))
                return;

            // 이동을 멈춘 사이 외부에서 위치가 변경되면 이전 낙차를 이어서 계산하지 않습니다.
            if ((transform.position - lastPosition).sqrMagnitude > 0.000001f)
                ResetFallTracking();

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
            while (remainingTime > 0f && (health == null || !health.IsDead))
            {
                float stepTime = Mathf.Min(remainingTime, MaxStepTime);
                if (IsRolling)
                    stepTime = Mathf.Min(stepTime, rollTime);

                Move(direction, stepTime);
                remainingTime -= stepTime;
            }

            lastPosition = transform.position;
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
            BlocksAttack = true;
            if (health != null)
                health.IsCombatImmune = true;
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
            float previousHeight = transform.position.y;
            CollisionFlags contacts = controller.Move(displacement);
            if ((contacts & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;

            if (IsRolling)
            {
                // 벽에 막히면 이동만 중단하고, 접지를 잃었을 때는 구르기 상태도 종료합니다.
                rollBlocked |= (contacts & CollisionFlags.Sides) != 0;
                rollTime = controller.isGrounded ? Mathf.Max(0f, rollTime - deltaTime) : 0f;
            }

            if (health != null)
            {
                health.IsCombatImmune = IsRolling;
                TrackFall(previousHeight);
                if (health.IsDead)
                {
                    rollTime = 0f;
                    health.IsCombatImmune = false;
                }
            }
        }

        private void ResetFallTracking()
        {
            lastPosition = transform.position;
            peakHeight = lastPosition.y;
            wasAirborne = false;
        }

        private void TrackFall(float previousHeight)
        {
            float height = transform.position.y;
            if (height < killHeight)
            {
                health.TryApplyDamage(health.CurrentHealth, DamageKind.Fall);
                return;
            }

            if (!controller.isGrounded)
            {
                peakHeight = wasAirborne
                    ? Mathf.Max(peakHeight, height)
                    : Mathf.Max(previousHeight, height);
                wasAirborne = true;
                return;
            }

            if (!wasAirborne)
                return;

            wasAirborne = false;
            float excess = peakHeight - height - SafeFallDistance;
            if (excess > 0f)
                health.TryApplyDamage(Mathf.CeilToInt(excess * 10f), DamageKind.Fall);
        }
    }
}
