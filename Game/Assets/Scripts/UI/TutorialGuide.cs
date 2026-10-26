using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Modes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 튜토리얼 안내 말풍선 (UX 4장, 안내자 클리오 — Story 2장). 튜토리얼 판에서만 켜진다.
    /// 방마다 한 줄 (이동 → 멈추면 쏜다 → 예고선 → 카드 → 조합 → 마지막 방), 처음 카드·조합 카드·기록 저장 화면에서 한 번씩, 보스 앞에서 한 줄.
    /// 7초 뒤 저절로 사라진다 (누르면 바로).
    /// </summary>
    public sealed class TutorialGuide : MonoBehaviour
    {
        [SerializeField] private GameObject banner;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button bannerButton;
        [SerializeField] private DungeonRun dungeon;
        [SerializeField] private BossRun boss;
        [SerializeField] private LevelUpScreen levelUp;
        [SerializeField] private RecordSaveScreen recordSave;

        private const float ShowSeconds = 12f;
        private float _hideAt;
        private bool _saidCards, _saidFusion, _saidRecord, _saidBoss;

        private static readonly string[] RoomLines =
        {
            "반가워요, 기록 수호자님! 화면 아래쪽을 누른 채 끌면 움직여요. 위쪽 문으로 가 볼까요?",
            "<b>멈추면 자동으로 쏴요!</b> 적이 보이면 손을 떼고 기다려 보세요.",
            "돌진병이 <color=#FF7A6B>빨간 선</color>을 그리면 곧 달려와요. 옆으로 비켰다가 멈춰서 쏘세요!",
            "레벨이 오를 때마다 카드 1장을 골라요. <b>회전 칼날</b>을 계속 키워 보세요!",
            "회전 칼날을 MAX로 키우고 방어력이 있으면 <color=#FFD966>★ 조합 카드</color>가 나와요!",
            "마지막 방! 크고 단단한 <b>엘리트</b>예요. 끝내면 이번 판을 기록서에 남겨요.",
        };

        private void Start()
        {
            banner.SetActive(false);
            if (bannerButton != null) bannerButton.onClick.AddListener(() => banner.SetActive(false));
            if (!GameSession.Tutorial)
            {
                enabled = false;
                return;
            }

            if (dungeon != null && dungeon.isActiveAndEnabled)
            {
                dungeon.RoomEntered += OnRoomEntered;
                if (dungeon.Room >= 1) OnRoomEntered(dungeon.Room); // 던전이 먼저 시작해서 1번 방 알림을 놓쳤으면
            }
            if (boss != null && boss.isActiveAndEnabled && !_saidBoss)
            {
                _saidBoss = true;
                Say("균열을 지키는 수문장이에요. 크게 휘두를 땐 빈틈이 생겨요! 방금 남긴 기록으로 싸워요.");
            }
        }

        private void OnDestroy()
        {
            if (dungeon != null) dungeon.RoomEntered -= OnRoomEntered;
        }

        private void OnRoomEntered(int room)
        {
            // 1번 방(이동 안내)은 방을 나갈 때까지 띄워 둔다
            if (room >= 1 && room <= RoomLines.Length) Say(RoomLines[room - 1], room == 1 ? 9999f : ShowSeconds);
        }

        private void Update()
        {
            if (levelUp != null && levelUp.IsOpen)
            {
                if (levelUp.ShowsFusion && !_saidFusion)
                {
                    _saidFusion = true;
                    Say("<color=#FFD966>★ 조합!</color> 두 스킬이 합쳐져 더 강한 스킬이 돼요. 골라 보세요!");
                }
                else if (!_saidCards)
                {
                    _saidCards = true;
                    Say("레벨 업! 카드 1장을 골라요. 회전 칼날을 추천해요.");
                }
            }

            if (recordSave != null && recordSave.IsOpen && !_saidRecord)
            {
                _saidRecord = true;
                Say("기록서에 남길 스킬을 골라요. 용량이 넘치면 뺄 스킬을 눌러 맞춘 뒤 저장!");
            }

            if (banner.activeSelf && Time.unscaledTime > _hideAt) banner.SetActive(false);
        }

        private void Say(string text, float seconds = ShowSeconds)
        {
            label.text = $"<color=#FFD966>클리오</color>  {text}";
            banner.SetActive(true);
            _hideAt = Time.unscaledTime + seconds;
        }
    }
}
