using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Equipment
{
    [RequireComponent(typeof(PlayerEquipmentCombat))]
    public sealed class EquipmentCombatVisuals : MonoBehaviour
    {
        private sealed class Effect
        {
            public LineRenderer Line;
            public float Until;
            public float Duration;
        }

        private readonly List<Effect> effects = new();
        private PlayerEquipmentCombat combat;
        private LineRenderer rangeLine;
        private readonly Dictionary<Color, Material> materials = new();
        private Vector3 direction = Vector3.forward;
        private ItemDefinition displayedWeapon;
        private GameObject weaponModel;
        private const float SlashDuration = 0.18f;
        private float slashUntil;

        public bool ShowRange { get; set; }
        public LineRenderer RangeLine => rangeLine;
        public int ActiveEffects => effects.Count;

        private void Awake()
        {
            combat = GetComponent<PlayerEquipmentCombat>();
            rangeLine = Line("Attack Range", new Color(0.2f, 0.9f, 0.8f), 0.035f);
            rangeLine.enabled = false;
        }

        private void OnEnable()
        {
            combat.Attacked += Attack;
            combat.Impacted += Impact;
        }

        private void OnDisable()
        {
            combat.Attacked -= Attack;
            combat.Impacted -= Impact;
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
            UpdateWeaponModel(weapon);
            rangeLine.enabled = ShowRange && weapon != null;
            if (rangeLine.enabled)
            {
                if (weapon.AttackMode == WeaponAttackMode.Melee)
                {
                    Vector3[] points = Arc(weapon.AttackRange, true);
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

        private void UpdateWeaponModel(ItemDefinition weapon)
        {
            if (displayedWeapon != weapon)
            {
                displayedWeapon = weapon;
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
                }
            }

            if (weaponModel == null)
                return;

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

        private void Attack(WeaponAttackMode mode)
        {
            if (combat.AimPoint.HasValue)
            {
                Vector3 planar = Vector3.ProjectOnPlane(combat.AimPoint.Value - combat.AttackOrigin, Vector3.up);
                if (planar.sqrMagnitude > 0.001f) direction = planar.normalized;
            }
            if (mode == WeaponAttackMode.Melee)
            {
                Vector3[] fan = Arc(combat.CurrentWeapon.AttackRange, true);
                var slash = new Vector3[fan.Length - 2];
                System.Array.Copy(fan, 1, slash, 0, slash.Length);
                slashUntil = Time.time + SlashDuration;
                AddEffect(slash, new Color(1f, 0.72f, 0.2f), SlashDuration);
            }
            else
            {
                Vector3 path = ((combat.AimPoint ?? combat.ProjectileOrigin + direction) - combat.ProjectileOrigin).normalized;
                Color tint = mode == WeaponAttackMode.Bow ? new Color(1f, 0.8f, 0.45f) : new Color(0.5f, 0.9f, 1f);
                AddEffect(new[] { combat.ProjectileOrigin, combat.ProjectileOrigin + path * 0.8f }, tint, 0.12f);
            }
        }

        private void Impact(Vector3 point)
        {
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
