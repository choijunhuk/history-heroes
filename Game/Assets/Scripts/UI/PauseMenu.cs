using HistoryHeroes.Common;
using HistoryHeroes.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 전투 일시정지 (v1.0 A1~A3): HUD 버튼 또는 앱이 뒤로 갈 때 → 계속 / 설정 / 판 포기.
    /// 판 포기는 한 번 더 눌러야 한다 (3초 안). 포기 = 그 자리에서 쓰러진 것과 같다 (모드마다 원래 쓰러짐 처리를 탄다).
    /// 레벨업·보상 방처럼 이미 멈춘 화면이 떠 있으면 앱이 뒤로 가도 이 메뉴를 열지 않는다 (그 화면이 그대로 기다린다).
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private const float ConfirmWindow = 3f;

        [SerializeField] private GameObject root;
        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button giveUpButton;
        [SerializeField] private TMP_Text giveUpLabel;
        [SerializeField] private SettingsPanel settings;
        [SerializeField] private PlayerController player;

        private float _confirmUntil = -1f;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            if (openButton != null) openButton.onClick.AddListener(Open);
            resumeButton.onClick.AddListener(Close);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => settings?.Open());
            giveUpButton.onClick.AddListener(GiveUp);
        }

        /// <summary>다른 화면(레벨업 카드·보상 방·결과)이 멈춰 두었으면 열지 않는다 (포기하면 그 화면이 결과 뒤에 남는다)</summary>
        private bool CanOpen => player != null && !player.Health.IsDead && !IsOpen && !GamePause.IsPaused;

        public void Open()
        {
            if (!CanOpen) return;
            root.SetActive(true);
            GamePause.Pause(this);
            _confirmUntil = -1f;
            giveUpLabel.text = "판 포기";
            infoLabel.text = GiveUpText();
            // 튜토리얼은 쓰러지지 않아서(체력이 돌아온다) 포기가 아무 일도 안 했다 → 단추를 숨긴다
            giveUpButton.gameObject.SetActive(!GameSession.Tutorial);
            SetGuideVisible(false);
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (settings != null && settings.IsOpen) settings.Close();
            root.SetActive(false);
            GamePause.Resume(this);
            SetGuideVisible(true);
        }

        /// <summary>튜토리얼 안내 띠는 일시정지 창이 떠 있는 동안만 숨긴다 (제목과 겹치지 않게). 레벨업 카드 때는 그대로 보인다</summary>
        private static void SetGuideVisible(bool visible)
        {
            TutorialGuide guide = FindAnyObjectByType<TutorialGuide>();
            if (guide == null) return;
            CanvasGroup group = guide.GetComponent<CanvasGroup>();
            if (group == null) group = guide.gameObject.AddComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
        }

        private void GiveUp()
        {
            if (Time.unscaledTime > _confirmUntil)
            {
                _confirmUntil = Time.unscaledTime + ConfirmWindow;
                giveUpLabel.text = "정말 포기? 한 번 더";
                return;
            }

            Close();
            player.Health.Kill();
        }

        private void Update()
        {
            if (IsOpen && _confirmUntil > 0f && Time.unscaledTime > _confirmUntil)
            {
                _confirmUntil = -1f;
                giveUpLabel.text = "판 포기";
            }

            if (openButton != null) openButton.gameObject.SetActive(player != null && !player.Health.IsDead && (IsOpen || !GamePause.IsPaused));
        }

        /// <summary>앱이 뒤로 가면 멈춘다 (전화·알림·홈 버튼). 이미 다른 화면이 멈춰 두었으면 그대로</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused && !GamePause.IsPaused) Open();
        }

        /// <summary>포기하면 어떻게 되나 (모드마다)</summary>
        private static string GiveUpText()
        {
            if (GameSession.Tutorial) return "튜토리얼은 지지 않아요. 끝까지 가 봐요!";
            if (GameSession.IsHeroTrial) return "포기하면 체험이 끝나요 (보상·기록 없음)";
            switch (GameSession.Mode)
            {
                case BattleMode.Boss: return GameSession.Practice ? "포기하면 연습이 끝나요" : "포기하면 패배로 끝나요 (하트는 돌아오지 않아요)";
                case BattleMode.Survival: return "포기하면 지금까지 버틴 시간으로 끝나요";
                case BattleMode.Rift: return "포기하면 여기서 끝나요 (체크포인트는 남아요)";
                case BattleMode.Daily: return GameSession.DailyOfficial ? "포기하면 지금 점수가 오늘의 공식 기록이 돼요" : "포기하면 연습이 끝나요";
                default: return "포기하면 쓰러진 것과 같아요 (지금까지의 빌드는 기록으로 남길 수 있어요)";
            }
        }

        private void OnDisable() => GamePause.Resume(this);
    }
}
