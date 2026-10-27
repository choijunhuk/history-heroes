using System.Collections.Generic;
using HistoryHeroes.Enemy;
using HistoryHeroes.Stats;

namespace HistoryHeroes.Dungeon
{
    /// <summary>특수 방 종류 (확장 1차 3장)</summary>
    public enum RoomSpecial
    {
        None,
        /// <summary>도전 방 "20초 버티기": 무리 3번 (0·7·14초), 20초가 되면 남은 적은 연기로 사라진다</summary>
        ChallengeSurvive,
        /// <summary>도전 방 "맞지 않고 깨기": 적은 평소의 70%, 첫 피격에 실패</summary>
        ChallengeNoHit,
        /// <summary>저주받은 엘리트: 속성 2개, 잡으면 희귀 이상 보장 카드 1번 더</summary>
        Cursed,
        /// <summary>기억의 석비: 보상 방 대신, 축복 2개 + 그냥 지나가기</summary>
        Stele,
    }

    /// <summary>한 판의 특수 방 계획. 판 시작 때 "갈림길 난수"(RunRandom.Forks)로 미리 다 정한다</summary>
    public sealed class SpecialPlan
    {
        public sealed class Fork
        {
            /// <summary>이 방을 깨면 문이 2개</summary>
            public int AfterRoom;
            /// <summary>고른 문으로 종류가 정해지는 방 (AfterRoom + 1)</summary>
            public int Room;
            /// <summary>오른쪽 문 (특별)</summary>
            public RoomSpecial Alternative;
            /// <summary>-1 = 아직 · 0 = 왼쪽(평범) · 1 = 오른쪽(특별)</summary>
            public int Choice = -1;
        }

        public readonly List<Fork> Forks = new List<Fork>();
        /// <summary>석비가 된 보상 방 → 보여 줄 축복 2개 (Blessings id)</summary>
        public readonly Dictionary<int, string[]> Steles = new Dictionary<int, string[]>();
        /// <summary>저주받은 엘리트의 속성 2개</summary>
        public EliteAffix CursedAffixes;
        /// <summary>"20초 버티기"의 무리 3번</summary>
        public readonly List<List<SpawnEntry>> SurviveWaves = new List<List<SpawnEntry>>();
        /// <summary>"맞지 않고 깨기"의 적 (평소의 70%)</summary>
        public List<SpawnEntry> NoHitEncounter = new List<SpawnEntry>();

        public RoomSpecial SpecialOf(int room)
        {
            if (Steles.ContainsKey(room)) return RoomSpecial.Stele;
            foreach (Fork f in Forks)
            {
                if (f.Room == room) return f.Choice == 1 ? f.Alternative : RoomSpecial.None;
            }

            return RoomSpecial.None;
        }

        public Fork ForkAfter(int room) => Forks.Find(f => f.AfterRoom == room);

        public List<int> Choices()
        {
            var list = new List<int>(Forks.Count);
            foreach (Fork f in Forks) list.Add(f.Choice);
            return list;
        }

        /// <summary>이어하기: 저장해 둔 선택. 옛 저장(선택 없음)인데 이미 지나간 갈림길은 평범한 쪽으로 본다</summary>
        public void RestoreChoices(IReadOnlyList<int> saved, int resumeRoom)
        {
            for (int i = 0; i < Forks.Count; i++)
            {
                int choice = saved != null && i < saved.Count ? saved[i] : -1;
                if (choice < 0 && resumeRoom > Forks[i].AfterRoom) choice = 0;
                Forks[i].Choice = choice < 0 ? -1 : choice > 0 ? 1 : 0;
            }
        }
    }

    /// <summary>
    /// 특수 방 규칙 (확장 1차 3장 + 검토 9장 1·2·8번): 15방 던전에만. 일일 도전·균열·튜토리얼·직접 짠 판은 그대로.
    /// 양쪽 방 종류·적 편성·도전 종류·석비·축복·저주 속성을 판 시작 때 갈림길 난수로 한꺼번에 정한다 → 어느 문을 골라도 뒤 방·카드 난수는 같다.
    /// </summary>
    public static class SpecialRooms
    {
        public const int RoomCount = 15;
        public const float SteleChance = 0.25f;
        public const float SurviveSeconds = 20f;
        public static readonly float[] WaveTimes = { 0f, 7f, 14f };
        /// <summary>무리 한 번 = 그 방 예산의 60% (세 번이면 평소보다 많지만 다 잡을 필요는 없다)</summary>
        public const float WaveBudget = 0.6f;
        public const float NoHitBudget = 0.7f;
        /// <summary>도전 성공 골드 = 보상 방 골드의 2배</summary>
        public static int ChallengeGold(int chapter) => 2 * RewardGold(chapter);
        public static int RewardGold(int chapter) => 50 + 25 * System.Math.Max(1, chapter);
        /// <summary>석비 "그냥 지나가기" 회복</summary>
        public const float PassHeal = 0.1f;

        /// <summary>갈림길 자리: 3번 뒤 (4번 = 일반/도전), 9번 뒤 (10번 = 엘리트/저주받은 엘리트). 석비 후보 = 6·11번 보상 방</summary>
        private static readonly (int after, RoomKind kind, RoomSpecial alt)[] ForkSlots =
        {
            (3, RoomKind.Normal, RoomSpecial.ChallengeSurvive),
            (9, RoomKind.Elite, RoomSpecial.Cursed),
        };

        private static readonly int[] SteleSlots = { 6, 11 };

        public static bool Eligible(IReadOnlyList<RoomPlan> plans, bool daily, bool rift, bool tutorial, bool custom)
        {
            if (daily || rift || tutorial || custom || plans == null || plans.Count != RoomCount) return false;
            foreach ((int after, RoomKind kind, RoomSpecial _) in ForkSlots)
            {
                if (plans[after].kind != kind) return false; // plans[after] = 방 after+1
            }

            return true;
        }

