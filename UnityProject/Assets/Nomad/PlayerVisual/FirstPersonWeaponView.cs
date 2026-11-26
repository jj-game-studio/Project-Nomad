using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Inventory;
using Nomad.Player;
using UnityEngine;

namespace Nomad.PlayerVisual
{
    [DefaultExecutionOrder(250)]
    public sealed class FirstPersonWeaponView : MonoBehaviour
    {
        private VillageFirstPersonCamera owner;
        private Transform player;
        private PlayerEquipmentCombat combat;
        private FirstPersonArms arms;
        private GameObject root;
        private GameObject weaponModel;
        private ItemDefinition displayed;
        private Transform muzzle;
        private Transform previousMuzzle;
        private Vector3 grip;
        private float started = -100f;
        private float duration = 0.5f;
        private float draw;
        private float chargePose;
        private TrailRenderer trail;
        private ParticleSystem charge;
        public bool HasArms => arms != null;
        public GameObject WeaponModel => weaponModel;

        public void Initialize(VillageFirstPersonCamera cameraOwner, Transform target)
        {
            owner = cameraOwner;
            player = target;
            combat = target.GetComponent<PlayerEquipmentCombat>();
            if (combat == null) return;
            combat.MeleeStarted += Melee;
            combat.Attacked += Attack;
            combat.MeleeCancelled += Cancel;
            combat.ChargeCancelled += Cancel;
        }

        private void Melee(ItemDefinition item, Vector3 direction)
        {
            if (!owner.IsActive) return;
            started = Time.time;
            duration = item.AttackInterval;
        }

        private void Attack(WeaponAttackMode mode)
        {
            if (!owner.IsActive || (mode == WeaponAttackMode.Melee && Time.time - started < duration)) return;
            started = Time.time;
            duration = combat.CurrentWeapon != null ? combat.CurrentWeapon.AttackInterval : 0.5f;
        }

        private void Cancel() { started = -100f; if (trail != null) trail.Clear(); }

        private void Update()
        {
            // 기존 외형 모듈의 Update 뒤, 전투 조준 Update 전에 표시 발사구를 연결합니다.
            if (owner != null && owner.IsActive && muzzle != null) ApplyMuzzle();
        }

        private void LateUpdate()
        {
            bool active = owner != null && owner.IsActive && combat != null;
            if (!active)
            {
                RestoreMuzzle();
                if (root != null && root.activeSelf)
                {
                    Cancel();
                    if (charge != null) charge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    root.SetActive(false);
                }
                return;
            }
            if (!EnsureArms()) return;
            root.SetActive(true);
            ItemDefinition item = combat.CurrentWeapon;
            if (displayed != item) ChangeWeapon(item);

            WeaponAttackMode mode = item != null ? item.AttackMode : WeaponAttackMode.Melee;
            draw = Mathf.MoveTowards(draw, combat.IsCharging ? combat.ChargeProgress : 0f, Time.deltaTime * 6f);
            chargePose = Mathf.MoveTowards(chargePose, combat.IsCharging ? 1f : 0f, Time.deltaTime * 8f);
            float progress = Mathf.Clamp01((Time.time - started) / Mathf.Max(0.1f, duration));
            Vector3 right = new Vector3(0.3f, -0.18f, 0.58f);
            Vector3 left = new Vector3(-0.28f, -0.48f, 0.24f);
            Quaternion pose = Quaternion.Euler(10, -8, 8);
            if (mode == WeaponAttackMode.Melee && progress < 1f)
            {
                float swing = progress < 0.18f ? -0.35f * Mathf.SmoothStep(0f, 1f, progress / 0.18f) :
                    progress < 0.48f ? Mathf.Lerp(-0.35f, 1f, Mathf.SmoothStep(0f, 1f, (progress - 0.18f) / 0.3f)) :
                    Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, (progress - 0.48f) / 0.52f));
                right += new Vector3(-0.18f, -0.06f, 0.05f) * swing;
                pose *= Quaternion.Euler(25f * swing, -20f * swing, 40f * swing);
            }
            else if (mode == WeaponAttackMode.Bow)
            {
                left = new Vector3(-0.18f, -0.19f, 0.58f);
                right = new Vector3(0.19f, -0.21f, 0.42f - draw * 0.19f);
                pose = Quaternion.Euler(0, -5, -8);
            }
            else if (mode == WeaponAttackMode.Staff)
            {
                right = new Vector3(0.3f, -0.28f, 0.5f);
                left = Vector3.Lerp(left, new Vector3(-0.22f, -0.16f, 0.52f), chargePose);
                pose = Quaternion.Euler(Mathf.Lerp(12f, 35f, chargePose), -8, -18);
            }

