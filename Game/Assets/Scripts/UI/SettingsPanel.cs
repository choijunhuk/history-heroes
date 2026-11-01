using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 설정 (UX 5장 접근성, 조사 A9): 화면 흔들림 끄기/약하게/보통, 피해 숫자 켜기/끄기, 번쩍임 줄이기.
    /// 2026-11-01 화면 채우기 개편: 카드(묶음) 안의 전체 폭 줄 — 켜기/끄기는 스위치, 여러 값은 고르기 칸 (SettingRow).
    /// 값이 바뀌면 바로 저장한다 (판 밖 설정). 저장 값과 적용 로직은 예전 그대로.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button backButton;
        [Tooltip("오른쪽 위 X (뒤로와 같다)")]
        [SerializeField] private Button closeButton;
        [Header("줄 (오른쪽 조절 칸). 줄을 누르면 값이 바로 저장된다")]
        [SerializeField] private SettingRow musicRow;
        [SerializeField] private SettingRow sfxRow;
        [SerializeField] private SettingRow vibrationRow;
        [Tooltip("그래픽 품질 3단 (낮음 · 보통 · 높음)")]
        [SerializeField] private SettingRow graphicsRow;
        [SerializeField] private SettingRow shakeRow;
        [SerializeField] private SettingRow numbersRow;
        [SerializeField] private SettingRow flashRow;
        [SerializeField] private SettingRow frameRow;
        [SerializeField] private SettingRow joystickModeRow;
        [SerializeField] private SettingRow joystickSizeRow;
        [SerializeField] private SettingRow joystickOpacityRow;
        [SerializeField] private SettingRow heartsRow;
        [SerializeField] private SettingRow patrolRow;
        [Header("v1.0 튜토리얼 다시 보기 · 정보 (로비에만)")]
        [SerializeField] private Button replayTutorialButton;
        [SerializeField] private Button infoButton;
        [SerializeField] private InfoPanel info;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            backButton.onClick.AddListener(Close);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            // 값 번호는 저장 값 그대로: 흔들림 0끄기 1약하게 2보통 · 음량 0~4(×25%) · 그래픽 0~2 · 크기/진하기 0~2
            Bind(musicRow, v => Change(s => s.musicVolume = v));
            Bind(sfxRow, v => Change(s => s.sfxVolume = v));
            Bind(vibrationRow, v => Change(s => s.vibration = v != 0));
            Bind(graphicsRow, v => Change(s => s.graphics = v));
            Bind(shakeRow, v => Change(s => s.shake = v));
            Bind(numbersRow, v => Change(s => s.damageNumbers = v != 0));
            Bind(flashRow, v => Change(s => s.reduceFlashes = v != 0));
            Bind(frameRow, v => Change(s => s.frame60 = v == 1));
            Bind(joystickModeRow, v => Change(s => s.joystickFixed = v == 1));
            Bind(joystickSizeRow, v => Change(s => s.joystickSize = v));
            Bind(joystickOpacityRow, v => Change(s => s.joystickOpacity = v));
            Bind(heartsRow, v => ChangeNotice(s => s.notifyHearts = v != 0));
            Bind(patrolRow, v => ChangeNotice(s => s.notifyPatrol = v != 0));
            if (infoButton != null) infoButton.onClick.AddListener(() => info?.Open());
            if (replayTutorialButton != null) replayTutorialButton.onClick.AddListener(() => Common.GameSession.StartTutorial(true));
        }

        private static void Bind(SettingRow row, System.Action<int> onChosen)
        {
            if (row != null) row.Chosen += onChosen;
        }

        public void Open()
        {
            root.SetActive(true);
            Render();
        }

        public void Close() => root.SetActive(false);

        /// <summary>알림을 켜면 그때 권한도 묻는다 (처음 물었을 때 "괜찮아요"를 골랐어도)</summary>
        private void ChangeNotice(System.Action<GameSettings> change)
        {
            GameSettings s = SaveSystem.Data.settings;
            change(s);
            if (s.notifyHearts || s.notifyPatrol)
            {
                s.notifyAsked = true;
                Common.LocalNotices.RequestPermission();
            }

            SaveSystem.Save();
            Render();
        }

        private void Change(System.Action<GameSettings> change)
        {
            change(SaveSystem.Data.settings);
            Common.SettingsApplier.Apply(SaveSystem.Data.settings);
            Audio.AudioManager.RefreshVolume();
            SaveSystem.Save();
            Render();
        }

        private static void Show(SettingRow row, int value)
        {
            if (row != null) row.Show(value);
        }

        private void Render()
        {
            GameSettings s = SaveSystem.Data.settings;
            Show(musicRow, Mathf.Clamp(s.musicVolume, 0, 4));
            Show(sfxRow, Mathf.Clamp(s.sfxVolume, 0, 4));
            Show(vibrationRow, s.vibration ? 1 : 0);
            Show(graphicsRow, Mathf.Clamp(s.graphics, 0, 2));
            Show(shakeRow, Mathf.Clamp(s.shake, 0, 2));
            Show(numbersRow, s.damageNumbers ? 1 : 0);
            Show(flashRow, s.reduceFlashes ? 1 : 0);
            Show(frameRow, s.frame60 ? 1 : 0);
            Show(joystickModeRow, s.joystickFixed ? 1 : 0);
            Show(joystickSizeRow, Mathf.Clamp(s.joystickSize, 0, 2));
            Show(joystickOpacityRow, Mathf.Clamp(s.joystickOpacity, 0, 2));
            Show(heartsRow, s.notifyHearts ? 1 : 0);
            Show(patrolRow, s.notifyPatrol ? 1 : 0);
        }
    }
}
