#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Nomad.PlayerVisual.Editor
{
    // 배치 Editor 갱신 간격에 영향받지 않도록 실제 플레이 프레임에서 검사합니다.
    [DefaultExecutionOrder(-1000)]
    public sealed class PlayerSwordVerificationLoop : MonoBehaviour
    {
        public Action Input;
        public Action Tick;
        private void Update() => Input?.Invoke();
        private void LateUpdate() => Tick?.Invoke();
    }
}
#endif
