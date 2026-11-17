using System.Collections.Generic;
using Nomad.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Nomad.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement), typeof(Health))]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerCombat : MonoBehaviour
    {
        private const float Range = 200f;

        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Projectile projectile;
        [SerializeField] private LayerMask collisionLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(1)] private int shotDamage = 25;
        [SerializeField, Min(1f)] private float projectileSpeed = 45f;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PlayerMovement movement;
        private Health health;

        public Vector3? AimPoint { get; private set; }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<Health>();
        }

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                TryShoot(mouse.position.ReadValue());
        }

        public bool TryShoot(Vector2 pointer)
        {
            AimPoint = null;
            if (!isActiveAndEnabled || Time.timeScale <= 0f || health.IsDead || movement.BlocksAttack ||
                viewCamera == null || muzzle == null || !viewCamera.pixelRect.Contains(pointer) || IsOverUi(pointer))
                return false;

            // 카메라 레이는 조준점만 정합니다. 발사체는 발사구에서 별도의 경로를 검사합니다.
            Ray aim = viewCamera.ScreenPointToRay(pointer);
            if (!Projectile.TryGetFirstHit(aim, Range, collisionLayers, transform, out RaycastHit hit))
                return false;

            AimPoint = hit.point;
            return Projectile.TryLaunch(projectile, transform, muzzle.position, hit.point,
                collisionLayers, projectileSpeed, Range, shotDamage);
        }

        private bool IsOverUi(Vector2 pointer)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            // 입력 모듈의 이전 프레임 포인터 상태에 의존하지 않고 클릭 좌표에서 검사합니다.
            var eventData = new PointerEventData(eventSystem) { position = pointer };
            eventSystem.RaycastAll(eventData, uiHits);
            foreach (RaycastResult hit in uiHits)
            {
                if (hit.module is not PhysicsRaycaster)
                    return true;
            }

            return false;
        }
    }
}
