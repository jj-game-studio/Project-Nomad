using UnityEngine;
using UnityEngine.Rendering;

namespace Nomad.Equipment
{
    // 실물 화살을 유지하고, 약공격의 짧은 꼬리와 차징 화살의 긴 광선·나선 궤적을 구분합니다.
    public sealed class ArrowShotVfx : MonoBehaviour
    {
        private Transform target;
        private TrailRenderer tail;
        private TrailRenderer spiral;
        private LineRenderer shaft;
        private float releasedAt = -1;
        public bool Charged { get; private set; }

        public static ArrowShotVfx Attach(Transform arrow, WeaponVfxStyle style, float power)
        {
            var effect = new GameObject("Arrow Shot VFX").AddComponent<ArrowShotVfx>();
            effect.target = arrow;
            effect.Charged = power > 1.1f;
            effect.transform.SetPositionAndRotation(arrow.position, arrow.rotation);
            effect.tail = effect.Trail("Arrow Wake", style.Core, effect.Charged ? 0.22f : 0.045f, effect.Charged ? 0.35f : 0.09f);

            if (effect.Charged)
            {
                effect.spiral = effect.Trail("Golden Spiral", style.Glow, 0.09f, 0.3f);
                effect.shaft = new GameObject("Energy Arrowhead").AddComponent<LineRenderer>();
                effect.shaft.transform.SetParent(effect.transform, false);
                effect.shaft.useWorldSpace = false;
                effect.shaft.sharedMaterial = WeaponVfx.Beam;
                effect.shaft.startColor = effect.shaft.endColor = style.Core;
                effect.shaft.widthMultiplier = 0.09f * style.Size;

                effect.shaft.positionCount = 5;
                effect.shaft.SetPositions(new[] { new Vector3(0, 0, -0.6f), new Vector3(0, 0, 0.45f),
                    new Vector3(-0.2f, 0, 0.15f), new Vector3(0, 0, 0.45f), new Vector3(0.2f, 0, 0.15f) });
                effect.shaft.shadowCastingMode = ShadowCastingMode.Off;
            }
            return effect;
        }

        private TrailRenderer Trail(string label, Color color, float width, float duration)
        {
            var trail = new GameObject(label).AddComponent<TrailRenderer>();
            trail.transform.SetParent(transform, false);
            trail.sharedMaterial = WeaponVfx.Beam;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0);
            trail.startWidth = width;
            trail.endWidth = 0;
            trail.time = duration;

            trail.minVertexDistance = 0.03f;
            trail.numCapVertices = 4;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            return trail;
        }

        private void LateUpdate()
        {
            if (target != null)
            {
                transform.SetPositionAndRotation(target.position, target.rotation);
                if (spiral != null)
                    spiral.transform.localPosition = new Vector3(Mathf.Cos(Time.time * 35), Mathf.Sin(Time.time * 35), 0) * 0.16f;
                return;
            }
            if (releasedAt < 0)
            {
                releasedAt = Time.time;
                tail.emitting = false;
                if (spiral != null) spiral.emitting = false;
                if (shaft != null) shaft.enabled = false;
            }
            if (Time.time - releasedAt > 0.4f) Destroy(gameObject);
        }
    }
}
