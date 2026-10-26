using System.Collections.Generic;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.Records
{
    /// <summary>
    /// 기록의 강점 (조사 A8, GameDesign 7-4): 스킬 역할(단일·광역·방어)별 **저장 비용 합**.
    /// 가장 큰 역할이 그 기록의 강점이고, 이름("칭기즈 칸 · 연사형")과 보스 추천 배지에 쓴다.
    /// 저장하지 않고 볼 때마다 계산한다 (패치로 비용·역할이 바뀌어도 맞게).
    /// </summary>
    public readonly struct RecordStrength
    {
        public readonly int Single;
        public readonly int Area;
        public readonly int Defense;

        public RecordStrength(int single, int area, int defense)
        {
            Single = single;
            Area = area;
            Defense = defense;
        }

        public int Total => Single + Area + Defense;

        /// <summary>가장 큰 역할. 다 0이면 None. 같으면 단일 → 광역 → 방어 순.</summary>
        public SkillRole Main
        {
            get
            {
                if (Total <= 0) return SkillRole.None;
                if (Single >= Area && Single >= Defense) return SkillRole.Single;
                return Area >= Defense ? SkillRole.Area : SkillRole.Defense;
            }
        }

        public float ShareOf(SkillRole role)
        {
            if (Total <= 0) return 0f;
            switch (role)
            {
                case SkillRole.Single: return (float)Single / Total;
                case SkillRole.Area: return (float)Area / Total;
                case SkillRole.Defense: return (float)Defense / Total;
                default: return 0f;
            }
        }

        /// <summary>검사를 거친 스킬 목록으로 계산한다 (RecordValidator.Result.Skills)</summary>
        public static RecordStrength Of(IEnumerable<RecordSkill> skills, SkillDatabase database)
        {
            int single = 0, area = 0, defense = 0;
            if (skills == null || database == null) return default;
            foreach (RecordSkill s in skills)
            {
                SkillData data = database.Find(s.id);
                if (data == null) continue;
                int cost = data.SaveCostAt(Mathf.Max(1, s.level));
                switch (data.role)
                {
                    case SkillRole.Single: single += cost; break;
                    case SkillRole.Area: area += cost; break;
                    case SkillRole.Defense: defense += cost; break;
                }
            }

            return new RecordStrength(single, area, defense);
        }

        public static RecordStrength Of(RunRecord record, SkillDatabase database)
        {
            return record == null || database == null ? default : Of(RecordValidator.Validate(record, database).Skills, database);
        }

        // ---------------- 글자 ----------------

        public const string SingleColor = "#FFB35C";
        public const string AreaColor = "#7FC8FF";
        public const string DefenseColor = "#8CE08C";

        public static string RoleName(SkillRole role)
        {
            switch (role)
            {
                case SkillRole.Single: return "단일";
                case SkillRole.Area: return "광역";
                case SkillRole.Defense: return "방어";
                default: return "없음";
            }
        }

        /// <summary>기록 이름 뒤에 붙는 빌드 유형 (GameDesign 6-8 이름 그대로)</summary>
        public static string TypeName(SkillRole role)
        {
            switch (role)
            {
                case SkillRole.Single: return "연사형";
                case SkillRole.Area: return "광역형";
                case SkillRole.Defense: return "탱커형";
                default: return "기본형";
            }
        }

        public static string RoleColor(SkillRole role)
        {
            switch (role)
            {
                case SkillRole.Single: return SingleColor;
                case SkillRole.Area: return AreaColor;
                case SkillRole.Defense: return DefenseColor;
                default: return "#C8CED6";
            }
        }

        /// <summary>
        /// "■■■■■■■■■■ 단일 60% · 광역 30% · 방어 10%" — 칸 10개를 역할 색으로 나누고, 색만으로 구분하지 않게 글자도 쓴다.
        /// </summary>
        public string Bar(int cells = 10)
        {
            if (Total <= 0) return "<color=#A9B4C2>강점 없음 (기본 공격만)</color>";

            int s = Mathf.RoundToInt(ShareOf(SkillRole.Single) * cells);
            int a = Mathf.RoundToInt(ShareOf(SkillRole.Area) * cells);
            if (s + a > cells) a = cells - s;
            int d = cells - s - a;
            if (Defense == 0 && d > 0) // 반올림으로 빈 역할에 칸이 생기지 않게
            {
                if (Area > 0) a += d;
                else s += d;
                d = 0;
            }

            var labels = new List<string>();
            foreach (SkillRole role in new[] { SkillRole.Single, SkillRole.Area, SkillRole.Defense })
            {
                float share = ShareOf(role);
                if (share > 0f) labels.Add($"<color={RoleColor(role)}>{RoleName(role)} {Mathf.RoundToInt(share * 100f)}%</color>");
            }

            return $"<color={SingleColor}>{new string('■', s)}</color><color={AreaColor}>{new string('■', a)}</color><color={DefenseColor}>{new string('■', d)}</color>  {string.Join(" · ", labels)}";
        }
    }
}
