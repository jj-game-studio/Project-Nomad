using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public enum DragonPattern { Basic, Circle, RadialFireballs, Breath, FirePool, Charge, Rain, BodySweep, Rockfall, OrbitFlames, LavaEruption }
    public enum DragonPhase { Waiting, Windup, Active, Recovery, Stunned, Dead, Arriving, Enraging }

    [DefaultExecutionOrder(120)]
    public sealed partial class DragonBossCombat : MonoBehaviour
    {
        public const int RainWaves = 20;
        public const int RainPerWave = 3;
        public const float RainInterval = 0.35f;
        public const float CircleRadius = 18f;
        public const float BreathRange = 22;
        public const float BreathAngle = 120;
        public const float RainDuration = 7.65f;
        public const int RadialFireballCount = 12;
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
        public bool EnrageRequested => health != null && !health.IsDead && health.CurrentHealth <= health.MaxHealth * 0.25f;
        public bool Enraged => enrage != null && enrage.Completed && health != null && !health.IsDead;
        public float EnrageAmount => enrage != null ? enrage.VisualAmount : 0;
        public bool EnrageTransitioning => Phase == DragonPhase.Enraging;
        private BossEnrageTransition enrage;
        public float Tempo => Enraged ? 1.3f : 1;
        public float MoveSpeed => Enraged ? 8 : 6;
        public float LeapDuration => BossJumpSlam.LeapSeconds / Tempo;
        public int ParrySuccesses { get; private set; }
        private BossParrySignal parrySignal;
        private BossHazard slam;
        private readonly System.Collections.Generic.HashSet<int> damagedVolleys = new();
        private int volleySerial, volleysSent;
        private float nextVolley;
        private BossHazard breath;
        public int VolleysReleased { get; private set; }
        public float DashSpeed => Tuning.ChargeSpeed * (Enraged ? 1.65f : 1);
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
        private int rainCount;
        private TextMesh status;
        private static readonly DragonPattern[] Rotation = { DragonPattern.Circle, DragonPattern.RadialFireballs,
            DragonPattern.Breath, DragonPattern.FirePool, DragonPattern.Rain, DragonPattern.OrbitFlames };

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
            gameObject.AddComponent<BossPatternMotion>().Configure(this);
            gameObject.AddComponent<BossBodySweepMotion>().Configure(this);
            backstep = gameObject.AddComponent<BossBreathBackstep>();
            backstep.Configure(this, arena);
            enrage = gameObject.AddComponent<BossEnrageTransition>();
            enrage.Configure(this, target);
            parrySignal = gameObject.AddComponent<BossParrySignal>();
            parrySignal.Configure(this, target);
            gameObject.AddComponent<BossVisualStyle>().Configure(this);
            effects = new GameObject("Dragon encounter effects").transform;
            effects.SetParent(arena.transform, false);
            background = new GameObject("Independent boss hazards").transform;
            background.SetParent(arena.transform, false);
            BossOrbitingFlames.Create(background, this, arena, target, 0, float.PositiveInfinity);
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
            nextCharge = Time.time + Tuning.ChargeInterval;
            nextRockfall = Time.time + Tuning.RockfallInterval;
            arrival = gameObject.AddComponent<BossArrival>();
            arrival.Configure(this, arena, target);
            if (FindFirstObjectByType<BossEncounterContent>()?.DropEntrance == false) arrival.Skip();
            gameObject.AddComponent<BossDeathPresentation>();
            gameObject.AddComponent<BossBattleHud>().Configure(this, target);
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
            if (EnrageTransitioning) return;
            TickIndependentHazards();
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
                nextRain = Time.time + Tuning.RainInterval;
                for (int index = 0; index < Tuning.RainPerWave; index++)
                {
                    float distance = index == 0 ? 0 : index == 1 ? 2.6f : 4.2f;
                    Vector3 point = ClampPoint(target.transform.position + Quaternion.Euler(0, rainCount * 137.5f + index * 120, 0) * Vector3.forward * distance);
                    float fall = Tuning.RainFallSeconds;
                    Area(point, Tuning.RainRadius, 360, fall, 0.12f, 20);
                    bool burning = Enraged && Random.value < Tuning.EnragedRainPoolChance;
                    BossFireball.Launch(effects, transform, target, point + Vector3.up * 10, point, fall, false,
                        impact: burning ? impact => Area(impact, 1.8f, 360, 0, 4, 8) : null, damage: 0,
                        radius: Tuning.RainRadius * 0.65f);
                    RainProjectilesSpawned++;
                }
                rainCount++;
            }
            if (Phase == DragonPhase.Active && Pattern == DragonPattern.RadialFireballs && Enraged && volleysSent < 2 && Time.time >= nextVolley)
                FireRadialVolley();
            if (Phase == DragonPhase.Active && Pattern == DragonPattern.Breath && Enraged && breath != null)
            {
                direction = Quaternion.AngleAxis(20 * Time.deltaTime, Vector3.up) * direction;
                transform.rotation = Quaternion.LookRotation(direction);
                breath.transform.rotation = transform.rotation;
            }
            if (Time.time < until) return;
            if (Phase == DragonPhase.Windup) Release();
            else if (Phase == DragonPhase.Active) Recover();
            else if (AutomaticPatterns) BeginNext();
        }

        public void BeginPattern(DragonPattern pattern)
        {
            // 낙하는 현재 공격을 끊지 않는 별도 환경 공격입니다.
            if (pattern == DragonPattern.Rockfall) { DropRocks(); return; }
            if ((pattern == DragonPattern.LavaEruption && GetComponent<BossArmor>().BreakCount == 0) || pattern == DragonPattern.Basic || EnrageTransitioning || health == null || health.IsDead || !Introduced || !PlayerInside || arena.IsLocked ||
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
            float windup = pattern == DragonPattern.Circle ? Tuning.SlamWindup :
                pattern == DragonPattern.BodySweep ? Tuning.BodySweepWindup : pattern == DragonPattern.RadialFireballs ? 1.8f : pattern == DragonPattern.Charge ? Tuning.ChargeWindup :
                pattern == DragonPattern.Rain ? 0.7f :
                pattern == DragonPattern.LavaEruption ? Tuning.LavaEruptionWarning : pattern == DragonPattern.OrbitFlames ? 0.75f : pattern == DragonPattern.Breath ? 0.55f : 0.9f;
            windup /= Tempo;
            if (pattern == DragonPattern.Breath) windup += BossBreathBackstep.Seconds;
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
                slam.WarningSeconds = windup + LeapDuration - BossParrySignal.ReactionSeconds;
                slam.WarningColor = new Color(1, 0.78f, 0.08f, 0.3f);
                slam.EmitsFlame = false;
                parrySignal.Arm(Time.time + windup + LeapDuration, Tuning.SlamRadius + 0.5f);
            }
            if (pattern == DragonPattern.BodySweep) PrepareBodySweep(windup);
            if (pattern == DragonPattern.RadialFireballs)
            {
                PrepareRadialWarning(windup);
                volleysSent = 0;
            }
            if (pattern == DragonPattern.Breath) backstep.Begin(-direction, Tuning.BreathBackstep);
            if (pattern == DragonPattern.OrbitFlames) PrepareOrbit(windup);
            if (pattern == DragonPattern.LavaEruption) PrepareLavaEruption(windup);
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

        private void Release()
        {
            status.text = "";
            Phase = DragonPhase.Active;
            until = Time.time + (Pattern == DragonPattern.Breath ? 2.4f : Pattern == DragonPattern.Rain ? RainDuration :
                Pattern == DragonPattern.Circle ? LeapDuration + 0.12f : Pattern == DragonPattern.RadialFireballs ? Tuning.RadialRange / (Tuning.RadialSpeed * Tempo) + 0.3f : 0.18f);
            if (Pattern == DragonPattern.OrbitFlames) until = Time.time + BossRingSequence.AfterFirstWarning;
            if (Pattern == DragonPattern.LavaEruption) until = Time.time + 1.1f;
            if (Pattern == DragonPattern.Rain)
                until = Time.time + (Tuning.RainWaves - 1) * Tuning.RainInterval + Tuning.RainFallSeconds + 0.15f;
            if (Pattern == DragonPattern.Breath) until = Time.time + 2.4f / Tempo;
            phaseStarted = Time.time;
            phaseDuration = until - Time.time;
            if (Pattern == DragonPattern.RadialFireballs)
            {
                FireRadialVolley();
                until = Time.time + Tuning.RadialRange / (Tuning.RadialSpeed * Tempo) + (Enraged ? 1 : 0) + 0.3f;
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
            BossHazard hazard = BossHazard.Create(effects, transform, (Pattern == DragonPattern.Breath || Pattern == DragonPattern.OrbitFlames) ? arena.GroundPoint(point) : ClampPoint(point), Quaternion.LookRotation(direction), target, radius, angle, delay, duration, ScaleDamage(damage), interval);
            hazard.GroundHeight = arena.GroundHeight;
            hazard.EmitsFlame = damage > 0;
            if (Pattern == DragonPattern.Breath)
            {
                hazard.BreathOrigin = Mouth;
                hazard.BreathTravelSeconds = Tuning.BreathTravelSeconds / Tempo;
                hazard.InnerRadius = Tuning.BreathSafeRadius;
                hazard.VisualRangeMultiplier = 1;
                hazard.HideWarningWhileActive = true;
            }
            return hazard;
        }

        private Vector3 ClampPoint(Vector3 point)
        {
            Quaternion rotation = Quaternion.LookRotation(arena.Forward);
            Vector3 local = Quaternion.Inverse(rotation) * (point - arena.Center);
            Vector3 radial = Vector3.ClampMagnitude(new Vector3(local.x, 0, local.z), BossArena.HalfSize - 3);
            local.x = radial.x;
            local.z = radial.z;
            return arena.GroundPoint(arena.Center + rotation * new Vector3(local.x, 0, local.z));
        }

        private bool HitRock()
        {
            float radius = GetComponent<CapsuleCollider>().radius;
            foreach (RaycastHit hit in Physics.SphereCastAll(transform.position + Vector3.up * radius, radius, direction,
                DashSpeed * Time.deltaTime + 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BossBreakableRock>() is BossBreakableRock rock && rock.Shatter())
                {
                    RockStuns++;
                    GetComponent<BossArmor>().DamageArmor(BossArmor.DurabilityPerLayer * 0.65f);
                    if (Phase != DragonPhase.Stunned) Stun(1.9f);
                    return true;
                }
            return false;
        }

        public void Stun(float seconds)
        {
            if (health == null || health.IsDead || !Introduced || EnrageTransitioning) return;
            CancelEffects();
            parrySignal.Clear();
            tactics.Stop();
            Pause();
            Phase = DragonPhase.Stunned;
            if (Enraged) seconds *= 0.6f;
            until = Time.time + seconds;
            phaseStarted = Time.time;
            phaseDuration = seconds;
            status.text = "";
            status.color = new Color(1, 0.9f, 0.3f);
            SetMotion("BossKnockdown", 1);
        }

        private void ArmorBroken(int count) => Stun(3.2f);

        private Vector3[] firePoolTargets = new Vector3[4];
        private void PlanFirePools(float windup)
        {
            firePoolTargets = new Vector3[Tuning.FirePoolCount * (Enraged ? 2 : 1)];
            for (int index = 0; index < firePoolTargets.Length; index++)
            {
                Vector3 offset = Quaternion.Euler(0, index * 360f / firePoolTargets.Length + 45, 0) * direction * (index % 2 == 0 ? 6 : 11);
                firePoolTargets[index] = ClampPoint(transform.position + offset);
                Area(firePoolTargets[index], Tuning.FirePoolRadius, 360, windup + 0.75f, 6, 10);
            }
        }

        public int ScaleDamage(int amount) => Mathf.RoundToInt(amount * (Enraged ? 1.4f : 1));

        public void HitRadialWave(int damage, int volley = 0)
        {
            if (protectedVolleys.Contains(volley) || !damagedVolleys.Add(volley)) return;
            pendingVolleyHits[volley] = damage;
        }

        private void FireRadialVolley()
        {
            volleysSent++;
            VolleysReleased++;
            int volley = ++volleySerial;
            nextVolley = Time.time + 1;
            if (Enraged && volleysSent == 1) PrepareRadialWarning(1, 1);
            for (int index = 0; index < Tuning.RadialCount; index++)
            {
                Vector3 heading = Quaternion.Euler(0, index * 360f / Tuning.RadialCount + (volleysSent - 1) * 180f / Tuning.RadialCount, 0) * transform.forward;
                Vector3 start = arena.GroundPoint(transform.position + heading * 3) + Vector3.up * 1.2f;
                Vector3 end = arena.GroundPoint(transform.position + heading * Tuning.RadialRange) + Vector3.up * 1.2f;
                BossFireball ball = BossFireball.Launch(effects, transform, target, start, end,
                    (Tuning.RadialRange - 3) / (Tuning.RadialSpeed * Tempo), false, damage: ScaleDamage(25), radius: Tuning.RadialRadius);
                ball.SignalControlled = true;
                ball.VolleyId = volley;
            }
        }

        public void OrbParried(int volley = 0)
        {
            ProtectVolley(volley);
            ParrySuccesses++;
        }

        public void ResolveParry()
        {
            if (Pattern != DragonPattern.Circle && Pattern != DragonPattern.BodySweep) return;
            ParrySuccesses++;
            if (Pattern == DragonPattern.BodySweep)
            {
                if (sweep != null) sweep.CancelDamage();
                GetComponent<BossArmor>().ApplyParryBreak();
                return;
            }
            if (slam != null) slam.CancelDamage();
            pendingSlamCounter = true;
        }

        public void FinishParriedSlam()
        {
            if (!pendingSlamCounter) return;
            pendingSlamCounter = false;
            GetComponent<BossArmor>().ApplyParryBreak();
        }

        private void Recover()
        {
            Phase = DragonPhase.Recovery;
            until = Time.time + Tuning.RecoverySeconds;
            status.text = "";
            SetMotion(null, 1);
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
            ClearBackground();
            tactics?.Stop();
            if (status != null) status.text = "";
            SetMotion(null, 1);
        }

        private void LateUpdate()
        {
            FlushVolleyHits();
            if (status != null && Camera.main != null) status.transform.rotation = Camera.main.transform.rotation;
        }
        private void OnDisable() { StopEncounter(); }
        private void OnDestroy()
        {
            if (TryGetComponent(out BossArmor armor)) armor.Broken -= ArmorBroken;
            if (effects != null) Destroy(effects.gameObject);
            if (background != null) Destroy(background.gameObject);
        }
    }
}
