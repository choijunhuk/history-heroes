using System;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 하트가 모자랄 때 (Economy.md 3장): 다음 하트까지 남은 시간 + 충전 방법 목록.
    /// 결제창을 바로 띄우지 않는다 (3-4 원칙). 광고 +2 (하루 3번), 보석 60 → +5 (하루 5번).
    /// </summary>
    public sealed class HeartPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button adButton;
        [SerializeField] private TMP_Text adLabel;
        [SerializeField] private Button gemButton;
        [SerializeField] private TMP_Text gemLabel;
        [SerializeField] private Button closeButton;

        private Action _onChanged;
        private bool _waitingForAd;
        private string _note;
        private float _nextRender;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            adButton.onClick.AddListener(WatchAd);
            gemButton.onClick.AddListener(BuyWithGems);
            closeButton.onClick.AddListener(() => root.SetActive(false));
        }

        public void Open(Action onChanged)
        {
            _onChanged = onChanged;
            _note = null;
            root.SetActive(true);
            Render();
        }

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < _nextRender) return;
            Render();
        }

        private void Render()
        {
            _nextRender = Time.unscaledTime + 0.5f;
            SaveData d = SaveSystem.Data;
            long now = Clock.UnixSeconds;
            string today = Clock.Today;
            int next = Hearts.SecondsToNext(d, now);
            DailyCounters daily = d.Today(today);

            string timer = d.hearts >= Hearts.Max ? "가득 찼어요" : $"다음 하트까지 {Hearts.FormatTime(next)}";
            messageLabel.text = $"하트가 부족해요\n지금 {d.hearts}개 · {timer}\n<size=70%>20분마다 1개씩, 최대 {Hearts.Max}개까지 차요</size>"
                                + (string.IsNullOrEmpty(_note) ? string.Empty : $"\n<color=#FF7A6B><size=70%>{_note}</size></color>");

            adButton.interactable = !_waitingForAd && Hearts.CanWatchAd(d, today);
            adLabel.text = _waitingForAd ? "광고 보는 중…" : $"광고 보고 하트 +{Hearts.AdReward}  ({daily.adHearts}/{Hearts.AdPerDay})";

            bool enoughGems = Wallet.Gems(d) >= Hearts.GemRefillCost;
            gemButton.interactable = !_waitingForAd && enoughGems && daily.gemHearts < Hearts.GemRefillPerDay;
            gemLabel.text = $"보석 {Hearts.GemRefillCost}개로 하트 +{Hearts.GemRefillAmount}  ({daily.gemHearts}/{Hearts.GemRefillPerDay})"
                            + (enoughGems ? string.Empty : "\n<size=70%>보석이 모자라요</size>");
        }

        private void WatchAd()
        {
            if (_waitingForAd) return;
            _waitingForAd = true;
            Render();
            Ads.Current.Show("hearts", success =>
            {
                _waitingForAd = false;
                if (success)
                {
                    Hearts.GrantAd(SaveSystem.Data, Clock.UnixSeconds, Clock.Today);
                    SaveSystem.Save();
                    _note = null;
                }
                else
                {
                    _note = "광고를 불러오지 못했어요 (횟수는 그대로)";
                }

                Render();
                _onChanged?.Invoke();
            });
        }

        private void BuyWithGems()
        {
            if (Hearts.TryBuyWithGems(SaveSystem.Data, Clock.UnixSeconds, Clock.Today)) SaveSystem.Save();
            Render();
            _onChanged?.Invoke();
        }
    }
}
