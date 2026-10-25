using System.Text;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>판 결과 화면: 시간, 처치, 레벨, 만든 조합, 가진 스킬. (Content.md 2-4, v0.1 16단계)</summary>
    public sealed class RunResultScreen : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button retryButton;

        private void Awake()
        {
            root.SetActive(false);
            retryButton.onClick.AddListener(Retry);
        }

        public void Show(RunSummary s)
        {
            GamePause.Pause(this);
            root.SetActive(true);

            titleLabel.text = s.Victory ? "클리어!" : "쓰러졌다…";

            int minutes = Mathf.FloorToInt(s.Seconds / 60f);
            int seconds = Mathf.FloorToInt(s.Seconds % 60f);
            var sb = new StringBuilder();
            sb.AppendLine($"시간  {minutes:00}:{seconds:00}");
            sb.AppendLine($"도달한 방  {s.RoomReached} / {s.RoomCount}");
            sb.AppendLine($"처치  {s.Kills}");
            sb.AppendLine($"레벨  {s.Level}");
            sb.AppendLine();
            sb.AppendLine("만든 조합");
            sb.AppendLine(s.Fusions.Count > 0 ? "  " + string.Join(", ", s.Fusions) : "  없음");
            sb.AppendLine();
            sb.AppendLine("스킬");
            foreach (string skill in s.Skills) sb.AppendLine("  " + skill);
            bodyLabel.text = sb.ToString();
        }

        private void Retry()
        {
            GamePause.ResetAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
