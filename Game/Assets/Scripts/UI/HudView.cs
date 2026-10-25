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
        [SerializeField] private TMP_Text hitsLabel;

        private System.Func<int> _hits;
        private int _hitLimit;
        private int _shownHits = -1;

        /// <summary>★2 도전 중이면 "맞은 횟수 n/한도"를 보여준다. 이미 땄으면 숨긴다 (Modes 3-2)</summary>
        public void SetHitCounter(bool show, System.Func<int> hits, int limit)
        {
            _hits = show ? hits : null;
            _hitLimit = limit;
            _shownHits = -1;
            if (hitsLabel != null) hitsLabel.gameObject.SetActive(show);
        }

        public string RoomText { set => roomLabel.text = value; }

        /// <summary>보스맵: 레벨업이 없으므로 레벨·경험치·처치 수를 숨긴다 (GameDesign 7-3)</summary>
        public void SetBossMode()
        {
            levelLabel.gameObject.SetActive(false);
            killLabel.gameObject.SetActive(false);
            expFill.transform.parent.gameObject.SetActive(false);
            experience = null;
        }

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

            if (_hits != null && hitsLabel != null)
            {
                int hits = _hits();
                if (hits != _shownHits)
                {
                    _shownHits = hits;
                    hitsLabel.SetText("맞음 {0}/{1}", hits, _hitLimit);
                    hitsLabel.color = hits > _hitLimit ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 1f, 1f, 0.8f);
                }
            }
        }
    }
}
