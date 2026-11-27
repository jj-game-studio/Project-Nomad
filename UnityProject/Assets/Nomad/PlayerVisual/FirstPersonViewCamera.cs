using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nomad.PlayerVisual
{
    // 월드 FOV와 독립적으로 손·무기를 렌더합니다. 월드 충돌/발사구 좌표는 그대로 사용합니다.
    internal sealed class FirstPersonViewCamera
    {
        private readonly Camera world;
        private readonly Camera view;
        private readonly UniversalAdditionalCameraData worldData;
        private readonly int mask;
        private readonly int originalLayerMask;
        public Camera Camera => view;

        public FirstPersonViewCamera(Camera worldCamera)
        {
            world = worldCamera;
            mask = 1 << LayerMask.NameToLayer("FirstPersonView");
            originalLayerMask = world.cullingMask & mask;
            view = new GameObject("First Person Render Camera", typeof(Camera)).GetComponent<Camera>();
            view.transform.SetParent(world.transform, false);
            view.cullingMask = mask;
            view.fieldOfView = 60f;
            view.nearClipPlane = 0.02f;
            view.farClipPlane = 8f;
            view.allowHDR = world.allowHDR;
            view.allowMSAA = world.allowMSAA;

            var data = view.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            // Overlay의 기본 clearDepth=true를 사용합니다(URP 17.6에서는 읽기 전용).
            data.renderShadows = false;
            data.renderPostProcessing = false;
            worldData = world.GetUniversalAdditionalCameraData();
            SetActive(false);
        }

        public void SetActive(bool active)
        {
            if (world == null || view == null) return;
            view.enabled = active;
            view.rect = world.rect;
            if (active)
            {
                world.cullingMask &= ~mask;
                if (!worldData.cameraStack.Contains(view)) worldData.cameraStack.Add(view);
            }
            else
            {
                worldData.cameraStack.Remove(view);
                world.cullingMask = (world.cullingMask & ~mask) | originalLayerMask;
            }
        }

        public static void SetLayer(GameObject root)
        {
            int layer = LayerMask.NameToLayer("FirstPersonView");
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
        }

        public void Dispose()
        {
            if (world != null) SetActive(false);
            if (view != null) Object.Destroy(view.gameObject);
        }
    }
}
