using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Game;
using Nomad.Equipment;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nomad.Player
{
    [DefaultExecutionOrder(-30)]
    [DisallowMultipleComponent]
    public sealed class VillageFirstPersonCamera : MonoBehaviour
    {
        private const float EyeHeight = 1.65f;
        private const float Sensitivity = 0.12f;
        private readonly Dictionary<Renderer, bool> hidden = new();
        private readonly List<Renderer> renderers = new();
        private TopDownCamera follow;
        private Transform target;
        private PlayerMovement movement;
        private PlayerInventory inventory;
        private Health health;
        private Camera view;
        private GameObject crosshair;
        private float yaw;
        private float pitch;
        private float previousFov;
        private float previousNear;
        private bool previousFollow;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private bool focused;
        private bool captured;
        private bool suppressMouseFrame;
        private int releasedFrame = -1;

        public bool IsActive { get; private set; }
        public bool InputBlocked { get; private set; }

        public void Initialize(TopDownCamera topDown, Transform player)
        {
            follow = topDown;
            target = player;
            view = GetComponent<Camera>();
            movement = player.GetComponent<PlayerMovement>();
            movement.Disabled += OnDisable;
            inventory = player.GetComponent<PlayerInventory>();
            health = player.GetComponent<Health>();
            focused = Application.isFocused;
            gameObject.AddComponent<Nomad.PlayerVisual.FirstPersonWeaponView>().Initialize(this, player);
            gameObject.AddComponent<FirstPersonLandmarks>().Initialize(this);

            crosshair = new GameObject("Village Crosshair", typeof(RectTransform), typeof(Canvas));
            crosshair.transform.SetParent(transform, false);
            Canvas canvas = crosshair.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var dot = (RectTransform)new GameObject("Dot", typeof(RectTransform)).transform;
            dot.SetParent(crosshair.transform, false);
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(4f, 4f);

            var image = dot.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 0.96f, 0.84f, 0.9f);
            image.raycastTarget = false;
            crosshair.SetActive(false);
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            bool wanted = target != null && target.gameObject.activeInHierarchy && game != null &&
                game.Player == target.gameObject && game.CurrentRegion.name == "Village" &&
                movement.isActiveAndEnabled && !game.IsRestarting && VillageViewSettings.FirstPerson;
            if (wanted != IsActive) SetActive(wanted);
            if (!IsActive) return;

            bool blocked = !focused || Time.timeScale <= 0f || game.IsLoading || game.IsChoosingField ||
                (health != null && health.IsDead) || inventory?.BlocksTradeInput == true ||
                inventory?.Panel?.IsOpen == true || target.GetComponent<EquipmentPanel>()?.IsOpen == true;
            if (blocked) releasedFrame = Time.frameCount;
            InputBlocked = blocked || Time.frameCount <= releasedFrame + 1;
            movement.ViewInputBlocked = InputBlocked;
            crosshair.SetActive(!InputBlocked);
            SetCapture(!InputBlocked);

            // 커서 잠금/복귀 프레임의 큰 delta는 시선에 적용하지 않습니다.
            if (captured && !suppressMouseFrame && Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                yaw += delta.x * Sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * Sensitivity, -75f, 75f);
            }
            suppressMouseFrame = false;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void LateUpdate()
        {
            if (!IsActive || target == null) return;
            // 머리 본 대신 몸의 위치만 따라 점프·구르기 애니메이션 흔들림을 배제합니다.
            transform.SetPositionAndRotation(target.position + Vector3.up * EyeHeight, Quaternion.Euler(pitch, yaw, 0f));
            target.GetComponentsInChildren(true, renderers);
            foreach (Renderer renderer in renderers)
            {
                if (!hidden.ContainsKey(renderer)) hidden.Add(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
        }

        private void SetActive(bool active)
        {
            IsActive = active;
            if (active)
            {
                previousFov = view.fieldOfView;
                previousNear = view.nearClipPlane;
                previousFollow = follow.enabled;
                previousCursorVisible = Cursor.visible;
                previousCursorLock = Cursor.lockState;

                yaw = target.eulerAngles.y;
                pitch = 0f;
                follow.enabled = false;
                view.fieldOfView = 70f;
                view.nearClipPlane = 0.05f;
                movement.FirstPersonView = true;
                suppressMouseFrame = true;
                return;
            }

            crosshair.SetActive(false);
            movement.FirstPersonView = false;
            movement.ViewInputBlocked = false;
            InputBlocked = false;
            foreach (var pair in hidden)
                if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            hidden.Clear();
            renderers.Clear();
            view.fieldOfView = previousFov;
            view.nearClipPlane = previousNear;
            follow.enabled = previousFollow;
            if (previousFollow) follow.ResetTracking();
            captured = false;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private void SetCapture(bool value)
        {
            if (captured != value) suppressMouseFrame = true;
            captured = value;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            suppressMouseFrame = true;
            if (!value && IsActive) SetCapture(false);
        }

        // 지역 이동으로 Player가 꺼지는 즉시 복원해 필드 모듈이 정상 탑다운 상태를 보관하게 합니다.
        private void OnDisable()
        {
            if (IsActive) SetActive(false);
        }

        private void OnDestroy()
        {
            if (movement != null) movement.Disabled -= OnDisable;
            if (crosshair != null) Destroy(crosshair);
        }
    }
}
