using HistoryHeroes.Player;
using HistoryHeroes.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>전투 화면 위쪽: 체력, 경험치, 레벨, 방 번호, 처치 수. (UX.md 2장)</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private ExperienceSystem experience;

        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private Image expFill;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text roomLabel;
        [SerializeField] private TMP_Text killLabel;

        public string RoomText { set => roomLabel.text = value; }

        private void Update()
        {
            if (player != null && player.Health != null)
            {
                float max = Mathf.Max(1f, player.Health.Max);
                hpFill.fillAmount = player.Health.Current / max;
                hpLabel.text = $"{Mathf.CeilToInt(player.Health.Current)} / {Mathf.CeilToInt(max)}";
            }

            if (experience != null)
            {
                expFill.fillAmount = experience.Level.Progress01;
                levelLabel.text = $"Lv {experience.Level.Level}";
                killLabel.text = $"처치 {experience.Kills}";
            }
        }
    }
}
