using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed class BossParrySlowMotion : MonoBehaviour
    {
        public const float Scale = 0.45f, Seconds = 0.16f;
        private static BossParrySlowMotion current;
        private float until, original, fixedStep;

        public static void Pulse()
        {
            if (Time.timeScale <= 0) return;
            if (current != null) { current.until = Time.unscaledTime + Seconds; return; }
            if (!Mathf.Approximately(Time.timeScale, 1)) return;

            var root = new GameObject("Parry brief slow motion");
            DontDestroyOnLoad(root);
            current = root.AddComponent<BossParrySlowMotion>();
            current.original = Time.timeScale; current.fixedStep = Time.fixedDeltaTime;
            current.until = Time.unscaledTime + Seconds;
            Time.timeScale = Scale; Time.fixedDeltaTime = current.fixedStep * Scale;
        }

        private void LateUpdate()
        {
            // 일시정지/필드 선택의 0배속은 건드리지 않고, 재개 시 저장된 슬로 배속을 정상으로 복원합니다.
            if (Time.unscaledTime < until || Time.timeScale <= 0) return;
            Restore();
            Destroy(gameObject);
        }

        private void Restore()
        {
            if (Mathf.Approximately(Time.timeScale, Scale)) Time.timeScale = original;
            if (Mathf.Approximately(Time.fixedDeltaTime, fixedStep * Scale)) Time.fixedDeltaTime = fixedStep;
        }

        private void OnDestroy() { Restore(); if (current == this) current = null; }
    }
}
