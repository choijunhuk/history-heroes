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

        [Header("방 진행 바 · 알림 (조사 A6·A7)")]
        [SerializeField] private RectTransform roomBar;
        [SerializeField] private TMP_Text noticeLabel;
        [Header("그래픽 재평가 2번: 방 칸 모양 · 표시 아이콘 (없으면 글자)")]
        [SerializeField] private Sprite cellSprite;
        [SerializeField] private Sprite eliteIcon;
        [SerializeField] private Sprite rewardIcon;
        [SerializeField] private Sprite bossIcon;
        private readonly System.Collections.Generic.List<Image> _cells = new System.Collections.Generic.List<Image>();
        private float _noticeUntil;

        private static readonly Color CellPast = new Color(0.55f, 0.75f, 0.55f, 0.9f);
        private static readonly Color CellNext = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color CellElite = new Color(1f, 0.8f, 0.25f, 0.95f);

        /// <summary>
        /// 방 칸: 지난 방은 채움, 지금 방은 크게, 엘리트 방은 금색 + ★ (Archero 2처럼 다음 엘리트까지 계획할 수 있게).
        /// elite[i] = i번째 방(0부터)이 엘리트인가.
        /// </summary>
        public void SetRoomProgress(int room, System.Collections.Generic.IReadOnlyList<bool> elite)
        {
            if (roomBar == null) return;
            roomBar.gameObject.SetActive(true);
            while (_cells.Count < elite.Count) _cells.Add(CreateCell(_cells.Count));

            const float size = 26f, gap = 8f;
            float width = elite.Count * size + (elite.Count - 1) * gap;
            for (int i = 0; i < _cells.Count; i++)
            {
                Image cell = _cells[i];
                bool exists = i < elite.Count;
                cell.gameObject.SetActive(exists);
                if (!exists) continue;

                bool current = i == room - 1;
                bool past = i < room - 1;
                var rt = (RectTransform)cell.transform;
                rt.anchoredPosition = new Vector2(-width * 0.5f + size * 0.5f + i * (size + gap), 0f);
                rt.sizeDelta = Vector2.one * (current ? size * 1.35f : size);
                Color color = elite[i] ? CellElite : past ? CellPast : current ? Color.white : CellNext;
                if (past && elite[i]) color = new Color(0.75f, 0.6f, 0.25f, 0.9f);
                cell.color = color;
                Transform mark = cell.transform.GetChild(0);
                mark.gameObject.SetActive(elite[i]);
                if (elite[i] && mark.TryGetComponent(out Image icon)) icon.sprite = eliteIcon;
            }
        }

        /// <summary>v0.6: 방 종류별 칸 (★ 엘리트 · + 보상 · ! 중간 보스)</summary>
        public void SetRoomProgress(int room, System.Collections.Generic.IReadOnlyList<Dungeon.RoomKind> kinds)
        {
            var special = new System.Collections.Generic.List<bool>(kinds.Count);
            foreach (Dungeon.RoomKind k in kinds) special.Add(k != Dungeon.RoomKind.Normal);
            SetRoomProgress(room, special);
            for (int i = 0; i < kinds.Count && i < _cells.Count; i++)
            {
                Transform mark = _cells[i].transform.GetChild(0);
                bool boss = kinds[i] == Dungeon.RoomKind.MidBoss || kinds[i] == Dungeon.RoomKind.Boss;
                if (mark.TryGetComponent(out Image icon))
                {
                    // 아이콘으로: 보스 = 해골, 보상 = 상자, 엘리트 = 별 (작아도 모양으로 갈린다)
                    icon.sprite = kinds[i] == Dungeon.RoomKind.Reward ? rewardIcon : boss ? bossIcon : eliteIcon;
                }
                else if (mark.TryGetComponent(out TMPro.TMP_Text label))
                {
                    label.text = kinds[i] == Dungeon.RoomKind.Reward ? "+" : boss ? "!" : "★";
                }
                else continue;
                bool past = i < room - 1;
                if (kinds[i] == Dungeon.RoomKind.Reward) _cells[i].color = past ? new Color(0.35f, 0.6f, 0.45f, 0.9f) : new Color(0.45f, 0.85f, 0.6f, 0.95f);
                if (kinds[i] == Dungeon.RoomKind.MidBoss || kinds[i] == Dungeon.RoomKind.Boss) _cells[i].color = past ? new Color(0.6f, 0.3f, 0.3f, 0.9f) : new Color(0.95f, 0.35f, 0.35f, 0.95f);
            }
        }

        public void HideRoomProgress()
        {
            if (roomBar != null) roomBar.gameObject.SetActive(false);
        }

        private Image CreateCell(int index)
        {
            var go = new GameObject($"Room{index + 1}", typeof(RectTransform));
            go.transform.SetParent(roomBar, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            if (cellSprite != null)
            {
                // 둥근 칸 + 아이콘 (그래픽 재평가 2번)
                image.sprite = cellSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 3f;
                var markGo = new GameObject("Mark", typeof(RectTransform));
                markGo.transform.SetParent(go.transform, false);
                var mark = markGo.AddComponent<Image>();
                mark.raycastTarget = false;
                mark.preserveAspect = true;
                mark.color = new Color(0.15f, 0.08f, 0.02f, 0.95f);
                var mrt = (RectTransform)markGo.transform;
                mrt.anchorMin = new Vector2(0.15f, 0.15f);
                mrt.anchorMax = new Vector2(0.85f, 0.85f);
                mrt.offsetMin = mrt.offsetMax = Vector2.zero;
                return image;
            }

            var star = new GameObject("Star", typeof(RectTransform));
            star.transform.SetParent(go.transform, false);
            var label = star.AddComponent<TextMeshProUGUI>();
            label.text = "★";
            label.fontSize = 20;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.3f, 0.2f, 0f);
            label.raycastTarget = false;
            var srt = (RectTransform)star.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            return image;
        }

        private void OnEnable() => Missions.MissionEvents.Completed += OnMissionCompleted;
        private void OnDisable() => Missions.MissionEvents.Completed -= OnMissionCompleted;
        private void OnMissionCompleted(string line) => ShowNotice(line, 2.2f);

        /// <summary>잠깐 뜨는 알림 (방 클리어 때 기록 용량 등)</summary>
        public void ShowNotice(string text, float seconds = 1.8f)
        {
            if (noticeLabel == null) return;
            noticeLabel.text = text;
            noticeLabel.gameObject.SetActive(true);
            _noticeUntil = Time.unscaledTime + seconds;
        }

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

        /// <summary>서바이벌: 방 진행 바·맞은 횟수 대신 남은 시간 (RoomText)</summary>
        public void SetSurvivalMode()
        {
            HideRoomProgress();
            SetHitCounter(false, null, 0);
        }

        /// <summary>보스맵: 레벨업이 없으므로 레벨·경험치·처치 수를 숨긴다 (GameDesign 7-3)</summary>
        public void SetBossMode()
        {
            HideRoomProgress();
            levelLabel.gameObject.SetActive(false);
            killLabel.gameObject.SetActive(false);
            expFill.transform.parent.gameObject.SetActive(false);
            experience = null;
        }

        // 지난번에 그린 값. 같으면 글자를 다시 만들지 않는다 (모바일에서 매 프레임 문자열 = GC)
        private int _hp = -1, _maxHp = -1, _level = -1, _kills = -1;

        private void Update()
        {
            if (noticeLabel != null && noticeLabel.gameObject.activeSelf && Time.unscaledTime > _noticeUntil) noticeLabel.gameObject.SetActive(false);

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
