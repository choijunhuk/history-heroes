using System;
using System.Text;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Modes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 판 결과 화면 (Content.md 2-4). 던전: 결과 → [기록 남기기] / [로비로]. 보스: 결과 → [다시 도전] / [로비로].
    /// </summary>
    public sealed class RunResultScreen : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TMP_Text secondaryLabel;

        private Action _primary;
        private Action _secondary;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            primaryButton.onClick.AddListener(() => _primary?.Invoke());
            secondaryButton.onClick.AddListener(() => _secondary?.Invoke());
        }

        /// <param name="saveRecord">기록을 남길 수 있으면 그 화면을 여는 동작, 못 남기면 null</param>
        public void Show(RunSummary s, Action saveRecord)
        {
            Open();
            titleLabel.text = s.Victory ? "클리어!" : "쓰러졌다…";

            // 도달 방 · 받은 것 · 기록 용량을 먼저, 자세한 숫자는 아래 작게 (전체 UI 검토: "목표 40%" 같은 개발용 글은 뺀다)
            var sb = new StringBuilder();
            sb.AppendLine($"<size=120%>도달한 방  <b>{s.RoomReached} / {s.RoomCount}</b></size>");
            AppendRewards(sb, s.RewardLines);
            sb.AppendLine();
            sb.AppendLine(s.Capacity > 0 ? $"남길 수 있는 기록 용량  <b>{s.Capacity}pt</b>" : "너무 일찍 쓰러져서 남길 수 있는 기록이 없어요");
            sb.AppendLine();
            sb.AppendLine($"<size=85%>시간 {Clock(s.Seconds)} · 처치 {s.Kills} · 레벨 {s.Level}");
            if (!string.IsNullOrEmpty(s.TopDamage)) sb.AppendLine($"피해  {s.TopDamage}");
            sb.AppendLine($"만든 조합  {(s.Fusions.Count > 0 ? string.Join(", ", s.Fusions) : "없음")}");
            sb.AppendLine($"스킬  {(s.Skills.Count > 0 ? string.Join(" · ", s.Skills) : "없음")}</size>");
            bodyLabel.text = sb.ToString();

            if (saveRecord != null) SetButtons("기록 남기기", saveRecord, "로비로", GameSession.ToLobby);
            else SetButtons("로비로", GameSession.ToLobby, null, null);
        }

        public void ShowBoss(BossSummary s)
        {
            Open();
            titleLabel.text = s.Victory ? "격파!" : "패배…";

            var sb = new StringBuilder();
            sb.AppendLine(s.BossName);
            if (s.Victory) sb.AppendLine($"시간  {Clock(s.Seconds)}");
            string star2 = s.Star2MaxHits > 0 ? $"  (★2: {s.Star2MaxHits}번 이하)" : string.Empty;
            sb.AppendLine($"맞은 횟수  {s.HitsTaken}{star2}");
            sb.AppendLine();
            sb.AppendLine("쓴 기록");
            sb.AppendLine("  " + s.RecordLabel);
            sb.AppendLine("  기록은 이겨도 져도 사라지지 않아요");
            if (s.FirstClear)
            {
                sb.AppendLine();
                sb.AppendLine($"처음 격파! {s.UnlockedChapter}구간이 열렸어요");
            }

            AppendRewards(sb, s.RewardLines);
            bodyLabel.text = sb.ToString();
            SetButtons(GameSession.Practice ? "다시 연습" : "다시 도전 (하트 1)", Retry, "로비로", GameSession.ToLobby);
        }

        public void ShowHeroTrial(Heroes.HeroData hero)
        {
            Open();
            transform.SetAsLastSibling(); // 같은 프레임에 열린 카드·보상 방보다 결과가 앞에 오게
            titleLabel.text = "체험 끝";
            bodyLabel.text = hero != null
                ? $"{hero.displayName} · Lv1 ★1\n\n{Heroes.HeroUnlock.LockText(hero)}\n\n체험은 보상과 기록을 남기지 않아요"
                : "체험은 보상과 기록을 남기지 않아요";
            SetButtons("영웅 보러 가기", GameSession.ToLobby, "로비", GameSession.ToLobby);
        }

        public void ShowSurvival(Survival.SurvivalSummary s)
        {
            Open();
            titleLabel.text = s.Victory ? "생존!" : "쓰러졌다…";

            var sb = new StringBuilder();
            sb.AppendLine($"버틴 시간  {Clock(s.Seconds)}" + (s.NewBest ? "  <color=#FFD966>최고 기록!</color>" : $"  (최고 {Clock(s.BestSeconds)})"));
            sb.AppendLine($"처치  {s.Kills}");
            sb.AppendLine($"레벨  {s.Level}");
            if (!string.IsNullOrEmpty(s.TopDamage)) sb.AppendLine($"<size=85%>피해  {s.TopDamage}</size>");
            sb.AppendLine();
            sb.AppendLine("만든 조합");
            sb.AppendLine(s.Fusions != null && s.Fusions.Count > 0 ? "  " + string.Join(", ", s.Fusions) : "  없음");
            sb.AppendLine("  서바이벌은 기록을 남기지 않아요. 조합 발견은 도감에 남아요");
            AppendRewards(sb, s.RewardLines);
            bodyLabel.text = sb.ToString();
            SetButtons($"다시 도전 (하트 {GameSession.HeartCost})", Retry, "로비로", GameSession.ToLobby);
        }

        private static void AppendRewards(StringBuilder sb, System.Collections.Generic.List<string> lines)
        {
            if (lines == null || lines.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine("<color=#FFD966>받은 것</color>");
            foreach (string line in lines) sb.AppendLine("  " + line);
        }

        /// <summary>무한의 균열·일일 도전 결과 (기록 저장 없음): 제목 + 본문 + 버튼 2개</summary>
        public void ShowText(string title, string body, string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            Open();
            titleLabel.text = title;
            bodyLabel.text = body;
            SetButtons(primary, onPrimary ?? Retry, secondary, onSecondary ?? GameSession.ToLobby);
        }

        private void Retry()
        {
            if (GameSession.TryRetry()) return;
            primaryLabel.text = "하트가 부족해요";
            primaryButton.interactable = false;
        }

        private void Open()
        {
            GamePause.Pause(this);
            root.SetActive(true);
        }

        private void SetButtons(string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            primaryLabel.text = primary;
            _primary = onPrimary;
            secondaryButton.gameObject.SetActive(onSecondary != null);
            if (onSecondary != null) secondaryLabel.text = secondary;
            _secondary = onSecondary;
        }

        private static string Clock(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int sec = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{sec:00}";
        }
    }
}
