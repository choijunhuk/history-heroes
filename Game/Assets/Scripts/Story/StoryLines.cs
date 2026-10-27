using System.Collections.Generic;

namespace HistoryHeroes.Story
{
    /// <summary>영웅 대사가 나오는 상황 (Story 3장)</summary>
    public enum LineMoment
    {
        RunStart,
        FirstLevelUp,
        Fusion,
        Legendary,
        LowHp,
        Death,
        BossDefeat,
    }

    /// <summary>
    /// 대사 (Story 2·3장). 영웅마다 상황별 한 줄 · 구간별 보스 앞 대사 · 로비에서 영웅끼리 짧은 대화.
    /// 국가·민족을 적으로 만들지 않는다 (적은 전부 망각의 군단).
    /// </summary>
    public static class StoryLines
    {
        private static readonly Dictionary<string, string[]> Hero = new Dictionary<string, string[]>
        {
            // 판 시작, 첫 레벨업, 조합 완성, 전설 획득, 체력 낮음, 사망, 보스 격파
            ["khan"] = new[] { "초원은 넓다. 달려라!", "더 멀리, 더 빠르게.", "천 개의 화살이 하늘을 덮는다!", "하늘이 나를 돕는구나.", "아직 말에서 떨어지지 않았다!", "초원으로… 돌아가겠다.", "또 하나의 땅을 되찾았다!" },
            ["guan"] = new[] { "의리로 길을 열겠다.", "칼날이 가벼워졌군.", "다섯 관문, 단숨에!", "청룡이 울부짖는다!", "이 정도로는 쓰러지지 않는다.", "의리를 지키지 못했구나.", "의리의 승리다." },
            ["napoleon"] = new[] { "전진! 포병은 준비됐나?", "전술을 바꾼다!", "아우스터리츠의 태양이다!", "운도 실력이다!", "후퇴는… 전략적 재배치다!", "워털루… 또인가.", "불가능은 없다!" },
            ["tesla"] = new[] { "자, 실험을 시작하죠.", "흥미로운 데이터군요.", "무선 송전, 드디어 성공이야!", "에디슨이 봤어야 하는데!", "전압이 떨어지고 있어…", "실험 실패. 다시 하죠.", "역시 교류가 이긴다!" },
            ["yisunsin"] = new[] { "죽고자 하면 살 것이다.", "포를 장전하라!", "학의 날개를 펼쳐라!", "이 배는 쉽게 가라앉지 않는다.", "아직 열두 척이 남았다!", "나의 죽음을… 알리지 마라.", "바다는 오늘도 지켜졌다." },
        };

        /// <summary>영웅 대사 (없으면 null)</summary>
        public static string For(string heroId, LineMoment moment)
        {
            if (heroId == null || !Hero.TryGetValue(heroId, out string[] lines)) return null;
            int i = (int)moment;
            return i >= 0 && i < lines.Length ? lines[i] : null;
        }

        /// <summary>보스 앞 대사 (구간 1~5): (누가, 대사) 줄들</summary>
        public static IReadOnlyList<(string speaker, string line)> BossIntro(int chapter)
        {
            switch (chapter)
            {
                case 1: return new[] { ("클리오", "균열을 지키는 수문장이에요. 크게 휘두를 땐 빈틈이 생겨요!") };
                case 2: return new[] { ("망령 군주", "이름 없는 자들아, 일어나라."), ("클리오", "졸개부터 정리해요! 졸개가 많을수록 군주가 단단해져요.") };
                case 3: return new[] { ("니콜라 테슬라", "이 거인, 설계가 엉망이군요."), ("클리오", "탄막이 쏟아져요. 피하면서 버텨요!") };
                case 4: return new[] { ("그림자 쌍둥이", "우리는 너의 잊힌 반쪽."), ("클리오", "진한 쪽만 맞아요. 한 번에 몰아쳐요!") };
                case 5: return new[] { ("망각의 왕", "역사는 결국 잊힌다. 너희도."), ("영웅들", "그러니까 기록하는 거다.") };
                default: return new (string, string)[0];
            }
        }

        /// <summary>로비 영웅끼리 대화 (두 영웅이 다 해금됐을 때만): (영웅 A, 대사 A, 영웅 B, 대사 B)</summary>
        public static readonly (string a, string lineA, string b, string lineB)[] Chats =
        {
            ("napoleon", "키가 작다고? 포탄은 높이 난다!", "khan", "내 말은 더 높이 뛴다."),
            ("tesla", "관우 장군, 그 칼은 전기를 잘 통하나요?", "guan", "…시험해 보겠나?"),
            ("khan", "당신 나라는 얼마나 넓었소?", "napoleon", "넓었지. 잠깐은."),
            ("napoleon", "포병이라면 나도 일가견이 있소.", "yisunsin", "바다 위에서도 맞힐 수 있소?"),
            ("yisunsin", "멈춰 서는 것도 용기요.", "guan", "물러서지 않는 것과 같은 말이군."),
        };
    }
}
