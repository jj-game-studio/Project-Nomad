using Nomad.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nomad.Hud
{
    // 미리보기 전용 입력입니다. 실제 마나/스태미나 게임 규칙을 변경하지 않습니다.
    public sealed class HudPreview : MonoBehaviour
    {
        private PlayerHud hud;
        private Health health;
        private int mana = 75;
        private int stamina = 60;

        private void Start()
        {
            hud = GetComponent<PlayerHud>();
            health = GetComponent<Health>();
            Refresh();
            Debug.Log("HUD_PREVIEW_READY: Health is live; mana, stamina and weapon use preview data.");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.hKey.wasPressedThisFrame)
                health.TryApplyDamage(25);
            if (keyboard.mKey.wasPressedThisFrame)
                mana = (mana + 25) % 125;
            if (keyboard.tKey.wasPressedThisFrame)
                stamina = (stamina + 20) % 120;
            if (keyboard.rKey.wasPressedThisFrame)
                ResetPreview();

            Refresh();
        }

        public static void ResetPreview()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Nomad/Hud/HudPreview.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#endif
        }

        private void Refresh()
        {
            hud.SetResources(mana, 100, stamina, 100);
            hud.SetWeapon("FIELD BLASTER");
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(20, 20, 520, 72), "HUD PREVIEW  /  Health live · resources & weapon preview only\nH: damage   M: mana   T: stamina   R: reset\nWASD: move   LMB: shoot   Shift: roll   Space: jump");
        }
    }
}
