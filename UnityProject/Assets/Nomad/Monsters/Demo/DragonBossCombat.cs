using Nomad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Nomad.Monsters.Demo
{
    public enum DragonPattern { Basic, Circle, TripleFireball, Breath, FirePool, Charge, Rain }
    public enum DragonPhase { Waiting, Windup, Active, Recovery, Stunned, Dead }

    [DefaultExecutionOrder(120)]
    public sealed partial class DragonBossCombat : MonoBehaviour
    {
        public const int RainWaves = 24;
        public const int RainPerWave = 3;
        public const float RainInterval = 0.3f;
        public const float RainDuration = 8.3f;
        public bool AutomaticPatterns { get; set; } = true;
        public int AttacksStarted { get; private set; }
        public int RainProjectilesSpawned { get; private set; }
        public DragonPattern Pattern { get; private set; }
        public DragonPhase Phase { get; private set; }
        public float Remaining => Mathf.Max(0, until - Time.time);
        public int ThresholdStuns { get; private set; }
        public int RockStuns { get; private set; }
        public Transform Effects => effects;
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
        private bool crossed75, crossed50;
        private TextMesh status;
        private static readonly DragonPattern[] Rotation = { DragonPattern.Circle, DragonPattern.TripleFireball,
            DragonPattern.Basic, DragonPattern.Breath, DragonPattern.FirePool, DragonPattern.Charge, DragonPattern.Rain };

        public void Configure(BossArena room, Health victim)
        {
            arena = room;
            target = victim;
            health = GetComponent<Health>();
            tracker = GetComponent<MonsterTracker>();
            agent = GetComponent<NavMeshAgent>();
            tactics = GetComponent<MonsterTactics>();
            animationDriver = GetComponent<MonsterAnimationDriver>();
            GetComponent<MonsterMeleeAttack>().enabled = false;
            tactics.Configure(target, 0, 14, 15, 6, 6.5f, 8, 0.8f, 0.1f, 28);
            agent.speed = 6;
            agent.acceleration = 40;
            agent.angularSpeed = 720;
            agent.stoppingDistance = 3.2f;
            ConfigurePresentation();
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
        }

        private bool PlayerInside => target != null && target.gameObject.activeInHierarchy && !target.IsDead &&
            Mathf.Abs((target.transform.position - arena.Center).y) < 4 &&
            Mathf.Max(Mathf.Abs(Vector3.Dot(target.transform.position - arena.Center, arena.Forward)),
                Mathf.Abs(Vector3.Dot(target.transform.position - arena.Center, Vector3.Cross(Vector3.up, arena.Forward)))) < BossArena.HalfSize;

        private void Update()
        {
            if (health == null) return;
            if (health.IsDead) { Phase = DragonPhase.Dead; StopEncounter(); return; }
            if (animationDriver != null && (Phase == DragonPhase.Windup || Phase == DragonPhase.Active))
            {
                float progress = Pattern == DragonPattern.Rain && Phase == DragonPhase.Active
                    ? Mathf.PingPong((Time.time - phaseStarted) * 1.8f, 1) : Mathf.Clamp01((Time.time - phaseStarted) / phaseDuration);
                animationDriver.ActionNormalizedTime = Mathf.Lerp(Phase == DragonPhase.Windup ? 0.03f : 0.3f,
                    Phase == DragonPhase.Windup ? 0.28f : 0.9f, progress);
            }
            if (arena.IsLocked || !PlayerInside)
            {
                if (Phase != DragonPhase.Waiting) StopEncounter();
                Phase = DragonPhase.Waiting;
                Pause();
                until = Time.time;
                return;
            }
            if (CheckThresholds()) return;
            if (Phase == DragonPhase.Stunned)
            {
                if (Time.time < until) return;
                Recover();
                return;
            }
            if (Phase == DragonPhase.Active && Pattern == DragonPattern.Charge)
            {
                if (HitRock()) return;
                if (tactics.TickCharge()) Recover();
                return;
            }
            if (Phase == DragonPhase.Active && Pattern == DragonPattern.Rain && Time.time >= nextRain && rainCount < RainWaves)
            {
                nextRain = Time.time + RainInterval;
                for (int index = 0; index < RainPerWave; index++)
                {
                    float distance = index == 0 ? 0 : index == 1 ? 2.6f : 4.2f;
                    Vector3 point = ClampPoint(target.transform.position + Quaternion.Euler(0, rainCount * 137.5f + index * 120, 0) * Vector3.forward * distance);
                    Area(point, 1.15f, 360, 1.1f, 0.12f, 20);
                    BossFireball.Launch(effects, transform, target, point + Vector3.up * 8, point, 1.1f, false, damage: 0);
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
            if (health == null || health.IsDead || !PlayerInside || arena.IsLocked ||
                (Phase == DragonPhase.Stunned && Time.time < until)) return;
            tactics.Stop();
            Pause();
            Pattern = pattern;
            AttacksStarted++;
            Phase = DragonPhase.Windup;
            aim = ClampPoint(target.transform.position);
            direction = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            transform.rotation = Quaternion.LookRotation(direction);
            float windup = pattern == DragonPattern.Circle ? 1.6f : pattern == DragonPattern.Basic ? 0.45f :
                pattern == DragonPattern.TripleFireball ? 0.55f : pattern == DragonPattern.Charge ? 0.8f :
                pattern == DragonPattern.Rain ? 0.7f : 0.9f;
            until = Time.time + windup;
            phaseStarted = Time.time;
            phaseDuration = windup;
            SetMotion("Attack_A", 1f / (windup + 0.5f));
            status.text = pattern == DragonPattern.TripleFireball ? "" : pattern == DragonPattern.Charge ? "돌진 준비" :
                pattern == DragonPattern.Breath ? "브레스 준비" : pattern == DragonPattern.Rain ? "화염우" : "";
            if (pattern == DragonPattern.Circle) Area(transform.position, 6.5f, 360, windup, 0.12f, 32);
            if (pattern == DragonPattern.Basic) Area(transform.position, 4, 110, windup, 0.12f, 18);
            if (pattern == DragonPattern.Breath) Area(transform.position, 9, 70, windup, 1.5f, 12, 0.5f);
            if (pattern == DragonPattern.FirePool)
            {
                Area(aim, 2.4f, 360, windup + 0.75f, 5, 10);
            }
            if (pattern == DragonPattern.Charge)
            {
                // 원뿔 윤곽은 실제 돌진 방향을 고정해서 보여줍니다.
                Area(transform.position, 12, 14, windup, 0.01f, 0);
                tactics.Prepare(MonsterSkill.Charge, transform.position + direction * 12);
            }
        }

        private void BeginNext()
        {
            DragonPattern next = Rotation[sequence % Rotation.Length];
            float engageRange = next == DragonPattern.Basic ? 3.8f : next == DragonPattern.Circle ? 5.5f : 9;
            if (Vector3.Distance(transform.position, target.transform.position) > engageRange)
            {
                tracker.enabled = true;
                tracker.ReactToDamage();
                if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
                until = Time.time + 0.08f;
                return;
            }
            sequence++;
            BeginPattern(next);
        }

        private void Release()
        {
            status.text = "";
            Phase = DragonPhase.Active;
            until = Time.time + (Pattern == DragonPattern.Breath ? 1.5f : Pattern == DragonPattern.Rain ? RainDuration : 0.18f);
            phaseStarted = Time.time;
            phaseDuration = until - Time.time;
            if (Pattern == DragonPattern.TripleFireball)
                foreach (float yaw in new[] { -22f, 0, 22f })
                {
                    Vector3 start = Mouth.position;
                    Vector3 end = start + Quaternion.Euler(0, yaw, 0) * direction * 24;
                    BossFireball.Launch(effects, transform, target, start, end, 3, false);
                }
            if (Pattern == DragonPattern.FirePool)
                BossFireball.Launch(effects, transform, target, Mouth.position, aim, 0.75f, true);
            if (Pattern == DragonPattern.Charge && !tactics.StartCharge(direction)) Recover();
            if (Pattern == DragonPattern.Rain) { rainCount = 0; RainProjectilesSpawned = 0; nextRain = Time.time; }
        }

        private BossHazard Area(Vector3 point, float radius, float angle, float delay, float duration, int damage, float interval = 0.75f)
        {
            BossHazard hazard = BossHazard.Create(effects, transform, ClampPoint(point), Quaternion.LookRotation(direction), target, radius, angle, delay, duration, damage, interval);
            hazard.EmitsFlame = Pattern != DragonPattern.Basic && damage > 0;
            if (Pattern == DragonPattern.Breath) hazard.BreathOrigin = Mouth;
            return hazard;
        }

        private Vector3 ClampPoint(Vector3 point)
        {
            Quaternion rotation = Quaternion.LookRotation(arena.Forward);
            Vector3 local = Quaternion.Inverse(rotation) * (point - arena.Center);
            local.x = Mathf.Clamp(local.x, -9.5f, 9.5f);
            local.z = Mathf.Clamp(local.z, -9.5f, 9.5f);
            return arena.Center + rotation * new Vector3(local.x, 0, local.z);
        }

        private bool CheckThresholds()
        {
            float ratio = health.CurrentHealth / (float)health.MaxHealth;
            bool stun = false;
            if (!crossed75 && ratio <= 0.75f) { crossed75 = true; ThresholdStuns++; stun = true; }
            if (!crossed50 && ratio <= 0.5f) { crossed50 = true; ThresholdStuns++; stun = true; }
            if (stun) Stun(3);
            return stun;
        }

        private bool HitRock()
        {
            float radius = GetComponent<CapsuleCollider>().radius;
            foreach (RaycastHit hit in Physics.SphereCastAll(transform.position + Vector3.up * radius, radius, direction,
                tactics.ChargeSpeed * Time.deltaTime + 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BossBreakableRock>() is BossBreakableRock rock && rock.Shatter())
                { RockStuns++; Stun(2.5f); return true; }
            return false;
        }

        public void Stun(float seconds)
        {
            if (health == null || health.IsDead) return;
            CancelEffects();
            tactics.Stop();
            Pause();
            Phase = DragonPhase.Stunned;
            until = Time.time + seconds;
            status.text = "";
            status.color = new Color(1, 0.9f, 0.3f);
            SetMotion("Hit_A", 0.35f);
        }

        private void Recover()
        {
            Phase = DragonPhase.Recovery;
            until = Time.time;
            status.text = "";
            SetMotion(null, 1);
            if (AutomaticPatterns) BeginNext();
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
            CancelEffects();
            tactics?.Stop();
            if (status != null) status.text = "";
            SetMotion(null, 1);
        }

        private void LateUpdate() { if (status != null && Camera.main != null) status.transform.rotation = Camera.main.transform.rotation; }
        private void OnDisable() { StopEncounter(); }
        private void OnDestroy() { if (effects != null) Destroy(effects.gameObject); }
    }
}
