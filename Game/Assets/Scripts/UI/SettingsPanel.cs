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

        private static readonly string[] ShakeNames = { "끄기", "약하게", "보통" };

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            shakeButton.onClick.AddListener(() => Change(s => s.shake = (s.shake + 2) % 3)); // 보통 → 약하게 → 끄기 → 보통
            numbersButton.onClick.AddListener(() => Change(s => s.damageNumbers = !s.damageNumbers));
            flashButton.onClick.AddListener(() => Change(s => s.reduceFlashes = !s.reduceFlashes));
            backButton.onClick.AddListener(() => root.SetActive(false));
        }

        public void Open()
        {
            root.SetActive(true);
            Render();
        }

        public void Close() => root.SetActive(false);

        private void Change(System.Action<GameSettings> change)
        {
            change(SaveSystem.Data.settings);
            SaveSystem.Save();
            Render();
        }

        private void Render()
        {
            GameSettings s = SaveSystem.Data.settings;
            shakeLabel.text = $"화면 흔들림: {ShakeNames[Mathf.Clamp(s.shake, 0, 2)]}";
            numbersLabel.text = $"피해 숫자: {(s.damageNumbers ? "켜기" : "끄기")}";
            flashLabel.text = $"번쩍임 줄이기: {(s.reduceFlashes ? "켜기" : "끄기")}";
        }
    }
}
