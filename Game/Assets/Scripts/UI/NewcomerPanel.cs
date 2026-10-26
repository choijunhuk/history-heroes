using System;
using System.Collections.Generic;
using System.Text;
using HistoryHeroes.Economy;
using HistoryHeroes.Missions;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 신규 7일 미션 + 출석부 (v1.0, Missions 5·6장). 칸마다 한 줄: 미션 ●(끝)/○(아직) + 받기 상태. 누르면 그 칸 보상을 받는다.
    /// 미션 순서가 그대로 "처음 일주일에 할 것" 안내가 된다.
    /// </summary>
    public sealed class NewcomerPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text attendanceLabel;
        [SerializeField] private ButtonList list;
        [SerializeField] private Button backButton;

        private static readonly Color ReadyColor = new Color(0.22f, 0.45f, 0.25f);
        private static readonly Color OpenColor = new Color(0.22f, 0.26f, 0.34f);
        private static readonly Color DoneColor = new Color(0.16f, 0.17f, 0.2f);
        private static readonly Color LockedColor = new Color(0.13f, 0.13f, 0.15f);

        private Action _onChanged;
        private Action<string> _notice;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            backButton.onClick.AddListener(() => root.SetActive(false));
        }

        public void Open(Action onChanged, Action<string> notice)
        {
            _onChanged = onChanged;
            _notice = notice;
            root.SetActive(true);
            Render(scrollToTop: true);
        }

        private void Render(bool scrollToTop)
        {
            SaveData s = SaveSystem.Data;
            DateTimeOffset now = Clock.Now();
            int cycle = Attendance.CycleDay(s);
            attendanceLabel.text = $"출석부  {AttendanceRow(cycle)}   (누적 {s.attendance.days}일)\n<size=80%>매일 첫 접속에 자동으로 받아요 · 7일째 고급 소환권 · 누적 14일 · 28일 장비 조각</size>";

            var rows = new List<ButtonList.Row>();
            int index = Newcomer.DayIndex(s, now);
            for (int day = 1; day <= Newcomer.Days; day++)
            {
                int d = day;
                bool open = Newcomer.DayOpen(s, d, now);
                bool claimed = s.newcomer.claimedDays.Contains(d);
                bool ready = Newcomer.CanClaim(s, d, now);
                var sb = new StringBuilder();
                string state = claimed ? "<color=#7FD17F>받음</color>" : ready ? "<color=#FFD966>받기!</color>" : open ? "진행 중" : $"<color=#8A94A6>{d - 1 - index}일 뒤 열림</color>";
                sb.Append($"<b>{d}일째</b>  {Newcomer.DayRewards[d - 1]}   {state}\n<size=80%>");
                foreach (NewcomerMission m in Newcomer.OfDay(d))
                {
                    bool done = open && Newcomer.Done(s, m); // 안 열린 칸은 끝났어도 ○ (내용을 미리 안 보여 준다)
                    sb.Append(done ? "<color=#7FD17F>●</color> " : "○ ").Append(open ? Newcomer.TitleOf(s, m) : "???").Append("   ");
                }

                sb.Append("</size>");
                rows.Add(new ButtonList.Row(sb.ToString(), claimed ? DoneColor : ready ? ReadyColor : open ? OpenColor : LockedColor, () => Claim(d), ready));
            }

            bool bonusReady = Newcomer.CanClaimBonus(s, now);
            rows.Add(new ButtonList.Row($"<b>전부 완료 보너스</b>  {Newcomer.Bonus}   {(s.newcomer.bonusClaimed ? "<color=#7FD17F>받음</color>" : bonusReady ? "<color=#FFD966>받기!</color>" : $"{s.newcomer.claimedDays.Count}/7")}",
                bonusReady ? ReadyColor : OpenColor, ClaimBonus, bonusReady));
            rows.Add(new ButtonList.Row($"<size=80%>시작한 날부터 {Newcomer.DeadlineDays}일 안에 받아야 해요 (남은 {Mathf.Max(0, Newcomer.DeadlineDays - index)}일)</size>", LockedColor, null, false));
            list.Show(rows, scrollToTop);
        }

        private static string AttendanceRow(int cycle)
        {
            var sb = new StringBuilder();
            for (int i = 1; i <= Attendance.Cycle; i++) sb.Append(i <= cycle ? "<color=#FFD966>●</color>" : "○");
            return sb.ToString();
        }

        private void Claim(int day)
        {
            string line = null;
            SaveSystem.CommitResult r = SaveSystem.Commit(() => (line = Newcomer.ClaimDay(SaveSystem.Data, day, Clock.Now())) != null);
            _notice?.Invoke(r == SaveSystem.CommitResult.Saved ? line : SaveSystem.SaveFailedText);
            Render(scrollToTop: false);
            _onChanged?.Invoke();
        }

        private void ClaimBonus()
        {
            string line = null;
            SaveSystem.CommitResult r = SaveSystem.Commit(() => (line = Newcomer.ClaimBonus(SaveSystem.Data, Clock.Now())) != null);
            _notice?.Invoke(r == SaveSystem.CommitResult.Saved ? line : SaveSystem.SaveFailedText);
            Render(scrollToTop: false);
            _onChanged?.Invoke();
        }
    }
}
