using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nomad.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(50)]
    public sealed class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float maxLookAhead = 2f;
        [SerializeField, Min(0f)] private float smoothTime = 0.15f;
        [SerializeField, Range(0f, 0.95f)] private float deadZone = 0.1f;

        private static readonly Vector3 Offset = new Vector3(0f, 12f, -8f);
        private static readonly Quaternion Rotation = Quaternion.LookRotation(-Offset);
        private Camera viewCamera;
        private Transform previousTarget;
        private Vector3 previousPosition;
        private Vector3 lookAhead;
        private bool pointerFocused;
        private bool tracking;

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            viewCamera.orthographic = false;
            viewCamera.fieldOfView = 50f;
            pointerFocused = Application.isFocused;
            ResetTracking();
        }

        private void OnEnable() => ResetTracking();

        private void OnApplicationFocus(bool hasFocus) => pointerFocused = hasFocus;

        // 작은 순간이동을 수행하는 호출자도 이전 커서 이동량을 명시적으로 비울 수 있습니다.
        public void ResetTracking()
        {
            lookAhead = Vector3.zero;
            tracking = false;
            if (target != null)
                transform.SetPositionAndRotation(target.position + Offset, Rotation);
        }

        private void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                ResetTracking();
                return;
            }

            Vector3 position = target.position;
            // 정상 구르기(10m/s)보다 큰 위치 불연속은 생성·순간이동으로 취급합니다.
            float continuousDistance = Mathf.Max(0.5f, Time.deltaTime * 12f);
            bool reset = !tracking || previousTarget != target ||
                (position - previousPosition).sqrMagnitude > continuousDistance * continuousDistance;
            Vector3 desired = reset ? Vector3.zero : DesiredLookAhead();
            float blend = smoothTime <= 0f ? 1f : 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime / smoothTime);
            lookAhead = reset || maxLookAhead <= 0f ? Vector3.zero :
                Vector3.Lerp(lookAhead, desired, blend);
            lookAhead = Vector3.ClampMagnitude(lookAhead, Mathf.Max(0f, maxLookAhead));

            // 플레이어 추적은 지연시키지 않고 커서에 따른 추가 이동만 보간합니다.
            transform.SetPositionAndRotation(position + Offset + lookAhead, Rotation);
            previousTarget = target;
            previousPosition = position;
            tracking = true;
        }

        private Vector3 DesiredLookAhead()
        {
            Mouse mouse = Mouse.current;
            if (!pointerFocused || mouse == null || Time.timeScale <= 0f || HasInteractiveWindow())
                return Vector3.zero;

            Rect rect = viewCamera.pixelRect;
            Vector2 pointer = mouse.position.ReadValue();
            if (rect.width <= 0f || rect.height <= 0f)
                return Vector3.zero;

            // 이동 후의 플레이어 화면 좌표가 아닌 고정된 화면 중앙을 기준으로 합니다.
            Vector2 direction = (pointer - rect.center) / (rect.size * 0.5f);
            // 화면 밖에서도 가장자리 방향을 유지해 경계에서 중앙으로 돌아오지 않습니다.
            direction.x = Mathf.Clamp(direction.x, -1f, 1f);
            direction.y = Mathf.Clamp(direction.y, -1f, 1f);
            float radius = direction.magnitude;
            float zone = Mathf.Clamp(deadZone, 0f, 0.95f);
            if (radius <= zone)
                return Vector3.zero;

            float distance = Mathf.Clamp01((radius - zone) / (1f - zone)) * Mathf.Max(0f, maxLookAhead);
            Vector3 forward = Vector3.ProjectOnPlane(Rotation * Vector3.forward, Vector3.up).normalized;
            return ((Rotation * Vector3.right) * direction.x + forward * direction.y).normalized * distance;
        }

        private static bool HasInteractiveWindow()
        {
            // 닫힌 창과 입력을 받지 않는 HUD는 제외하고 상점에도 같은 기준을 적용합니다.
            foreach (BaseRaycaster raycaster in RaycasterManager.GetRaycasters())
            {
                if (raycaster is not GraphicRaycaster || !raycaster.isActiveAndEnabled)
                    continue;

                Canvas canvas = raycaster.GetComponent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled)
                    continue;

                var graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas);
                for (int index = 0; index < graphics.Count; index++)
                {
                    Graphic graphic = graphics[index];
                    if (graphic == null || !graphic.isActiveAndEnabled || graphic.canvasRenderer.cull)
                        continue;

                    Vector2 center = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera,
                        graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center));
                    if (graphic.Raycast(center, raycaster.eventCamera))
                        return true;
                }
            }

            return false;
        }
    }
}