        /// <param name="chapter">적 편성용 (없으면 빈 편성, 테스트)</param>
        public static SpecialPlan Plan(IReadOnlyList<RoomPlan> plans, ChapterData chapter, System.Random forks)
        {
            var plan = new SpecialPlan();
            // 난수 순서는 결과와 관계없이 항상 같게 (모두 뽑는다)
            bool survive = forks.NextDouble() < 0.5;
            foreach ((int after, RoomKind _, RoomSpecial alt) in ForkSlots)
            {
                RoomSpecial alternative = alt == RoomSpecial.ChallengeSurvive && !survive ? RoomSpecial.ChallengeNoHit : alt;
                plan.Forks.Add(new SpecialPlan.Fork { AfterRoom = after, Room = after + 1, Alternative = alternative });
            }

            foreach (int room in SteleSlots)
            {
                bool stele = forks.NextDouble() < SteleChance;
                string[] offer = Blessings.PickTwo(forks);
                if (stele && room <= plans.Count && plans[room - 1].kind == RoomKind.Reward) plan.Steles[room] = offer;
            }

            plan.CursedAffixes = EliteAffixes.Roll(forks, 2);

            RoomPlan challenge = plans[ForkSlots[0].after];
            for (int w = 0; w < WaveTimes.Length; w++) plan.SurviveWaves.Add(Encounter.Build(chapter, Scaled(challenge, WaveBudget), ForkSlots[0].after + 1, forks));
            plan.NoHitEncounter = Encounter.Build(chapter, Scaled(challenge, NoHitBudget), ForkSlots[0].after + 1, forks);
            return plan;
        }

        private static RoomPlan Scaled(RoomPlan source, float scale)
        {
            RoomPlan copy = source.Copy();
            copy.kind = RoomKind.Normal;
            copy.elites = 0;
            copy.budget = System.Math.Max(2, (int)System.Math.Round(source.budget * scale));
            return copy;
        }

        public static string Title(RoomSpecial special)
        {
            switch (special)
            {
                case RoomSpecial.ChallengeSurvive: return "도전: 20초 버티기";
                case RoomSpecial.ChallengeNoHit: return "도전: 맞지 않고 깨기";
                case RoomSpecial.Cursed: return "저주받은 엘리트";
                case RoomSpecial.Stele: return "기억의 석비";
                default: return string.Empty;
            }
        }
    }

    /// <summary>
    /// 기억의 석비 축복과 대가 (확장 1차 3-5장, 이번 판 안에서만 — 기록에는 남지 않는다).
    /// "통찰(다음 카드 4장)"은 카드 화면이 3장 틀이라 "지혜(바로 레벨업)"로 바꿨다 (Expansion1 3-5장 메모)
    /// </summary>
    public static class Blessings
    {
        public readonly struct Info
        {
            public readonly string Id, Name, Good, Bad, Icon;

            public Info(string id, string name, string good, string bad, string icon)
            {
                Id = id;
                Name = name;
                Good = good;
                Bad = bad;
                Icon = icon;
            }
        }

        public static readonly Info[] All =
        {
            new Info("fight", "투지", "공격력 +20%", "받는 피해 +15%", "atk_up"),
            new Info("haste", "서두름", "쿨다운 -15%", "최대 체력 -15%", "cdr"),
            new Info("steel", "강철", "최대 체력 +25%", "이동 속도 -10%", "hp_up"),
            new Info("wisdom", "지혜", "바로 레벨업 1번", "받는 피해 +10%", "level_up"),
        };

        public static int IndexOf(string id) => System.Array.FindIndex(All, b => b.Id == id);

        /// <summary>서로 다른 축복 2개 (난수를 항상 2번 쓴다)</summary>
        public static string[] PickTwo(System.Random random)
        {
            int a = random.Next(All.Length);
            int b = random.Next(All.Length - 1);
            if (b >= a) b++;
            return new[] { All[a].Id, All[b].Id };
        }

        /// <summary>석비 버튼 글: 이름 + 좋은 점 / 대가</summary>
        public static string Text(string id)
        {
            int i = IndexOf(id);
            if (i < 0) return id;
            Info b = All[i];
            return $"{b.Name}\n<size=70%><color=#9CFFB0>{b.Good}</color>  <color=#FF9C9C>{b.Bad}</color></size>";
        }

        /// <summary>능력치 보정 (출처 = Blessing). "지혜"의 바로 레벨업은 고를 때만 (DungeonRun)</summary>
        public static void Apply(string id, StatSheet stats, object owner)
        {
            void Mod(StatType stat, ModifierKind kind, float value) =>
                stats.Add(new StatModifier(stat, kind, value, ModifierSource.Blessing, owner, StatTarget.Both));

            switch (id)
            {
                case "fight":
                    Mod(StatType.Attack, ModifierKind.Percent, 0.2f);
                    Mod(StatType.DamageTaken, ModifierKind.Flat, 0.15f);
                    break;
                case "haste":
                    Mod(StatType.CooldownReduction, ModifierKind.Flat, 0.15f);
                    Mod(StatType.MaxHp, ModifierKind.Percent, -0.15f);
                    break;
                case "steel":
                    Mod(StatType.MaxHp, ModifierKind.Percent, 0.25f);
                    Mod(StatType.MoveSpeed, ModifierKind.Percent, -0.1f);
                    break;
                case "wisdom":
                    Mod(StatType.DamageTaken, ModifierKind.Flat, 0.1f);
                    break;
            }
        }
    }
}
