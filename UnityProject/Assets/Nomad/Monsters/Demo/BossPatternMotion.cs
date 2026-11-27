using System.Collections.Generic;
using UnityEngine;

namespace Nomad.Monsters.Demo
{
    [DefaultExecutionOrder(370)]
    public sealed class BossPatternMotion : MonoBehaviour
    {
        private DragonBossCombat boss;
        private Transform[] bones;
        private readonly Dictionary<Transform, Quaternion> originals = new();
        private readonly Dictionary<Transform, Quaternion> applied = new();
        private float weight;

        public void Configure(DragonBossCombat owner)
        {
            boss = owner;
            bones = System.Array.FindAll(GetComponentsInChildren<Transform>(), bone => bone.name.Contains("wing_upper") || bone.name == "tail1" || bone.name == "jaw_lower");
        }

        private void Update() => Restore();

        private void Restore()
        {
            foreach (var pair in applied)
                if (pair.Key != null && Quaternion.Angle(pair.Key.localRotation, pair.Value) < 0.05f)
                    pair.Key.localRotation = originals[pair.Key];
            originals.Clear();
            applied.Clear();
        }

        private void LateUpdate()
        {
            if (boss == null) return;
            bool attacking = boss.Phase == DragonPhase.Windup || boss.Phase == DragonPhase.Active;
            if (!attacking || !boss.Introduced) { weight = 0; return; }
            weight = Mathf.MoveTowards(weight, 1, Time.deltaTime * 4);
            bool release = boss.Phase == DragonPhase.Active;
            foreach (Transform bone in bones)
            {
                originals[bone] = bone.localRotation;
                if (bone.name.Contains("wing_upper"))
                {
                    float side = bone.name.EndsWith(".L") ? -1 : 1;
                    float spread = boss.Pattern == DragonPattern.Charge ? -18 : boss.Pattern == DragonPattern.Circle ? (release ? -12 : 24) : 12;
                    float beat = release ? Mathf.Sin(Time.time * (boss.Pattern == DragonPattern.Rain ? 8 : 5)) * 4 : 0;
                    bone.rotation = Quaternion.AngleAxis((spread + beat) * side * weight, transform.forward) * bone.rotation;
                }
                else if (bone.name == "jaw_lower")
                {
                    float open = boss.Pattern == DragonPattern.Breath ? (release ? 18 : 8) : release ? 10 : 4;
                    bone.rotation = Quaternion.AngleAxis(open * weight, transform.right) * bone.rotation;
                }
                else bone.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * 3.2f) * 6 * weight, transform.up) * bone.rotation;
                applied[bone] = bone.localRotation;
            }
        }

        private void OnDisable() { Restore(); weight = 0; }
    }
}