            // 벽에 가까우면 손을 낮춥니다. 실제 발사/피해의 벽 차단 검사는 기존 전투가 담당합니다.
            if (Projectile.TryGetFirstHit(new Ray(transform.position, transform.forward), 0.75f,
                combat.CollisionLayers, player, out RaycastHit wall))
            {
                float lower = 1f - wall.distance / 0.75f;
                right += new Vector3(0, -0.28f, -0.2f) * lower;
                left += new Vector3(0, -0.28f, -0.2f) * lower;
            }
            arms.Pose(transform, right, left);
            arms.Grip(transform, pose, mode == WeaponAttackMode.Bow, mode == WeaponAttackMode.Staff);
            if (weaponModel != null)
            {
                Transform hand = mode == WeaponAttackMode.Bow ? arms.LeftHand : arms.RightHand;
                weaponModel.transform.rotation = transform.rotation * pose * item.EquippedVisual.transform.localRotation;
                weaponModel.transform.position = hand.TransformPoint(new Vector3(mode == WeaponAttackMode.Bow ? -0.045f : 0.045f, -0.015f, 0.015f)) - weaponModel.transform.TransformVector(grip);
                if (trail != null) trail.emitting = !owner.InputBlocked && progress > 0.32f && progress < 0.65f;
                ApplyMuzzle();
            }
            if (charge != null)
            {
                charge.transform.position = muzzle != null ? muzzle.position : arms.RightHand.position;
                if (combat.IsCharging && !charge.isPlaying) charge.Play();
                else if (!combat.IsCharging && charge.isPlaying) charge.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private bool EnsureArms()
        {
            if (arms != null) return true;
            GameObject prefab = Resources.Load<GameObject>("VillageFirstPersonArms");
            if (prefab == null) return false;
            root = new GameObject("First Person View");
            root.transform.SetParent(transform, false);
            arms = new FirstPersonArms(root.transform, prefab);

            charge = WeaponVfx.CreateParticles(root.transform, "Hand Charge", WeaponVfx.Glow);
            charge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = charge.main;
            main.loop = true;
            main.startLifetime = 0.25f;
            main.startSpeed = 0.03f;
            main.startSize = 0.025f;
            main.maxParticles = 24;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = charge.emission;
            emission.enabled = true;
            emission.rateOverTime = 22f;

            var shape = charge.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            return true;
        }

        private void ChangeWeapon(ItemDefinition item)
        {
            RestoreMuzzle();
            Cancel();
            if (weaponModel != null) Destroy(weaponModel);

            displayed = item;
            draw = chargePose = 0f;
            muzzle = null;
            trail = null;
            weaponModel = item != null && item.EquippedVisual != null ? Instantiate(item.EquippedVisual, root.transform) : null;
            if (weaponModel == null) return;

            weaponModel.name = "First Person " + item.ItemId;
            weaponModel.transform.localScale *= item.AttackMode == WeaponAttackMode.Melee ? 0.36f : 0.65f;
            foreach (Collider collider in weaponModel.GetComponentsInChildren<Collider>()) collider.enabled = false;
            MeshFilter mesh = weaponModel.GetComponentInChildren<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null) return;

            Bounds bounds = mesh.sharedMesh.bounds;
            Vector3 localGrip = bounds.center;
            if (item.AttackMode == WeaponAttackMode.Melee) localGrip.y = bounds.min.y + bounds.size.y * 0.18f;
            grip = weaponModel.transform.InverseTransformPoint(mesh.transform.TransformPoint(localGrip));
            muzzle = new GameObject("View Weapon Tip").transform;
            muzzle.SetParent(mesh.transform, false);
            muzzle.localPosition = item.AttackMode == WeaponAttackMode.Bow ? bounds.center :
                new Vector3(bounds.center.x, bounds.max.y - 0.02f, bounds.center.z);
            if (item.AttackMode == WeaponAttackMode.Melee)
            {
                trail = muzzle.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = WeaponVfx.Glow;
                trail.time = 0.1f;
                trail.startWidth = 0.035f;
                trail.endWidth = 0f;
                trail.startColor = new Color(1f, 0.82f, 0.45f, 0.4f);
                trail.endColor = Color.clear;
                trail.emitting = false;
            }
        }

        private void ApplyMuzzle()
        {
            if (muzzle == null || (displayed.AttackMode != WeaponAttackMode.Bow && displayed.AttackMode != WeaponAttackMode.Staff)) return;
            if (combat.WeaponMuzzle != muzzle) previousMuzzle = combat.WeaponMuzzle;
            combat.WeaponMuzzle = muzzle;
        }

        private void RestoreMuzzle()
        {
            if (combat != null && combat.WeaponMuzzle == muzzle) combat.WeaponMuzzle = previousMuzzle;
            previousMuzzle = null;
        }

        private void OnDisable() { RestoreMuzzle(); if (root != null) root.SetActive(false); }
        private void OnDestroy()
        {
            if (combat != null)
            {
                combat.MeleeStarted -= Melee;
                combat.Attacked -= Attack;
                combat.MeleeCancelled -= Cancel;
                combat.ChargeCancelled -= Cancel;
            }
            RestoreMuzzle();
            if (root != null) Destroy(root);
        }
    }
}
