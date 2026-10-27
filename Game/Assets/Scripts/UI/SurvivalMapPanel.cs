using System;
using HistoryHeroes.Common;
using HistoryHeroes.Records;
using HistoryHeroes.Survival;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 서바이벌 맵 고르기 (확장 1차 6-6장): 맵 카드를 좌우로 넘긴다 — 이름 · 지형 · 별 3칸 · 최고 시간 · 시간 · 마지막 보스 · 맵 임무 · 잠금 조건.
    /// 열린 맵이면 "시작 (하트 1)".
    /// </summary>
    public sealed class SurvivalMapPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private SurvivalData[] maps = new SurvivalData[0];
        [SerializeField] private Image banner;
        [SerializeField] private TMP_Text indexLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text blurbLabel;
        [SerializeField] private TMP_Text starsLabel;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private TMP_Text lockLabel;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private UiPop cardPop;

        private int _index;
        private Action<int> _onStart;

        public bool IsOpen => root != null && root.activeSelf;
        public int Index => _index;

        private void Awake()
        {
            root.SetActive(false);
            prevButton.onClick.AddListener(() => Show(_index - 1));
            nextButton.onClick.AddListener(() => Show(_index + 1));
            startButton.onClick.AddListener(Begin);
            closeButton.onClick.AddListener(Close);
        }

        /// <summary>열린 맵 중 가장 뒤 맵부터 보여 준다</summary>
        public void Open(Action<int> onStart)
        {
            _onStart = onStart;
            SaveData d = SaveSystem.Data;
            int last = 0;
            for (int i = 0; i < maps.Length; i++)
            {
                if (GameSession.SurvivalMapUnlocked(d, i)) last = i;
            }

            root.SetActive(true);
            Show(last);
        }

        public void Close() => root.SetActive(false);

        public void Show(int index)
        {
            if (maps.Length == 0) return;
            _index = (index % maps.Length + maps.Length) % maps.Length;
            SurvivalData map = maps[_index];
            SaveData d = SaveSystem.Data;
            bool open = GameSession.SurvivalMapUnlocked(d, _index);
            SurvivalMapSave save = d.survival.FindMap(map.mapId);
            int stars = save != null ? SurvivalProgress.StarCount(save.stars) : 0;

            if (banner != null) banner.color = open ? map.mapColor : Color.Lerp(map.mapColor, new Color(0.25f, 0.25f, 0.28f), 0.7f);
            indexLabel.text = $"맵 {_index + 1} / {maps.Length}";
            nameLabel.text = map.mapName;
            blurbLabel.text = map.mapBlurb;
            starsLabel.text = Economy.Stars.Display(stars);
            string best = save != null && save.bestSeconds > 0f ? SurvivalRun.Format(save.bestSeconds) : "—";
            infoLabel.text = $"{SurvivalRun.Format(map.duration)} 버티기 · 마지막 보스 <color=#FFD27A>{map.bossName}</color>\n" +
                             $"최고 기록 {best}\n" +
                             $"★1 클리어  ★2 {MissionText(map)}  ★3 전설 없이 클리어";
            lockLabel.gameObject.SetActive(!open);
            lockLabel.text = "잠김 — " + SurvivalProgress.LockText(_index);
            startButton.interactable = open;
            startLabel.text = open ? $"시작 · 하트 {GameSession.SurvivalHearts}" : "잠김";
            prevButton.gameObject.SetActive(maps.Length > 1);
            nextButton.gameObject.SetActive(maps.Length > 1);
            if (cardPop != null) cardPop.Play();
        }

        private static string MissionText(SurvivalData map)
        {
            switch (map.mission)
            {
                case SurvivalMission.Haystacks: return $"짚더미 {map.missionTarget}개";
                case SurvivalMission.Braziers: return $"화로 {map.missionTarget}개";
                default: return $"{SurvivalRun.Format(map.missionDeadline)} 전 엘리트 {map.missionTarget}";
            }
        }

        private void Begin()
        {
            if (!GameSession.SurvivalMapUnlocked(SaveSystem.Data, _index)) return;
            Action<int> start = _onStart;
            start?.Invoke(_index);
        }
    }
}
