using System.Collections.Generic;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;

namespace HistoryHeroes.Progress
{
    /// <summary>
    /// 튜토리얼 (UX 4장 "첫 10분"): 오프닝 → 튜토리얼 던전 방 6개 (칭기즈 칸) → 기록 저장 → 튜토리얼 보스 (꼭 이기게) → 해금.
    /// 튜토리얼에서는 절대 지지 않는다 (쓰러질 만큼 맞으면 체력이 돌아온다), 적 피해는 약하게, 경험치는 두 배.
    /// 별·구간 해금·보스 최초 격파 보상은 주지 않는다 (진짜 1구간은 따로 깬다). 튜토리얼 판은 "처음 3판 하트 없음"에 세지 않는다.
    /// </summary>
    public static class Tutorial
    {
        /// <summary>튜토리얼 판 시드 (모두 같은 튜토리얼)</summary>
        public const int Seed = 20261026;
        public const string Hero = "khan";

        public const float EnemyHp = 0.8f;
        public const float EnemyAttack = 0.35f;
        public const float BossHp = 0.35f;
        public const float BossAttack = 0.3f;
        public const float ExpMultiplier = 2f;

        /// <summary>쓰러질 만큼 맞으면 이만큼 체력으로 (지지 않게)</summary>
        public const float SaveHp = 0.5f;

        /// <summary>처음 들어온 사람 (튜토리얼을 안 했고 진행한 것도 없다)</summary>
        public static bool Needed(SaveData s) => !s.tutorialDone && s.records.Count == 0 && s.unlockedChapter <= 1 && s.stars.Count == 0 && s.dungeonRuns == 0;

        /// <summary>
        /// 방 6개: ① 빈 방 (이동) ② 궁병 1 (멈추면 쏜다) ③ 돌진병 2 (예고선 → 피하기) ④⑤ 섞어서 (레벨업·조합) ⑥ 엘리트 돌진병 (마지막)
        /// </summary>
        public static List<RoomPlan> Plan()
        {
            return new List<RoomPlan>
            {
                new RoomPlan { kind = RoomKind.Normal, budget = 0, minTypes = 1, maxTypes = 1 },
                new RoomPlan { kind = RoomKind.Normal, budget = 2, minTypes = 1, maxTypes = 1, only = "archer" },
                new RoomPlan { kind = RoomKind.Normal, budget = 4, minTypes = 1, maxTypes = 1, only = "charger" },
                new RoomPlan { kind = RoomKind.Normal, budget = 6, minTypes = 2, maxTypes = 2 },
                new RoomPlan { kind = RoomKind.Normal, budget = 8, minTypes = 2, maxTypes = 2 },
                new RoomPlan { kind = RoomKind.Elite, budget = 2, minTypes = 1, maxTypes = 1, elites = 1, only = "charger" },
            };
        }

        /// <summary>튜토리얼 카드: 회전 칼날 · 방어력을 먼저 보여 준다 → 회전 칼날 MAX + 방어력 = 강철 회오리 (조합 튜토리얼)</summary>
        public static readonly string[] CardOrder = { "spin_blade", "def_up", "atk_up", "aspd_up", "hp_up" };

        /// <summary>튜토리얼 보스를 깼다: 출석부·신규 7일 미션·일일 미션·사냥매(펫)가 열리고, 첫 고급 상자 1번 무료 (🎫1)</summary>
        public static List<string> Complete(SaveData s)
        {
            var lines = new List<string>();
            if (s.tutorialDone) return lines;
            s.tutorialDone = true;
            var reward = new Reward { Gold = 500, Tickets = 1 };
            Wallet.Grant(s, reward);
            lines.Add("튜토리얼 끝!  " + reward + " (상점에서 고급 상자 1번 무료)");
            lines.Add("열림: 출석부 · 신규 7일 미션 · 일일 미션 · 펫 칸");
            return lines;
        }
    }
}
