using Nomad.Combat;
using Nomad.Monsters.Demo;
using Nomad.Terrain;
using UnityEngine;

namespace Nomad.Gameplay
{
    [DefaultExecutionOrder(80)]
    public sealed class MonsterMapDebug : MonoBehaviour
    {
        private TerrainIntegrationControls controls;
        private MonsterTestPanel panel;
        public bool Ready => controls.Navigation.State == FieldGenerationState.Ready;

        private void Awake()
        {
            controls = GetComponent<TerrainIntegrationControls>();
            panel = GetComponent<MonsterTestPanel>();
            controls.Navigation.GenerationStarted += Clear;
        }

        private void Start() => panel.SetInvincible(true);

        private void Clear(string id)
        {
            panel.ClearMonsters();
            foreach (Projectile projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                if (projectile.gameObject.scene == gameObject.scene) Destroy(projectile.gameObject);
        }

        private void OnDestroy()
        {
            if (controls != null) controls.Navigation.GenerationStarted -= Clear;
        }
    }
}
