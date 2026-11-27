using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossHazard
    {
        private void CreateContinuousJet()
        {
            const int streams = 17;
            for (int index = 0; index < streams; index++)
            {
                float heading = Mathf.Lerp(-angle * 0.5f + 9, angle * 0.5f - 9, index / (float)(streams - 1));
                Vector3 direction = Quaternion.Euler(0, heading, 0) * Vector3.forward;
                Vector3 start = direction * (InnerRadius + 0.9f) + Vector3.up * 1.45f;
                Vector3 end = direction * (Radius - 1.4f) + Vector3.up * 0.85f;
                Transform jet = BossFlameVfx.Jet(transform, start, end, 0.35f, 1.2f);
                jet.name = "Coherent breath flame volume";
            }
        }
    }
}
