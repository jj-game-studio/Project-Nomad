using System;
using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Inventory;
using Nomad.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerEquipment), typeof(PlayerMovement), typeof(Health))]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerEquipmentCombat : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Projectile projectile;
        [SerializeField] private ItemDefinition unarmedDefinition;
        [SerializeField] private LayerMask collisionLayers = Physics.DefaultRaycastLayers;

        private readonly List<RaycastResult> uiHits = new();
        private PlayerEquipment equipment;
        private PlayerMovement movement;
        private Health health;
        private PlayerCombat legacyCombat;
        private bool restoreLegacy;
        private float nextAttackTime;

        public event Action<WeaponAttackMode> Attacked;
        public Vector3? AimPoint { get; private set; }
        public bool InputBlocked { get; set; }
        public ItemDefinition CurrentWeapon => equipment.ActiveWeapon != null ? equipment.ActiveWeapon : unarmedDefinition;

        private void Awake()
        {
            equipment = GetComponent<PlayerEquipment>();
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<Health>();
            legacyCombat = GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            // 기존 전투 컴포넌트가 있는 프리팹에서도 한 클릭이 두 번 공격하지 않습니다.
            restoreLegacy = legacyCombat != null && legacyCombat.enabled;
            if (restoreLegacy)
                legacyCombat.enabled = false;
        }

        private void OnDisable()
        {
            if (restoreLegacy && legacyCombat != null)
                legacyCombat.enabled = true;
        }

        private void LateUpdate()
        {
            if (Mouse.current?.leftButton.wasPressedThisFrame == true)
                TryAttack(Mouse.current.position.ReadValue());
        }

        public bool TryAttack(Vector2 pointer)
        {
            AimPoint = null;
            ItemDefinition weapon = CurrentWeapon;
            if (!isActiveAndEnabled || !equipment.isActiveAndEnabled || InputBlocked ||
                Time.timeScale <= 0 || health.IsDead || movement.BlocksAttack || Time.time < nextAttackTime ||
                weapon == null || weapon.EquipmentType != EquipmentType.Weapon || weapon.AttackPower <= 0 ||
                weapon.AttackRange <= 0 || weapon.AttackInterval <= 0)
                return false;

            Camera camera = viewCamera != null ? viewCamera : Camera.main;
            if (camera == null || !camera.pixelRect.Contains(pointer) || IsOverUi(pointer))
                return false;

            Ray aimRay = camera.ScreenPointToRay(pointer);
            Vector3 origin = transform.position + Vector3.up;
            Vector3 aim;
            if (Projectile.TryGetFirstHit(aimRay, 1000f, collisionLayers, transform, out RaycastHit hit))
                aim = hit.point;
            else if (new Plane(Vector3.up, origin).Raycast(aimRay, out float distance))
                aim = aimRay.GetPoint(distance);
            else
                return false;

            bool launched;
            if (weapon.AttackMode == WeaponAttackMode.Ranged)
                launched = muzzle != null && Projectile.TryLaunch(projectile, transform, muzzle.position, aim,
                    collisionLayers, 45f, weapon.AttackRange, weapon.AttackPower);
            else if (weapon.AttackMode == WeaponAttackMode.Melee)
            {
                Vector3 direction = Vector3.ProjectOnPlane(aim - origin, Vector3.up).normalized;
                if (direction.sqrMagnitude < 0.001f)
                    return false;
                nextAttackTime = Time.time + weapon.AttackInterval;
                Melee(origin, direction, weapon);
                launched = true;
            }
            else
                return false;

            if (!launched)
                return false;

            AimPoint = aim;
            nextAttackTime = Time.time + weapon.AttackInterval;
            Attacked?.Invoke(weapon.AttackMode);
            return true;
        }

        private void Melee(Vector3 origin, Vector3 direction, ItemDefinition weapon)
        {
            var targets = new HashSet<Health>();
            foreach (Collider collider in Physics.OverlapSphere(origin, weapon.AttackRange, collisionLayers,
                         QueryTriggerInteraction.Ignore))
            {
                Health target = collider.GetComponentInParent<Health>();
                if (target == null || target == health || target.IsDead || targets.Contains(target))
                    continue;

                Vector3 point = collider.ClosestPoint(origin);
                Vector3 path = point - origin;
                Vector3 planar = Vector3.ProjectOnPlane(path, Vector3.up);
                // 첫 구현은 조준 방향의 전방 120도 부채꼴입니다. 사거리는 CSV에서 읽습니다.
                if (planar.sqrMagnitude > 0.001f && Vector3.Dot(direction, planar.normalized) < 0.5f)
                    continue;

                if (HasWallBetween(origin, path))
                    continue;

                targets.Add(target);
            }

            // 피해 이벤트가 다른 대상을 삭제하더라도 한 번의 공격에서 중복 판정하지 않습니다.
            foreach (Health target in targets)
                if (target != null && !target.IsDead)
                    target.TryApplyDamage(weapon.AttackPower);
        }

        private bool HasWallBetween(Vector3 origin, Vector3 path)
        {
            if (path.sqrMagnitude < 0.000001f)
                return false;

            foreach (RaycastHit hit in Physics.RaycastAll(origin, path.normalized, path.magnitude,
                         collisionLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<Health>() == null)
                    return true;
            return false;
        }

        private bool IsOverUi(Vector2 pointer)
        {
            if (EventSystem.current == null)
                return false;

            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, uiHits);
            foreach (RaycastResult hit in uiHits)
                if (hit.module is not PhysicsRaycaster)
                    return true;
            return false;
        }
    }
}
