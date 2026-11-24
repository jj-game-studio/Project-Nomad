using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    [RequireComponent(typeof(PlayerEquipmentCombat))]
    [DefaultExecutionOrder(200)]
    public sealed partial class EquipmentCombatVisuals : MonoBehaviour
    {
        private sealed class Effect
        {
            public LineRenderer Line;
            public float Until;
            public float Duration;
        }

        private readonly List<Effect> effects = new();
        private PlayerEquipmentCombat combat;
        private PlayerEquipment equipment;
        private Transform weaponSocket;
        private LineRenderer rangeLine;
        private readonly Dictionary<Color, Material> materials = new();
        private Vector3 direction = Vector3.forward;
        private ItemDefinition displayedWeapon;
        private GameObject weaponModel;
        private const float SlashDuration = 0.18f;
        private float slashUntil;

        // 외형 모듈이 제공할 때만 손 뼈를 사용하며 기존 캡슐 플레이어는 기존 배치를 유지합니다.
        public Transform WeaponSocket
        {
            get => weaponSocket;
            set
            {
                if (weaponSocket == value) return;
                weaponSocket = value;
                if (value == null || weaponModel == null) return;
                weaponModel.transform.SetParent(value, false);
                weaponModel.transform.localPosition = Vector3.zero;
                weaponModel.transform.localRotation = displayedWeapon.EquippedVisual.transform.localRotation;
            }
        }
        public GameObject WeaponModel => weaponModel;
        public bool ShowRange { get; set; }
        public LineRenderer RangeLine => rangeLine;
        public int ActiveEffects => effects.Count;

        private void Awake()
        {
            combat = GetComponent<PlayerEquipmentCombat>();
            equipment = GetComponent<PlayerEquipment>();
            rangeLine = Line("Attack Range", new Color(0.2f, 0.9f, 0.8f), 0.035f);
            rangeLine.enabled = false;
            var gathering = new GameObject("Charge Gathering");
            gathering.transform.SetParent(transform, false);
            chargeGather = gathering.AddComponent<ChargeGatherVfx>();
            chargeGather.Configure(combat);
        }

        private void OnEnable()
        {
            chargeGather.enabled = true;
            equipment.Changed += RefreshWeapon;
            combat.Attacked += Attack;
            combat.Fired += Fire;
            combat.WeaponImpacted += Impact;
            combat.RangedImpacted += RangedImpact;
        }

        private void OnDisable()
        {
            combat.Attacked -= Attack;
            equipment.Changed -= RefreshWeapon;
            combat.Fired -= Fire;
            combat.WeaponImpacted -= Impact;
            combat.RangedImpacted -= RangedImpact;
            chargeGather.enabled = false;
            combat.StaffMuzzle = null;
            combat.PosedStaffOffset = null;
            rangeLine.enabled = false;
            foreach (Effect effect in effects) Destroy(effect.Line.gameObject);
            effects.Clear();
            if (weaponModel != null)
                weaponModel.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current?.f2Key.wasPressedThisFrame == true)
                ShowRange = !ShowRange;
            if (Mouse.current != null && !combat.EquipmentUiOpen && combat.TryGetAim(Mouse.current.position.ReadValue(), out Vector3 aim))
            {
                Vector3 planar = Vector3.ProjectOnPlane(aim - combat.AttackOrigin, Vector3.up);
                if (planar.sqrMagnitude > 0.001f) direction = planar.normalized;
            }

            ItemDefinition weapon = combat.CurrentWeapon;
            RefreshWeapon();
            rangeLine.enabled = ShowRange && weapon != null;
            if (rangeLine.enabled)
            {
                if (weapon.AttackMode == WeaponAttackMode.Melee)
                {
                    // 가슴 높이 부채꼴을 그대로 그리면 오르막 지형에 묻혀 양 끝 선만 보이므로 바닥에 붙여 그립니다.
                    Vector3[] points = Arc(weapon.AttackRange, true);
                    for (int index = 0; index < points.Length; index++)
                        points[index] = OnGround(points[index]);
                    rangeLine.positionCount = points.Length;
                    rangeLine.SetPositions(points);
                }
                else
                {
                    Vector3 rangedAim = combat.AimPoint ?? combat.ProjectileOrigin + direction * weapon.AttackRange;
                    if (Mouse.current != null && combat.TryGetAim(Mouse.current.position.ReadValue(), out Vector3 pointerAim)) rangedAim = pointerAim;
                    Vector3 path = (rangedAim - combat.ProjectileOrigin).normalized;
                    float distance = weapon.AttackRange;
                    if (Projectile.TryGetFirstHit(new Ray(combat.ProjectileOrigin, path), distance,
                            combat.CollisionLayers, transform, out RaycastHit hit)) distance = hit.distance;
                    rangeLine.positionCount = 2;
                    rangeLine.SetPositions(new[] { combat.ProjectileOrigin, combat.ProjectileOrigin + path * distance });
                }
            }

            for (int index = effects.Count - 1; index >= 0; index--)
            {
                Effect effect = effects[index];
                if (Time.time >= effect.Until)
                {
                    Destroy(effect.Line.gameObject);
                    effects.RemoveAt(index);
                }
                else
                {
                    float remaining = (effect.Until - Time.time) / effect.Duration;
                    effect.Line.widthMultiplier = Mathf.Max(0.01f, 0.11f * remaining);
                }
            }
        }

        private void RefreshWeapon()
        {
            ItemDefinition weapon = combat.CurrentWeapon;
            UpdateWeaponModel(weapon);
            combat.StaffMuzzle = weapon?.AttackMode == WeaponAttackMode.Staff ? staffMuzzle : null;
        }

        private void UpdateWeaponModel(ItemDefinition weapon)
        {
            if (displayedWeapon != weapon)
            {
                displayedWeapon = weapon;
                staffMuzzle = null;
                combat.StaffMuzzle = null;
                combat.PosedStaffOffset = null;
                slashUntil = 0f;
                if (weaponModel != null)
                {
                    weaponModel.SetActive(false);
                    Destroy(weaponModel);
                }

                GameObject prefab = weapon != null ? weapon.EquippedVisual : null;
                weaponModel = prefab != null ? Instantiate(prefab, transform, false) : null;
                if (weaponModel != null)
                {
                    // 원본 배치용 콜라이더는 장착 외형의 물리 판정에 참여하지 않습니다.
                    foreach (Collider collider in weaponModel.GetComponentsInChildren<Collider>(true))
                        collider.enabled = false;
                    ConfigureStaffMuzzle(weapon);
                }
            }

            if (weaponModel == null)
                return;

            if (WeaponSocket != null)
            {
                weaponModel.transform.SetParent(WeaponSocket, false);
                weaponModel.transform.localPosition = Vector3.zero;
                weaponModel.transform.localRotation = weapon.EquippedVisual.transform.localRotation;
                weaponModel.SetActive(true);
                return;
            }

            weaponModel.transform.SetParent(transform, true);
            Quaternion facing = Quaternion.LookRotation(direction);
            Quaternion pose = Quaternion.identity;
            if (Time.time < slashUntil)
            {
                float progress = 1f - (slashUntil - Time.time) / SlashDuration;
                pose = Quaternion.Euler(55f, Mathf.Lerp(65f, -65f, progress), 0f);
            }

            weaponModel.transform.SetPositionAndRotation(
                combat.AttackOrigin + facing * new Vector3(0.43f, 0.1f, 0.2f),
                facing * pose * weapon.EquippedVisual.transform.localRotation);
            if (!weaponModel.activeSelf)
                weaponModel.SetActive(true);
        }

        private Vector3[] Arc(float radius, bool clipped)
        {
            const int segments = 32;
            var points = new Vector3[segments + 3];
            points[0] = combat.AttackOrigin;
            for (int index = 0; index <= segments; index++)
            {
                float angle = Mathf.Lerp(-PlayerEquipmentCombat.MeleeHalfAngle, PlayerEquipmentCombat.MeleeHalfAngle, index / (float)segments);
                Vector3 ray = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                float reach = clipped ? combat.MeleeReach(ray, radius) : radius;
                points[index + 1] = combat.AttackOrigin + ray * reach;
            }
            points[points.Length - 1] = combat.AttackOrigin;
            return points;
        }

        private Vector3 OnGround(Vector3 point)
        {
            // 경사 한계(약 50도)까지 오른 지면도 찾도록 거리만큼 위에서 아래로 검사합니다.
            float feet = transform.position.y;
            float distance = Vector3.ProjectOnPlane(point - transform.position, Vector3.up).magnitude;
            var ray = new Ray(new Vector3(point.x, feet + 1.6f + distance * 1.2f, point.z), Vector3.down);
            Vector3 ground = new Vector3(point.x, feet, point.z);
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(ray, ray.origin.y - feet + 3f, combat.CollisionLayers, QueryTriggerInteraction.Ignore))
                if (hit.distance < nearest && !hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<Health>() == null)
                {
                    nearest = hit.distance;
                    ground = hit.point;
                }
            return ground + Vector3.up * 0.06f;
        }

        private void Attack(WeaponAttackMode mode)
        {
            if (combat.AimPoint.HasValue)
            {
                Vector3 planar = Vector3.ProjectOnPlane(combat.AimPoint.Value - combat.AttackOrigin, Vector3.up);
                if (planar.sqrMagnitude > 0.001f) direction = planar.normalized;
            }

            ItemDefinition weapon = combat.CurrentWeapon;
            bool styled = WeaponVfx.TryGetStyle(weapon, out WeaponVfxStyle style);
            if (mode == WeaponAttackMode.Melee)
            {
                slashUntil = Time.time + SlashDuration;
                if (styled && style.Slash)
                {
                    SwordSlashVfx.Spawn(combat.AttackOrigin, direction, PlayerEquipmentCombat.MeleeHalfAngle,
                        SweepReaches(weapon.AttackRange), style);
                    return;
                }

                Vector3[] fan = Arc(weapon.AttackRange, true);
                var slash = new Vector3[fan.Length - 2];
                System.Array.Copy(fan, 1, slash, 0, slash.Length);
                AddEffect(slash, new Color(1f, 0.72f, 0.2f), SlashDuration);
            }
            else if (mode == WeaponAttackMode.Staff && styled)
            {
                // 시전 섬광도 Fire의 실제 발사 위치에서 생성합니다.
                return;
            }
            else
            {
                Vector3 path = ((combat.AimPoint ?? combat.ProjectileOrigin + direction) - combat.ProjectileOrigin).normalized;
                Color tint = mode == WeaponAttackMode.Bow ? new Color(1f, 0.8f, 0.45f) : new Color(0.5f, 0.9f, 1f);
                AddEffect(new[] { combat.ProjectileOrigin, combat.ProjectileOrigin + path * 0.8f }, tint, 0.12f);
            }
        }

        // 베기 VFX는 칼이 오른쪽에서 왼쪽으로 지나가므로 오른쪽 끝(+각)부터 도달 거리를 담습니다.
        private float[] SweepReaches(float range)
        {
            const int segments = 32;
            var reaches = new float[segments + 1];
            for (int index = 0; index <= segments; index++)
            {
                float angle = Mathf.Lerp(PlayerEquipmentCombat.MeleeHalfAngle, -PlayerEquipmentCombat.MeleeHalfAngle, index / (float)segments);
                reaches[index] = combat.MeleeReach(Quaternion.AngleAxis(angle, Vector3.up) * direction, range);
            }
            return reaches;
        }

        private void Fire(Projectile projectile, ItemDefinition weapon)
        {
            if (projectile == null) return;
            float power = combat.LastShotPower;
            WeaponVfxStyle style = RangedStyle(weapon, power);
            if (weapon.AttackMode == WeaponAttackMode.Staff)
                MagicBoltVfx.Attach(projectile.transform, style);
            else if (weapon.AttackMode == WeaponAttackMode.Bow)
                ArrowShotVfx.Attach(projectile.transform, style, power);

            if (weapon.AttackMode == WeaponAttackMode.Staff || power > 1.1f)
                WeaponVfx.Burst(projectile.transform.position, style, power > 1.1f ? 0.5f : 0.25f);
        }

        private void Impact(Vector3 point, ItemDefinition weapon)
        {
            if (weapon != null && (weapon.AttackMode == WeaponAttackMode.Staff || weapon.AttackMode == WeaponAttackMode.Bow)) return;

            AddEffect(new[] { point - Vector3.right * 0.16f, point + Vector3.right * 0.16f,
                point, point - Vector3.up * 0.16f, point + Vector3.up * 0.16f }, new Color(1f, 0.9f, 0.5f), 0.2f);
        }

        private void AddEffect(Vector3[] points, Color color, float duration)
        {
            if (effects.Count >= 24)
            {
                Destroy(effects[0].Line.gameObject);
                effects.RemoveAt(0);
            }
            LineRenderer line = Line("Attack Effect", color, 0.1f);
            line.positionCount = points.Length;
            line.SetPositions(points);
            effects.Add(new Effect { Line = line, Until = Time.time + duration, Duration = duration });
        }

        private LineRenderer Line(string name, Color color, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            if (!materials.TryGetValue(color, out Material material))
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", color);
                materials.Add(color, material);
            }
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials.Values) Destroy(material);
            materials.Clear();
        }
    }
}
