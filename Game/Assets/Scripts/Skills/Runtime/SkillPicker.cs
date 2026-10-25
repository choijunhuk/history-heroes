using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>레벨업 카드 3장 뽑기 (GameDesign 6-4).</summary>
    public static class SkillPicker
    {
        /// <summary>
        /// 1단계: 후보 목록 만들기 (구현 완료).
        /// - 새 스킬: 그 종류의 빈 칸이 있을 때만
        /// - 가진 스킬: 최대 레벨이 아닐 때만 (레벨업 카드)
        /// - 이 영웅이 쓸 수 있고, 조합 재료로 이미 쓰이지 않은 스킬만
        /// - 액티브와 패시브는 한 목록에 섞인다
        /// </summary>
        public static List<SkillCard> BuildCandidates(SkillLoadout loadout, IEnumerable<SkillData> pool, string heroId)
        {
            var candidates = new List<SkillCard>();
            foreach (SkillData skill in pool)
            {
                if (skill == null || !skill.IsAvailableFor(heroId) || loadout.IsConsumed(skill)) continue;

                SkillInstance owned = loadout.Find(skill);
                if (owned != null)
                {
                    if (!owned.IsMaxLevel)
                    {
                        candidates.Add(new SkillCard { Type = CardType.LevelUp, Skill = skill, NextLevel = owned.Level + 1 });
                    }
                }
                else if (loadout.HasFreeSlot(skill.Kind))
                {
                    candidates.Add(new SkillCard { Type = CardType.NewSkill, Skill = skill, NextLevel = 1 });
                }
            }

            // 조합 결과는 풀에 없지만 레벨업은 된다 (합체 결과 Lv2 +25%, Lv3 +50% — Skills.md 4-2)
            foreach (SkillInstance owned in loadout.Actives)
            {
                if (owned.IsFusionResult && !owned.IsMaxLevel)
                {
                    candidates.Add(new SkillCard { Type = CardType.LevelUp, Skill = owned.Data, NextLevel = owned.Level + 1 });
                }
            }

            return candidates;
        }

        /// <summary>
        /// 2단계: 후보에서 count장 뽑기.
        ///
        /// ✍️ TODO (개발자가 직접 구현할 부분, Docs/Dev/v0.1-Plan.md 11단계)
        /// 규칙 (GameDesign 6-4):
        ///   1. 카드 한 장마다 rarityTable.WeightOf(등급, floor)로 **등급을 먼저** 뽑는다.
        ///   2. 그 등급의 후보 중에서 하나를 무작위로 고른다.
        ///   3. 이미 뽑힌 카드는 다음 카드의 후보에서 뺀다 (같은 스킬 두 장 금지).
        ///   4. 뽑힌 등급에 후보가 없으면: 전설 → 희귀 → 일반 순서로 내려가고,
        ///      일반이 비었으면 희귀로 올라간다. **전설로는 절대 올라가지 않는다.**
        ///   5. 그래도 없으면 SkillCard.Fallback()으로 채운다 (중복 가능).
        /// 힌트: 가중치 합 → Random.value × 합 → 누적 합이 그 값을 넘는 등급 선택.
        ///
        /// 지금은 게임이 돌아가게 하려고 **등급을 무시하고 균등하게** 뽑는 임시 코드가 들어 있다.
        /// 위 규칙대로 바꾸면 이 임시 코드를 지운다.
        /// </summary>
        public static List<SkillCard> PickSkills(List<SkillCard> candidates, int count, int floor, RarityTable rarityTable)
        {
            // ---- 임시 코드 시작 (TODO로 바꿀 것) ----
            var pool = new List<SkillCard>(candidates);
            var picked = new List<SkillCard>(count);
            while (picked.Count < count)
            {
                if (pool.Count == 0)
                {
                    picked.Add(SkillCard.Fallback());
                    continue;
                }

                int index = Random.Range(0, pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return picked;
            // ---- 임시 코드 끝 ----
        }
    }
}
