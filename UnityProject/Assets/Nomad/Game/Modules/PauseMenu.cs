using System.Collections.Generic;
using Nomad.Combat;
using Nomad.Equipment;
using Nomad.Interaction;
using Nomad.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Nomad.Game.Modules
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        private readonly List<Behaviour> suspendedInputs = new();
        private GameManager manager;
        private PauseMenuView view;
        private GameObject previousSelection;
        private float previousTimeScale;
        private bool previousAudioPause;
        private bool showingSettings;

        public bool IsOpen { get; private set; }

        private void Start() => manager = GetComponentInParent<GameManager>();

        private void Update()
        {
            if (manager == null || manager.IsLoading || manager.IsRestarting || manager.IsChoosingField ||
                manager.Player == null || !manager.Player.activeInHierarchy ||
                (manager.Player.TryGetComponent(out Health health) && health.IsDead))
            {
                Resume();
                return;
            }

            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true)
                return;

            // 열린 장비·가방·상자의 Esc는 창 닫기에만 사용합니다.
            if (!IsOpen && manager.Player.GetComponent<PlayerInventory>()?.Panel?.IsOpen == true)
                return;

            if (showingSettings)
                ShowMenu();
            else if (IsOpen)
                Resume();
            else if (manager.Player.GetComponent<PlayerInventory>()?.BlocksTradeInput != true)
                Open();
        }

        private void Open()
        {
            view ??= new PauseMenuView(transform, Resume, Restart, ShowSettings, Quit, ShowMenu);
            previousSelection = EventSystem.current?.currentSelectedGameObject;
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            IsOpen = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            // 시간을 멈춰도 직접 키와 마우스를 읽는 상호작용·가방·장비 입력은 따로 막습니다.
            GameObject player = manager.Player;
            PauseInput(player.GetComponent<PlayerInteraction>());
            PauseInput(player.GetComponent<EquipmentPanel>());
            PauseInput(player.GetComponent<PlayerInventory>()?.Panel);
            ShowMenu();
        }

        private void PauseInput(Behaviour input)
        {
            if (input == null || !input.enabled)
                return;

            suspendedInputs.Add(input);
            input.enabled = false;
        }

        private void Resume()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            showingSettings = false;
            view.Hide();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                    ? previousSelection
                    : null);

            for (int i = suspendedInputs.Count - 1; i >= 0; i--)
            {
                if (suspendedInputs[i] != null)
                    suspendedInputs[i].enabled = true;
            }

            suspendedInputs.Clear();
            previousSelection = null;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
        }

        private void ShowMenu()
        {
            showingSettings = false;
            view.Show(false);
        }

        private void ShowSettings()
        {
            showingSettings = true;
            view.Show(true);
        }

        private void Restart()
        {
            Resume();
            Time.timeScale = 1f;
            manager.Restart();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDisable() => Resume();

        private void OnDestroy() => view?.ReleaseFont();
    }
}
