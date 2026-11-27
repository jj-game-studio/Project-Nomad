using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class BossHazard
    {
        public bool Irregular => angle >= 359 && duration > 1 && damage > 0;
        private float ShapeSeed => Mathf.Repeat(transform.position.x * 0.73f + transform.position.z * 0.41f, Mathf.PI * 2);

        public float EdgeRadius(Vector3 direction)
        {
            if (!Irregular) return Radius;
            // 외형·경고·피해가 같은 굴곡을 사용하고 매 프레임 흔들리지 않습니다.
            float radians = Mathf.Atan2(direction.x, direction.z);
            return Radius * (0.82f + 0.1f * Mathf.Sin(radians * 3 + ShapeSeed) + 0.06f * Mathf.Cos(radians * 5 - ShapeSeed * 0.7f));
        }

        private void EmitLava()
        {
            for (int index = 0; index < Mathf.CeilToInt(Radius * 14 * Time.deltaTime); index++)
            {
                Vector3 heading = Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward;
                Vector3 position = transform.position + heading * (Mathf.Sqrt(Random.value) * EdgeRadius(heading) * 0.94f);
                position.y = (GroundHeight != null ? GroundHeight(position) : transform.position.y) + 0.3f;
                fire.Emit(new ParticleSystem.EmitParams { position = position, velocity = Vector3.up * Random.Range(0.4f, 1.2f) }, 1);
            }
        }
    }
}
