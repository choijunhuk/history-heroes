using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 설정 (UX 5장 접근성, 조사 A9): 화면 흔들림 끄기/약하게/보통, 피해 숫자 켜기/끄기, 번쩍임 줄이기.
    /// 버튼을 누를 때마다 다음 값으로 바뀌고 바로 저장한다 (판 밖 설정).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button shakeButton;
        [SerializeField] private TMP_Text shakeLabel;
        [SerializeField] private Button numbersButton;
        [SerializeField] private TMP_Text numbersLabel;
        [SerializeField] private Button flashButton;
        [SerializeField] private TMP_Text flashLabel;
        [SerializeField] private Button backButton;
        [Header("v1.0 프레임 · 조이스틱")]
        [SerializeField] private Button frameButton;
        [SerializeField] private TMP_Text frameLabel;
        [SerializeField] private Button joystickModeButton;
        [SerializeField] private TMP_Text joystickModeLabel;
        [SerializeField] private Button joystickSizeButton;
        [SerializeField] private TMP_Text joystickSizeLabel;
        [SerializeField] private Button joystickOpacityButton;
        [SerializeField] private TMP_Text joystickOpacityLabel;
        [Header("v1.0 튜토리얼 다시 보기 · 정보 (로비에만)")]
        [SerializeField] private Button replayTutorialButton;
        [SerializeField] private Button infoButton;
        [SerializeField] private InfoPanel info;
        [Header("v1.0 알림")]
        [SerializeField] private Button heartsButton;
        [SerializeField] private TMP_Text heartsLabel;
        [SerializeField] private Button patrolButton;
        [SerializeField] private TMP_Text patrolLabel;

        private static readonly string[] ShakeNames = { "끄기", "약하게", "보통" };

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            shakeButton.onClick.AddListener(() => Change(s => s.shake = (s.shake + 2) % 3)); // 보통 → 약하게 → 끄기 → 보통
            numbersButton.onClick.AddListener(() => Change(s => s.damageNumbers = !s.damageNumbers));
            flashButton.onClick.AddListener(() => Change(s => s.reduceFlashes = !s.reduceFlashes));
            backButton.onClick.AddListener(() => root.SetActive(false));
            if (frameButton != null) frameButton.onClick.AddListener(() => Change(s => s.frame60 = !s.frame60));
            if (joystickModeButton != null) joystickModeButton.onClick.AddListener(() => Change(s => s.joystickFixed = !s.joystickFixed));
            if (joystickSizeButton != null) joystickSizeButton.onClick.AddListener(() => Change(s => s.joystickSize = (s.joystickSize + 1) % 3));
            if (joystickOpacityButton != null) joystickOpacityButton.onClick.AddListener(() => Change(s => s.joystickOpacity = (s.joystickOpacity + 1) % 3));
            if (infoButton != null) infoButton.onClick.AddListener(() => info?.Open());
            if (replayTutorialButton != null) replayTutorialButton.onClick.AddListener(() => Common.GameSession.StartTutorial(true));
            if (heartsButton != null) heartsButton.onClick.AddListener(() => ChangeNotice(s => s.notifyHearts = !s.notifyHearts));
            if (patrolButton != null) patrolButton.onClick.AddListener(() => ChangeNotice(s => s.notifyPatrol = !s.notifyPatrol));
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

        private static readonly string[] Steps3 = { "작게", "보통", "크게" };
        private static readonly string[] Opacity3 = { "연하게", "보통", "진하게" };

        private void Change(System.Action<GameSettings> change)
        {
            change(SaveSystem.Data.settings);
            Common.SettingsApplier.Apply(SaveSystem.Data.settings);
            SaveSystem.Save();
            Render();
        }

        private void Render()
        {
            GameSettings s = SaveSystem.Data.settings;
            shakeLabel.text = $"화면 흔들림: {ShakeNames[Mathf.Clamp(s.shake, 0, 2)]}";
            numbersLabel.text = $"피해 숫자: {(s.damageNumbers ? "켜기" : "끄기")}";
            flashLabel.text = $"번쩍임 줄이기: {(s.reduceFlashes ? "켜기" : "끄기")}";
            if (frameLabel != null) frameLabel.text = $"프레임: {(s.frame60 ? "60 (부드럽게)" : "30 (배터리 절약)")}";
            if (joystickModeLabel != null) joystickModeLabel.text = $"조이스틱: {(s.joystickFixed ? "고정" : "누른 자리")}";
            if (joystickSizeLabel != null) joystickSizeLabel.text = $"조이스틱 크기: {Steps3[Mathf.Clamp(s.joystickSize, 0, 2)]}";
            if (joystickOpacityLabel != null) joystickOpacityLabel.text = $"조이스틱 진하기: {Opacity3[Mathf.Clamp(s.joystickOpacity, 0, 2)]}";
            if (heartsLabel != null) heartsLabel.text = $"하트가 다 차면: {(s.notifyHearts ? "켜기" : "끄기")}";
            if (patrolLabel != null) patrolLabel.text = $"순찰이 가득 차면: {(s.notifyPatrol ? "켜기" : "끄기")}";
        }
    }
}
