using UnityEngine;

namespace Nomad.Monsters.Demo
{
    public sealed partial class DragonBossCombat
    {
        public Transform Mouth { get; private set; }
        private void ConfigurePresentation()
        {
            Transform[] bones = GetComponentsInChildren<Transform>();
            Transform upper = System.Array.Find(bones, bone => bone.name == "jaw_upper");
            Transform lower = System.Array.Find(bones, bone => bone.name == "jaw_lower");
            Mouth = new GameObject("Boss Mouth").transform;
            Mouth.SetParent(upper != null ? upper : transform, false);
            Mouth.position = upper != null ? (upper.position + (lower != null ? lower.position : upper.position)) * 0.5f + transform.forward * 0.25f :
                transform.position + Vector3.up * 1.8f + transform.forward * 1.5f;
            gameObject.AddComponent<BossStunDisplay>().Configure(this, Mouth);
        }
    }
}
