using UnityEngine;

namespace Nomad.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        private static readonly Vector3 Offset = new Vector3(0f, 12f, -8f);

        private void Awake()
        {
            Camera viewCamera = GetComponent<Camera>();
            viewCamera.orthographic = false;
            viewCamera.fieldOfView = 50f;
            LateUpdate();
        }

        private void LateUpdate()
        {
            // 플레이어가 방향을 바꿔도 카메라의 각도는 유지합니다.
            transform.SetPositionAndRotation(target.position + Offset, Quaternion.LookRotation(-Offset));
        }
    }
}
