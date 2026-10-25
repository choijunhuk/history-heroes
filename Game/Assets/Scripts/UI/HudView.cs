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

        // 지난번에 그린 값. 같으면 글자를 다시 만들지 않는다 (모바일에서 매 프레임 문자열 = GC)
        private int _hp = -1, _maxHp = -1, _level = -1, _kills = -1;

        private void Update()
        {
            if (player != null && player.Health != null)
            {
                float max = Mathf.Max(1f, player.Health.Max);
                hpFill.fillAmount = player.Health.Current / max;

                int hp = Mathf.CeilToInt(player.Health.Current), maxHp = Mathf.CeilToInt(max);
                if (hp != _hp || maxHp != _maxHp)
                {
                    _hp = hp;
                    _maxHp = maxHp;
                    hpLabel.SetText("{0} / {1}", hp, maxHp);
                }
            }

            if (experience != null)
            {
                expFill.fillAmount = experience.Level.Progress01;

                if (experience.Level.Level != _level)
                {
                    _level = experience.Level.Level;
                    levelLabel.SetText("Lv {0}", _level);
                }

                if (experience.Kills != _kills)
                {
                    _kills = experience.Kills;
                    killLabel.SetText("처치 {0}", _kills);
                }
            }
        }
    }
}
