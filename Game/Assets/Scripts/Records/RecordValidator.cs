using System.Collections.Generic;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.Records
{
    /// <summary>
    /// 기록을 쓰기 전에 지금 게임 데이터 기준으로 검사한다 (GameDesign 7-5 패치 정책).
    /// - 게임에서 없어진 스킬은 빼고 알린다
    /// - 레벨은 지금 최대 레벨 안으로
    /// - 진화의 필수 패시브가 게임에서 없어졌으면 진화 전 액티브 최대 레벨로 (그 스킬이 재료인 조합도 다시 검사)
    /// - 레시피만 바뀐 조합 결과는 그대로 둔다 (이미 만든 건 빼앗지 않는다)
    /// - 칸 수를 넘으면 뒤쪽부터 뺀다
    /// - 패치로 비용이 올라 저장 당시 용량을 넘으면 OverCapacity → 다음 사용 때 다시 고르게 한다
    /// </summary>
    public static class RecordValidator
    {
        public sealed class Result
        {
            public readonly List<RecordSkill> Skills = new List<RecordSkill>();
            public readonly List<string> Notes = new List<string>();
            public int Cost;
            public bool OverCapacity;
            public bool IsEmpty => Skills.Count == 0;
        }

        public static Result Validate(RunRecord record, SkillDatabase database, int activeSlots = 4, int passiveSlots = 4)
        {
            var result = new Result();
            if (record == null || database == null || record.skills == null) return result;

            var seen = new HashSet<string>();
            int actives = 0;
            int passives = 0;

            foreach (RecordSkill saved in record.skills)
            {
                if (saved == null || string.IsNullOrEmpty(saved.id) || saved.level <= 0) continue;

                SkillData data = database.Find(saved.id);
                if (data == null)
                {
                    result.Notes.Add($"지금 게임에 없는 스킬이라 뺐어요 ({saved.id})");
                    continue;
                }

                int level = Mathf.Clamp(saved.level, 1, data.maxLevel);
                if (level != saved.level) result.Notes.Add($"{data.displayName}: 최대 레벨이 바뀌어 Lv{level}로 맞췄어요");
                bool fusion = saved.fusion;

                if (fusion)
                {
                    FusionRecipe recipe = database.RecipeProducing(data);
                    bool materialGone = recipe != null && recipe.type == FusionType.Evolution && recipe.materialA != null && recipe.materialB == null;
                    if (materialGone)
                    {
                        result.Notes.Add($"{data.displayName}: 진화 재료가 없어져서 {recipe.materialA.displayName} Lv{recipe.materialA.maxLevel}로 돌아갔어요");
                        data = recipe.materialA;
                        level = data.maxLevel;
                        fusion = false;
                    }
                }

                if (!seen.Add(data.id))
                {
                    result.Notes.Add($"같은 스킬이 두 번 있어서 하나만 남겼어요 ({data.displayName})");
                    continue;
                }

                bool passive = data.Kind == SkillKind.Passive;
                if (passive ? passives >= passiveSlots : actives >= activeSlots)
                {
                    result.Notes.Add($"{(passive ? "패시브" : "액티브")} 칸이 모자라서 뺐어요 ({data.displayName})");
                    continue;
                }

                if (passive) passives++;
                else actives++;

                result.Skills.Add(new RecordSkill(data.id, level, fusion));
                result.Cost += data.SaveCostAt(level);
            }

            result.OverCapacity = record.capacity > 0 && result.Cost > record.capacity;
            return result;
        }
    }
}
