using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Monsters
{
    // 렌더 직전에만 작은 충격을 더하고 즉시 복원해 추적 카메라의 기준 위치를 바꾸지 않습니다.
    public sealed class HitCameraImpulse : MonoBehaviour
    {
        private Camera view;
        private float until;
        private Vector3 original;
        private bool applied;

        public static void Pulse()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            if (!camera.TryGetComponent(out HitCameraImpulse impulse)) impulse = camera.gameObject.AddComponent<HitCameraImpulse>();
            impulse.until = Time.unscaledTime + 0.09f;
        }

        private void OnEnable()
        {
            view = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        private void Begin(ScriptableRenderContext context, Camera camera)
        {
            if (camera != view || Time.timeScale <= 0 || Time.unscaledTime >= until) return;
            original = transform.position;
            float strength = 0.045f * Mathf.Clamp01((until - Time.unscaledTime) / 0.09f);
            transform.position += (transform.right * Mathf.Sin(Time.unscaledTime * 160) +
                transform.up * Mathf.Cos(Time.unscaledTime * 190) * 0.5f) * strength;
            applied = true;
        }

        private void End(ScriptableRenderContext context, Camera camera) { if (camera == view) Restore(); }
        private void Restore() { if (applied) transform.position = original; applied = false; }

        private void OnDisable()
        {
            Restore();
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
        }
    }
}
