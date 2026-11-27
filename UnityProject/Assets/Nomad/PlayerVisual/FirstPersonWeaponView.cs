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
        private FirstPersonViewCamera renderCamera;
        private FirstPersonBow bow;
        private Transform gripSocket;
        private float strikeAt = -100f;
        private float releasedAt = -100f;
        private bool awaitingStrike;
        private float windup;
        private Renderer originalCharge;
        private bool originalChargeHidden;
        private float started = -100f;
        private float duration = 0.5f;
        private float draw;
        private float chargePose;
        private TrailRenderer trail;
        private ParticleSystem charge;
        public bool HasArms => arms != null;
        public GameObject WeaponModel => weaponModel;
        public Camera RenderCamera => renderCamera?.Camera;

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
            windup = player.GetComponent<IMeleeAttackTiming>()?.GetWindup(item) ?? 0f;
            awaitingStrike = true;
            strikeAt = -100f;
        }

        private void Attack(WeaponAttackMode mode)
        {
            if (!owner.IsActive) return;
            if (mode == WeaponAttackMode.Melee)
            {
                awaitingStrike = false;
                strikeAt = Time.time;
            }
            else
            {
                releasedAt = Time.time;
                if (mode == WeaponAttackMode.Staff && charge != null)
                {
                    charge.Emit(new ParticleSystem.EmitParams { startSize = 0.12f, startLifetime = 0.08f,
                        startColor = new Color(0.7f, 0.9f, 1f, 0.7f) }, 1);
                    charge.Emit(6);
                }
            }
        }

        private void Cancel()
        {
            started = strikeAt = releasedAt = -100f;
            awaitingStrike = false;
            if (trail != null) { trail.emitting = false; trail.Clear(); }
        }

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
                renderCamera?.SetActive(false);
                SetOriginalCharge(false);
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
            renderCamera.SetActive(true);
            SetOriginalCharge(true);
            ItemDefinition item = combat.CurrentWeapon;
            if (displayed != item) ChangeWeapon(item);

            WeaponAttackMode mode = item != null ? item.AttackMode : WeaponAttackMode.Melee;
            draw = Mathf.MoveTowards(draw, combat.IsCharging ? combat.ChargeProgress : 0f, Time.deltaTime * 6f);
            chargePose = Mathf.MoveTowards(chargePose, combat.IsCharging ? 1f : 0f, Time.deltaTime * 8f);
            float sinceStrike = Time.time - strikeAt;
            float sinceRelease = Time.time - releasedAt;
            Vector3 right = new Vector3(0.36f, -0.26f, 0.74f);
            Vector3 left = new Vector3(-0.45f, -0.65f, 0.35f);
            Quaternion pose = Quaternion.Euler(-12, 25, -10);
            if (mode == WeaponAttackMode.Melee)
            {
                // 타격 이벤트에서 베기가 중심을 통과하고, 준비/복귀는 화면 가장자리로 이어집니다.
                if (awaitingStrike)
                {
                    float elapsed = Time.time - started;
                    float prep = Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(0.04f, windup - 0.08f));
                    right += new Vector3(0.07f, 0.07f, -0.12f) * prep;
                    pose *= Quaternion.Euler(-30 * prep, -20 * prep, -30 * prep);
                    float cut = Mathf.SmoothStep(0, 1, (elapsed - windup + 0.08f) / 0.08f);
                    right = Vector3.Lerp(right, new Vector3(-0.15f, -0.17f, 0.82f), cut);
                    pose = Quaternion.Slerp(pose, Quaternion.Euler(45, -40, 65), cut);
                }
                else if (sinceStrike < 0.34f)
                {
                    float recover = Mathf.SmoothStep(0, 1, sinceStrike / 0.34f);
                    right = Vector3.Lerp(new Vector3(-0.15f, -0.17f, 0.82f), right, recover);
                    pose = Quaternion.Slerp(Quaternion.Euler(45, -40, 65), pose, recover);
                }
            }
            else if (mode == WeaponAttackMode.Bow)
            {
                left = Vector3.Lerp(new Vector3(-0.27f, -0.27f, 0.8f), new Vector3(-0.13f, -0.08f, 0.68f), chargePose);
                right = Vector3.Lerp(new Vector3(-0.16f, -0.24f, 0.72f), new Vector3(0.1f, -0.16f, 0.4f), draw);
                pose = Quaternion.Euler(-3, 0, -8);
            }
            else if (mode == WeaponAttackMode.Staff)
            {
                right = new Vector3(0.39f, -0.3f, 0.78f);
                left = Vector3.Lerp(left, new Vector3(-0.28f, -0.18f, 0.68f), chargePose);
                float recoil = sinceRelease < 0.25f ? Mathf.Sin(sinceRelease / 0.25f * Mathf.PI) : 0f;
                pose = Quaternion.Euler(Mathf.Lerp(-8, 7, chargePose) - recoil * 12f, 15, -12);
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
                if (trail != null) trail.emitting = !owner.InputBlocked && !awaitingStrike && sinceStrike < 0.1f;
                bow?.Pose(arms.RightHand.TransformPoint(new Vector3(0.09f, 0.035f, 0)),
                    transform.position + transform.forward * 30f, combat.ArrowCount > 0 && sinceRelease > 0.18f);
                ApplyMuzzle();
            }
            if (charge != null)
            {
                charge.transform.position = muzzle != null ? muzzle.position : arms.RightHand.position;
                if (mode == WeaponAttackMode.Staff && WeaponVfx.TryGetStyle(item, out WeaponVfxStyle style))
                {
                    var main = charge.main;
                    main.startColor = style.Glow;
                    main.startSize = Mathf.Lerp(0.018f, 0.04f, draw);
                    var emission = charge.emission;
                    emission.rateOverTime = Mathf.Lerp(8f, 32f, draw);
                }
                if (combat.IsCharging && mode == WeaponAttackMode.Staff && !charge.isPlaying) charge.Play();
                else if ((!combat.IsCharging || mode != WeaponAttackMode.Staff) && charge.isPlaying) charge.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
            renderCamera = new FirstPersonViewCamera(GetComponent<Camera>());

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
            shape.radius = 0.025f;
            main.startColor = new Color(0.3f, 0.65f, 1f, 0.8f);
            WeaponVfx.Fade(charge, 0f);
            FirstPersonViewCamera.SetLayer(root);
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
            bow = null;
            if (gripSocket != null) Destroy(gripSocket.gameObject);
            gripSocket = null;
            weaponModel = null;
            if (item == null || item.EquippedVisual == null) return;

            bool isBow = item.AttackMode == WeaponAttackMode.Bow;
            Transform hand = isBow ? arms.LeftHand : arms.RightHand;
            gripSocket = new GameObject("Palm Grip Socket").transform;
            gripSocket.SetParent(hand, false);
            gripSocket.localPosition = isBow ? new Vector3(-0.09f, -0.035f, 0) : new Vector3(0.09f, 0.035f, 0);
            gripSocket.localRotation = Quaternion.Inverse(Quaternion.Euler(90, 0, isBow ? -90 : 90));
            weaponModel = Instantiate(item.EquippedVisual, gripSocket);

            weaponModel.name = "First Person " + item.ItemId;
            foreach (Collider collider in weaponModel.GetComponentsInChildren<Collider>()) collider.enabled = false;
            MeshFilter mesh = weaponModel.GetComponentInChildren<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null) return;

            Bounds bounds = mesh.sharedMesh.bounds;
            float length = item.AttackMode == WeaponAttackMode.Staff ? 1.25f : isBow ? 1.05f : 0.82f;
            float currentLength = mesh.transform.TransformVector(Vector3.up * bounds.size.y).magnitude;
            weaponModel.transform.localScale *= length / Mathf.Max(0.01f, currentLength);
            Vector3 localGrip = bounds.center;
            if (item.AttackMode == WeaponAttackMode.Melee) localGrip.y = bounds.min.y + bounds.size.y * 0.12f;
            Vector3 grip = weaponModel.transform.InverseTransformPoint(mesh.transform.TransformPoint(localGrip));
            weaponModel.transform.localPosition = -(weaponModel.transform.localRotation * Vector3.Scale(grip, weaponModel.transform.localScale));
            if (isBow) bow = new FirstPersonBow(weaponModel.transform, mesh);
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
            FirstPersonViewCamera.SetLayer(gripSocket.gameObject);
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

        private void SetOriginalCharge(bool hidden)
        {
            if (originalCharge == null && player != null)
            {
                originalCharge = player.Find("Charge Gathering")?.GetComponentInChildren<ParticleSystemRenderer>();
                if (originalCharge != null) originalChargeHidden = originalCharge.forceRenderingOff;
            }
            if (originalCharge != null) originalCharge.forceRenderingOff = hidden || originalChargeHidden;
        }

        private void OnDisable() { SetOriginalCharge(false); RestoreMuzzle(); renderCamera?.SetActive(false); if (root != null) root.SetActive(false); }
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
            SetOriginalCharge(false);
            renderCamera?.Dispose();
            if (root != null) Destroy(root);
        }
    }
}
