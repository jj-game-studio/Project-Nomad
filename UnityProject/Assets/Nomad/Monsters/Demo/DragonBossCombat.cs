using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public enum DragonPattern { Basic, Circle, RadialFireballs, Breath, FirePool, Charge, Rain }
    public enum DragonPhase { Waiting, Windup, Active, Recovery, Stunned, Dead, Arriving }

    [DefaultExecutionOrder(120)]
    public sealed partial class DragonBossCombat : MonoBehaviour
    {
        public const int RainWaves = 20;
        public const int RainPerWave = 3;
        public const float RainInterval = 0.35f;
        public const float CircleRadius = 12f;
        public const float BreathRange = 18;
        public const float BreathAngle = 110;
        public const float RainDuration = 7.65f;
        public const int RadialFireballCount = 72;
        public bool AutomaticPatterns { get; set; } = true;
        public int AttacksStarted { get; private set; }
        public int RainProjectilesSpawned { get; private set; }
        public DragonPattern Pattern { get; private set; }
        public DragonPhase Phase { get; private set; }
        public float Remaining => Mathf.Max(0, until - Time.time);
        public int RockStuns { get; private set; }
        public Transform Effects => effects;
        public bool Introduced => arrival == null || arrival.Landed;
        public BossCombatTuning Tuning { get; private set; } = new BossCombatTuning();
        public bool Enraged => health != null && !health.IsDead && health.CurrentHealth <= health.MaxHealth * 0.25f;
        public float Tempo => Enraged ? 1.3f : 1;
        public float MoveSpeed => Enraged ? 8 : 6;
        public float LeapDuration => BossJumpSlam.LeapSeconds / Tempo;
        public int ParrySuccesses { get; private set; }
        private BossParrySignal parrySignal;
        private BossHazard slam;
        private bool radialParried;
        private bool radialHitPlayer;
        private bool pendingSlamCounter;
        private BossArrival arrival;
        private BossArena arena;
        private Health health, target;
        private MonsterTracker tracker;
        private MonsterTactics tactics;
        private NavMeshAgent agent;
        private MonsterAnimationDriver animationDriver;
        private Transform effects;
        private float until, nextRain;
        private float phaseStarted, phaseDuration;
        private Vector3 aim, direction;
        private int sequence, rainCount;
        private TextMesh status;
        private static readonly DragonPattern[] Rotation = { DragonPattern.Circle, DragonPattern.RadialFireballs,
            DragonPattern.Basic, DragonPattern.Breath, DragonPattern.FirePool, DragonPattern.Rain };

        public void Configure(BossArena room, Health victim)
        {
            arena = room;
            target = victim;
            health = GetComponent<Health>();
            BossCombatTuning defaults = FindFirstObjectByType<BossEncounterContent>()?.Tuning;
            Tuning = defaults != null ? JsonUtility.FromJson<BossCombatTuning>(JsonUtility.ToJson(defaults)) : new BossCombatTuning();
            tracker = GetComponent<MonsterTracker>();
            agent = GetComponent<NavMeshAgent>();
            tactics = GetComponent<MonsterTactics>();
            animationDriver = GetComponent<MonsterAnimationDriver>();
            GetComponent<MonsterMeleeAttack>().enabled = false;
            tactics.Configure(target, 0, BossArena.HalfSize * 2.9f, 15, 6, 6.5f, 8, 0.8f, 0.1f, 28);
            tracker.enabled = false;
            GetComponent<BossArmor>().Broken += ArmorBroken;
            gameObject.AddComponent<BossKnockdownAnimation>();
            gameObject.AddComponent<BossJumpSlam>().Configure(this, arena);
            agent.speed = 6;
            agent.acceleration = 18;
            agent.angularSpeed = TurnSpeed;
            agent.updateRotation = false;
            agent.stoppingDistance = 3.2f;
            ConfigurePresentation();
            gameObject.AddComponent<BossRadialChargePose>().Configure(this);
            parrySignal = gameObject.AddComponent<BossParrySignal>();
            parrySignal.Configure(this, target);
            gameObject.AddComponent<BossVisualStyle>().Configure(this);
            effects = new GameObject("Dragon encounter effects").transform;
            effects.SetParent(arena.transform, false);
            status = new GameObject("Boss action").AddComponent<TextMesh>();
            status.transform.SetParent(transform, false);
            status.transform.localPosition = Vector3.up * 4.2f;
            status.font = Resources.Load<Font>("ShopFonts/Jua-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            status.GetComponent<Renderer>().sharedMaterial = status.font.material;
            status.anchor = TextAnchor.MiddleCenter;
            status.fontSize = 40;
            status.characterSize = 0.025f;
            status.color = new Color(1, 0.7f, 0.2f);
            until = Time.time + 0.2f;
            arrival = gameObject.AddComponent<BossArrival>();
            arrival.Configure(this, arena, target);
            if (FindFirstObjectByType<BossEncounterContent>()?.DropEntrance == false) arrival.Skip();
            gameObject.AddComponent<BossDeathPresentation>();
        }

        private bool PlayerInside => target != null && target.gameObject.activeInHierarchy && !target.IsDead &&
            Mathf.Abs((target.transform.position - arena.Center).y) < 8 &&
            Mathf.Max(Mathf.Abs(Vector3.Dot(target.transform.position - arena.Center, arena.Forward)),
                Mathf.Abs(Vector3.Dot(target.transform.position - arena.Center, Vector3.Cross(Vector3.up, arena.Forward)))) < BossArena.HalfSize + (engaged ? 8 : 0);

        private void Update()
        {
            if (health == null) return;
            if (health.IsDead) { Phase = DragonPhase.Dead; StopEncounter(); return; }
            if (!Introduced)
            {
                Pause();
                if (!arena.IsLocked && PlayerInside && Vector3.Distance(target.transform.position, arena.Center) <= 14)
                {
                    Phase = DragonPhase.Arriving;
                    arrival.Begin();
                }
                return;
            }
            if (animationDriver != null && (Phase == DragonPhase.Windup || Phase == DragonPhase.Active))
            {
                float progress = Pattern == DragonPattern.Rain && Phase == DragonPhase.Active
                    ? Mathf.PingPong((Time.time - phaseStarted) * 1.8f, 1) : Mathf.Clamp01((Time.time - phaseStarted) / phaseDuration);
                animationDriver.ActionNormalizedTime = Mathf.Lerp(Phase == DragonPhase.Windup ? 0.03f : 0.3f,
                    Phase == DragonPhase.Windup ? 0.28f : 0.9f, progress);
            }
            if (arena.IsLocked || !PlayerInside)
            {
                engaged = false;
                pursuitStarted = -1;
                if (Phase != DragonPhase.Waiting) StopEncounter();
                Phase = DragonPhase.Waiting;
                Pause();
                until = Time.time;
                return;
            }
            if (Phase == DragonPhase.Stunned)
            {
                if (animationDriver != null) animationDriver.ActionNormalizedTime = Mathf.Clamp01((Time.time - phaseStarted) / phaseDuration);
                if (Time.time < until) return;
                Recover();
                return;
            }
            UpdateFacing();
            if (Phase == DragonPhase.Active && Pattern == DragonPattern.Charge)
            {
                if (HitRock()) return;
                if (TickBossCharge()) Recover();
                return;
            }
            if (Phase == DragonPhase.Active && Pattern == DragonPattern.Rain && Time.time >= nextRain && rainCount < Tuning.RainWaves)
            {
                nextRain = Time.time + Tuning.RainInterval / Tempo;
                for (int index = 0; index < Tuning.RainPerWave; index++)
                {
                    float distance = index == 0 ? 0 : index == 1 ? 2.6f : 4.2f;
                    Vector3 point = ClampPoint(target.transform.position + Quaternion.Euler(0, rainCount * 137.5f + index * 120, 0) * Vector3.forward * distance);
                    float fall = Tuning.RainFallSeconds / Tempo;
                    Area(point, Tuning.RainRadius, 360, fall, 0.12f, 20);
                    BossFireball.Launch(effects, transform, target, point + Vector3.up * 10, point, fall, false, damage: 0,
                        radius: Tuning.RainRadius * 0.65f);
                    RainProjectilesSpawned++;
                }
                rainCount++;
            }
            if (Time.time < until) return;
            if (Phase == DragonPhase.Windup) Release();
            else if (Phase == DragonPhase.Active) Recover();
            else if (AutomaticPatterns) BeginNext();
        }

        public void BeginPattern(DragonPattern pattern)
        {
            if (health == null || health.IsDead || !Introduced || !PlayerInside || arena.IsLocked ||
                (Phase == DragonPhase.Stunned && Time.time < until)) return;
            tactics.Stop();
            Pause();
            parrySignal.Clear();
            pendingSlamCounter = false;
            Pattern = pattern;
            AttacksStarted++;
            Phase = DragonPhase.Windup;
            aim = ClampPoint(target.transform.position);
            direction = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            ChargeAimLocked = false;
            float windup = pattern == DragonPattern.Circle ? Tuning.SlamWindup : pattern == DragonPattern.Basic ? 0.45f :
                pattern == DragonPattern.RadialFireballs ? 1.4f : pattern == DragonPattern.Charge ? Tuning.ChargeWindup :
                pattern == DragonPattern.Rain ? 0.7f : pattern == DragonPattern.Breath ? 1.6f : 0.9f;
            windup /= Tempo;
            float turning = Vector3.Angle(transform.forward, direction) / (pattern == DragonPattern.Charge ? ChargeTurnSpeed : TurnSpeed);
            windup = Mathf.Max(windup, turning + (pattern == DragonPattern.Charge ? ChargeLockSeconds : 0.12f));
            until = Time.time + windup;
            phaseStarted = Time.time;
            phaseDuration = windup;
            SetMotion("Attack_A", 1f / (windup + 0.5f));
            status.text = pattern == DragonPattern.RadialFireballs ? "" : pattern == DragonPattern.Charge ? "돌진 준비" :
                pattern == DragonPattern.Breath ? "브레스 준비" : pattern == DragonPattern.Rain ? "화염우" : "";
            if (pattern == DragonPattern.Circle)
            {
                slam = Area(transform.position, Tuning.SlamRadius, 360, windup + LeapDuration, 0.12f, 32);
                slam.WarningSeconds = windup;
                slam.EmitsFlame = false;
                parrySignal.Arm(Time.time + windup + LeapDuration, Tuning.SlamRadius + 0.5f);
            }
            if (pattern == DragonPattern.RadialFireballs)
            {
                radialParried = false;
                radialHitPlayer = false;
                float distance = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).magnitude;
                parrySignal.Arm(Time.time + windup + Mathf.Max(0, distance - 3 - Tuning.RadialRadius) / (Tuning.RadialSpeed * Tempo), Tuning.RadialRange);
            }
            if (pattern == DragonPattern.Basic) Area(transform.position, Tuning.BasicRange, 110, windup, 0.12f, 18);
            if (pattern == DragonPattern.Breath) Area(transform.position, Tuning.BreathRange, Tuning.BreathAngle, windup, 2.4f / Tempo, 12, 0.8f / Tempo);
            if (pattern == DragonPattern.FirePool)
            {
                PlanFirePools(windup);
            }
            if (pattern == DragonPattern.Charge)
            {
                PlannedChargeDistance = Mathf.Clamp(Vector3.Distance(transform.position, aim) + ChargeOvershoot, MinimumChargeDistance, BossArena.HalfSize * 2.9f);
                chargeWarning = BossChargeWarning.Create(effects);
                chargeWarning.GroundHeight = arena.GroundHeight;
                chargeWarning.Refresh(transform.position, direction, Mathf.Min(PlannedChargeDistance, arena.DistanceToBoundary(transform.position, direction)), 0, false);
            }
        }

        private void BeginNext()
        {
            bool chargeDue = Time.time >= nextCharge;
            DragonPattern next = chargeDue ? DragonPattern.Charge : Rotation[sequence % Rotation.Length];
            float engageRange = chargeDue ? float.PositiveInfinity : next == DragonPattern.Basic ? 3.8f : next == DragonPattern.Circle ? 5.5f : 9;
            if (Vector3.Distance(transform.position, target.transform.position) > engageRange)
            {
                if (pursuitStarted < 0) pursuitStarted = Time.time;
                if (Time.time - pursuitStarted < 2.5f)
                {
                    PursuePlayer();
                    until = Time.time + 0.08f;
                    return;
                }
                // 경로가 막혀도 영원히 서 있지 않고 원거리 패턴으로 이어갑니다.
                next = DragonPattern.RadialFireballs;
            }
            pursuitStarted = -1;
            if (!chargeDue) sequence++;
            BeginPattern(next);
        }

        private void Release()
        {
            status.text = "";
            Phase = DragonPhase.Active;
            until = Time.time + (Pattern == DragonPattern.Breath ? 2.4f : Pattern == DragonPattern.Rain ? RainDuration :
                Pattern == DragonPattern.Circle ? LeapDuration + 0.12f : Pattern == DragonPattern.RadialFireballs ? Tuning.RadialRange / (Tuning.RadialSpeed * Tempo) + 0.3f : 0.18f);
            if (Pattern == DragonPattern.Rain)
                until = Time.time + (Tuning.RainWaves - 1) * Tuning.RainInterval / Tempo + Tuning.RainFallSeconds / Tempo + 0.15f;
            if (Pattern == DragonPattern.Breath) until = Time.time + 2.4f / Tempo;
            phaseStarted = Time.time;
            phaseDuration = until - Time.time;
            if (Pattern == DragonPattern.RadialFireballs && !radialParried)
                for (int index = 0; index < Tuning.RadialCount; index++)
                {
                    Vector3 heading = Quaternion.Euler(0, index * 360f / Tuning.RadialCount, 0) * transform.forward;
                    Vector3 start = arena.GroundPoint(transform.position + heading * 3) + Vector3.up * 1.2f;
                    Vector3 end = arena.GroundPoint(transform.position + heading * Tuning.RadialRange) + Vector3.up * 1.2f;
                    BossFireball ball = BossFireball.Launch(effects, transform, target, start, end, (Tuning.RadialRange - 3) / (Tuning.RadialSpeed * Tempo), false,
                        damage: ScaleDamage(25), radius: Tuning.RadialRadius);
                    ball.SignalControlled = true;
                }
            if (Pattern == DragonPattern.FirePool)
                foreach (Vector3 point in firePoolTargets)
                    BossFireball.Launch(effects, transform, target, Mouth.position, point, 0.75f, true);
            if (Pattern == DragonPattern.Charge)
            {
                direction = transform.forward;
                ChargeAimLocked = true;
                if (chargeWarning != null) Destroy(chargeWarning.gameObject);
                tactics.Prepare(MonsterSkill.Charge, transform.position + direction * PlannedChargeDistance);
                nextCharge = Time.time + Tuning.ChargeInterval;
                if (tactics.StartCharge(direction))
                {
                    ChargesReleased++;
                    dashRemaining = PlannedChargeDistance;
                    dashStarted = Time.time;
                    dashHitPlayer = false;
                }
                else Recover();
            }
            if (Pattern == DragonPattern.Rain) { rainCount = 0; RainProjectilesSpawned = 0; nextRain = Time.time; }
        }

        private BossHazard Area(Vector3 point, float radius, float angle, float delay, float duration, int damage, float interval = 0.75f)
        {
            BossHazard hazard = BossHazard.Create(effects, transform, ClampPoint(point), Quaternion.LookRotation(direction), target, radius, angle, delay, duration, ScaleDamage(damage), interval);
            hazard.GroundHeight = arena.GroundHeight;
            hazard.EmitsFlame = Pattern != DragonPattern.Basic && damage > 0;
            if (Pattern == DragonPattern.Breath)
            {
                hazard.BreathOrigin = Mouth;
                hazard.BreathTravelSeconds = Tuning.BreathTravelSeconds / Tempo;
                hazard.InnerRadius = Tuning.BreathSafeRadius;
                hazard.VisualRangeMultiplier = 1.2f;
                hazard.HideWarningWhileActive = true;
            }
            return hazard;
        }

        private Vector3 ClampPoint(Vector3 point)
        {
            Quaternion rotation = Quaternion.LookRotation(arena.Forward);
            Vector3 local = Quaternion.Inverse(rotation) * (point - arena.Center);
            local.x = Mathf.Clamp(local.x, -BossArena.HalfSize + 3, BossArena.HalfSize - 3);
            local.z = Mathf.Clamp(local.z, -BossArena.HalfSize + 3, BossArena.HalfSize - 3);
            return arena.GroundPoint(arena.Center + rotation * new Vector3(local.x, 0, local.z));
        }

        private bool HitRock()
        {
            float radius = GetComponent<CapsuleCollider>().radius;
            foreach (RaycastHit hit in Physics.SphereCastAll(transform.position + Vector3.up * radius, radius, direction,
                Tuning.ChargeSpeed * Tempo * Time.deltaTime + 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BossBreakableRock>() is BossBreakableRock rock && rock.Shatter())
                {
                    RockStuns++;
                    GetComponent<BossArmor>().DamageArmor(BossArmor.DurabilityPerLayer * 0.65f);
                    if (Phase != DragonPhase.Stunned) Stun(2.8f);
                    return true;
                }
            return false;
        }

        public void Stun(float seconds)
        {
            if (health == null || health.IsDead || !Introduced) return;
            CancelEffects();
            parrySignal.Clear();
            tactics.Stop();
            Pause();
            Phase = DragonPhase.Stunned;
            until = Time.time + seconds;
            phaseStarted = Time.time;
            phaseDuration = seconds;
            status.text = "";
            status.color = new Color(1, 0.9f, 0.3f);
            SetMotion("BossKnockdown", 1);
        }

        private void ArmorBroken(int stage) => Stun(5);

        private Vector3[] firePoolTargets = new Vector3[4];
        private void PlanFirePools(float windup)
        {
            firePoolTargets = new Vector3[Tuning.FirePoolCount];
            for (int index = 0; index < firePoolTargets.Length; index++)
            {
                Vector3 offset = Quaternion.Euler(0, index * 360f / firePoolTargets.Length + 45, 0) * direction * (index % 2 == 0 ? 6 : 11);
                firePoolTargets[index] = ClampPoint(transform.position + offset);
                Area(firePoolTargets[index], Tuning.FirePoolRadius, 360, windup + 0.75f, 6, 10);
            }
        }

        public int ScaleDamage(int amount) => Mathf.RoundToInt(amount * (Enraged ? 1.4f : 1));

        public void HitRadialWave(int damage)
        {
            if (radialHitPlayer) return;
            radialHitPlayer = true;
            target.TryApplyDamage(damage);
        }

        public void ResolveParry()
        {
            ParrySuccesses++;
            bool circle = Pattern == DragonPattern.Circle;
            if (circle)
            {
                if (slam != null) slam.CancelDamage();
                pendingSlamCounter = true;
            }
            else
            {
                radialParried = true;
                foreach (BossFireball ball in effects.GetComponentsInChildren<BossFireball>()) ball.Disperse();
                BossFireball reflected = BossFireball.Launch(effects, transform, target,
                    target.transform.position + Vector3.up * 1.3f, transform.position + Vector3.up * 1.8f, 0.4f, false);
                reflected.Reflect(target.transform);
            }
            arena.ScatterParryRocks(target.transform.position, transform.position, 2 + ParrySuccesses % 2);
        }

        public void FinishParriedSlam()
        {
            if (!pendingSlamCounter) return;
            pendingSlamCounter = false;
            health.TryApplyCombatImpact(80, false, CombatImpactSource.ParriedSlam);
        }

        private void Recover()
        {
            Phase = DragonPhase.Recovery;
            until = Time.time;
            status.text = "";
            SetMotion(null, 1);
            if (AutomaticPatterns) BeginNext();
        }

        public void FinishArrival()
        {
            Phase = DragonPhase.Waiting;
            until = Time.time + 0.2f;
        }

        private void SetMotion(string state, float speed)
        {
            if (animationDriver == null) return;
            animationDriver.ActionState = state;
            animationDriver.ActionSpeed = speed;
            animationDriver.ActionNormalizedTime = state == null ? -1 : state == "Hit_A" ? 0.4f : 0.03f;
            animationDriver.ActionSequence++;
        }

        private void Pause()
        {
            tracker.enabled = false;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        }

        private void CancelEffects()
        {
            if (effects == null) return;
            foreach (Transform child in effects) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }

        private void StopEncounter()
        {
            parrySignal?.Clear();
            CancelEffects();
            tactics?.Stop();
            if (status != null) status.text = "";
            SetMotion(null, 1);
        }

        private void LateUpdate() { if (status != null && Camera.main != null) status.transform.rotation = Camera.main.transform.rotation; }
        private void OnDisable() { StopEncounter(); }
        private void OnDestroy()
        {
            if (TryGetComponent(out BossArmor armor)) armor.Broken -= ArmorBroken;
            if (effects != null) Destroy(effects.gameObject);
        }
    }
}
