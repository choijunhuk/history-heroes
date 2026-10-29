using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HistoryHeroes.Skills;

namespace HistoryHeroes.Records
{
    /// <summary>기록을 화면에 보여줄 글자 (보관함, 보스 시작 전 화면, 결과 화면)</summary>
    public static class RecordFormat
    {
        /// <summary>
        /// 예: "1번 기록 · 칭기즈 칸 연사형 · 클리어 · 10/26 05:30".
        /// database를 주면 강점으로 빌드 유형을 붙인다 (조사 A8 자동 이름, 저장하지 않고 볼 때 계산).
        /// </summary>
        public static string Title(RunRecord record, SkillDatabase database = null)
        {
            if (record == null) return "빈 칸";
            string progress = record.cleared ? "클리어" : $"방 {record.roomReached}/{record.roomCount}";
            string type = string.Empty;
            if (database != null)
            {
                SkillRole main = RecordStrength.Of(record, database).Main;
                type = $"<color={RecordStrength.RoleColor(main)}>{RecordStrength.TypeName(main)}</color>";
            }

            string heroAndType = $"{record.heroName} {type}".Trim();
            string hero = string.IsNullOrEmpty(heroAndType) ? string.Empty : $" · {heroAndType}";
            string name = record.slot < 0 ? "임시 기록" : $"{record.slot + 1}번 기록";
            return $"{name}{hero} · {progress} · {When(record.createdAt)}";
        }

        public static string When(string isoUtc)
        {
            if (string.IsNullOrEmpty(isoUtc)) return "";
            if (!DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime utc)) return "";
            return utc.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>스킬 한 줄씩. 액티브(조합 포함) 먼저, 패시브 나중.</summary>
        public static List<string> SkillLines(IEnumerable<RecordSkill> skills, SkillDatabase database)
        {
            var actives = new List<string>();
            var passives = new List<string>();
            foreach (RecordSkill s in skills)
            {
                SkillData data = database != null ? database.Find(s.id) : null;
                string name = data != null ? data.displayName : s.id;
                string line = name;

                if (s.fusion)
                {
                    FusionRecipe recipe = database != null ? database.RecipeProducing(data) : null;
                    bool evolution = recipe != null && recipe.type == FusionType.Evolution;
                    line = evolution ? $"{name} (진화)" : $"{name} Lv{s.level} (합체)";
                }
                else
                {
                    line = $"{name} Lv{s.level}";
                }

                if (data != null && data.Kind == SkillKind.Passive) passives.Add(line);
                else actives.Add(line);
            }

            actives.AddRange(passives);
            return actives;
        }

        /// <summary>보관함 칸에 쓰는 여러 줄 설명</summary>
        /// <param name="maxSkills">스킬 줄을 이만큼만 (레벨 높은 순) + "외 N개". 보관함 카드처럼 좁은 곳용 (전체 UI 검토 9번)</param>
        public static string Details(RunRecord record, SkillDatabase database, int maxSkills = int.MaxValue)
        {
            if (record == null) return "비어 있어요";
            RecordValidator.Result checkedBuild = RecordValidator.Validate(record, database);
            var sb = new StringBuilder();
            var shown = new List<RecordSkill>(checkedBuild.Skills);
            int hidden = 0;
            if (shown.Count > maxSkills)
            {
                shown.Sort((a, b) => b.level.CompareTo(a.level));
                hidden = shown.Count - maxSkills;
                shown.RemoveRange(maxSkills, hidden);
            }

            List<string> lines = SkillLines(shown, database);
            if (hidden > 0) lines.Add($"외 {hidden}개");
            sb.AppendLine(RecordStrength.Of(checkedBuild.Skills, database).Bar()); // 강점 막대 (조사 A8)
            sb.AppendLine(lines.Count > 0 ? string.Join(" · ", lines) : "스킬 없음 (기본 공격만)");
            sb.Append($"비용 {checkedBuild.Cost} / {record.capacity}pt");
            if (checkedBuild.OverCapacity) sb.Append("  (패치로 비용이 올라 다시 골라야 해요)");
            for (int i = 0; i < checkedBuild.Notes.Count && i < 2; i++) sb.Append($"\n<color=#FFB347>바뀐 점: {checkedBuild.Notes[i]}</color>");
            if (checkedBuild.Notes.Count > 2) sb.Append($"<color=#FFB347> 외 {checkedBuild.Notes.Count - 2}개</color>");
            if (record.bossesDefeated.Count > 0) sb.Append($"  · 보스 {record.bossesDefeated.Count}마리 격파");
            return sb.ToString();
        }
    }
}
