using System;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Meta;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 로비 "우편함" (LiveOps 4장): 안 받은 우편 먼저, 받은 우편은 흐리게 뒤로. 줄을 누르면 그 우편, [모두 받기]는 전부.
    /// 로비는 Open(onClosed)로 열고, 빨간 점은 MailBox.UnclaimedCount로 센다.
    /// </summary>
    public sealed class MailPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private ButtonList list;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private Button claimAllButton;
        [SerializeField] private TMP_Text claimAllLabel;
        [SerializeField] private Button backButton;
        [Tooltip("저장 실패 안내")]
        [SerializeField] private TMP_Text messageLabel;

        private static readonly Color RowNew = new Color(0.96f, 0.58f, 0.16f);
        private static readonly Color RowTaken = UiTone.Lift(new Color(0.14f, 0.3f, 0.2f));

        private Action _onClosed;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            claimAllButton.onClick.AddListener(ClaimAll);
            backButton.onClick.AddListener(() =>
            {
                root.SetActive(false);
                _onClosed?.Invoke();
            });
        }

        public void Open(Action onClosed = null)
        {
            _onClosed = onClosed;
            // 열 때 정리 (기한 지난 우편 · 100통 넘는 받은 우편)
            SaveData save = SaveSystem.Data;
            DateTimeOffset now = Clock.Now();
            SaveSystem.Commit(() => MailBox.Refresh(save, now)); // 바뀐 게 없으면 저장하지 않는다
            if (messageLabel != null) messageLabel.text = string.Empty;
            root.SetActive(true);
            Render();
        }

        private void Render()
        {
            SaveData save = SaveSystem.Data;
            DateTimeOffset now = Clock.Now();
            List<MailItem> mails = MailBox.Sorted(save, now);
            int unclaimed = MailBox.UnclaimedCount(save, now);

            var rows = new List<ButtonList.Row>();
            foreach (MailItem m in mails)
            {
                string id = m.id;
                rows.Add(new ButtonList.Row(RowText(m, now), m.claimed ? RowTaken : RowNew, m.claimed ? (Action)null : () => Claim(id), !m.claimed));
            }

            list.Show(rows, true);
            emptyLabel.gameObject.SetActive(mails.Count == 0);
            claimAllButton.interactable = unclaimed > 0;
            claimAllLabel.text = unclaimed > 0 ? $"모두 받기 ({unclaimed})" : "모두 받기";
        }

        private static string RowText(MailItem m, DateTimeOffset now)
        {
            string reward = MailBox.ToReward(m).ToString();
            string tail = m.claimed ? "받음" : "<b>[받기]</b>";
            int days = MailBox.DaysLeft(m, now);
            string left = days >= 0 && !m.claimed ? $" · {days}일 남음" : string.Empty;
            string body = string.IsNullOrEmpty(m.body) ? string.Empty : $"\n<size=85%>{m.body}</size>";
            return $"<size=118%><b>{m.title}</b></size>{body}\n<size=85%>{reward}{left}  {tail}</size>";
        }

        private void Claim(string id)
        {
            Reward granted = default;
            SaveData save = SaveSystem.Data;
            DateTimeOffset now = Clock.Now();
            SaveSystem.CommitResult r = SaveSystem.Commit(() => MailBox.Claim(save, id, now, out granted));
            Finish(r, granted);
        }

        private void ClaimAll()
        {
            Reward granted = default;
            SaveData save = SaveSystem.Data;
            DateTimeOffset now = Clock.Now();
            SaveSystem.CommitResult r = SaveSystem.Commit(() =>
            {
                granted = MailBox.ClaimAll(save, now, out int count);
                return count > 0;
            });
            Finish(r, granted);
        }

        private void Finish(SaveSystem.CommitResult r, Reward granted)
        {
            if (r == SaveSystem.CommitResult.Rejected) return;
            // 저장을 못 하면 받기를 되돌렸다: 아무 일 없는 것처럼 보이지 않게 이유를 보여 준다
            if (messageLabel != null) messageLabel.text = r == SaveSystem.CommitResult.SaveFailed ? SaveSystem.SaveFailedText : string.Empty;
            if (r == SaveSystem.CommitResult.Saved && !granted.IsEmpty) RewardPopup.Show(granted.ToString());
            Render();
        }
    }
}
