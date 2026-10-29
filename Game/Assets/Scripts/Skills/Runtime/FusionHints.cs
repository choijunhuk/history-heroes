using System;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 발견한 조합 힌트 (조사 A3·A4, GameDesign 6-2 레시피 공개).
    /// - 카드: "◆ 강철 회오리 ← 방어력 · 회전 칼날 4/5" (완성에 가장 가까운 조합 1개 + "외 n")
    /// - 조합 카드: "※ 고르면 이번 판엔 막혀요 / 불꽃 발자국 · 뇌신의 분노" (재료가 사라져 못 만들게 되는 조합)
    /// 발견하지 않은 레시피는 절대 말하지 않는다 (스포일러 방지). 화면과 떨어진 계산만 둔다 (테스트용).
    /// </summary>
    public static class FusionHints
    {
        public struct Progress
        {
            public FusionRecipe Recipe;
            public SkillData Self;
            public SkillData Partner;
            public int SelfLevel;      // 이 카드를 고른 뒤 레벨
            public int SelfNeed;
            public int PartnerLevel;   // 지금 가진 레벨 (없으면 0)
            public int PartnerNeed;

            /// <summary>0~1, 두 재료 진행의 평균</summary>
            public float Completion => (Ratio(SelfLevel, SelfNeed) + Ratio(PartnerLevel, PartnerNeed)) * 0.5f;

            private static float Ratio(int level, int need) => need <= 0 ? 1f : Mathf.Clamp01((float)level / need);
        }

        /// <summary>이번 판에 아직 만들 수 있는 조합인가: 영웅 전용, 이미 만듦, 숨김, 결과 보유, 재료 소모를 거른다</summary>
        public static bool StillPossible(FusionRecipe r, SkillLoadout loadout, string heroId)
        {
            if (r == null || r.materialA == null || r.materialB == null || r.result == null) return false;
            if (!string.IsNullOrEmpty(r.heroOnlyId) && r.heroOnlyId != heroId) return false;
            if (loadout.IsCompleted(r) || loadout.IsHidden(r) || loadout.Find(r.result) != null) return false;
            if (loadout.IsConsumed(r.materialA) || loadout.IsConsumed(r.materialB)) return false;
            if (loadout.IsBanned(r.materialA) || loadout.IsBanned(r.materialB)) return false;
            return true;
        }

        /// <summary>
        /// 없는 재료를 이번 판에 얻을 수 있는가: 그 종류의 빈 칸이 재료 수만큼 있어야 한다 (패시브는 진화에서도 안 사라져 칸이 안 빈다).
        /// 새 스킬 카드도 아직 없는 재료로 세므로, 그 카드가 쓸 칸까지 같이 본다.
        /// </summary>
        public static bool Obtainable(FusionRecipe r, SkillLoadout loadout)
        {
            int needActive = 0, needPassive = 0;
            foreach (SkillData m in new[] { r.materialA, r.materialB })
            {
                if (m == null || loadout.Find(m) != null) continue;
                if (m.Kind == SkillKind.Passive) needPassive++;
                else needActive++;
            }

            int freeActive = loadout.ActiveSlots - loadout.Actives.Count;
            int freePassive = loadout.PassiveSlots - loadout.Passives.Count;
            return needActive <= freeActive && needPassive <= freePassive;
        }

        /// <summary>
        /// skill을 nextLevel로 올리면 가장 완성에 가까워지는 발견한 조합. 없으면 false.
        /// others = 같은 스킬이 들어가는 다른 발견한 조합 수 ("외 n").
        /// </summary>
        public static bool TryNearest(SkillData skill, int nextLevel, SkillLoadout loadout, IEnumerable<FusionRecipe> recipes,
            string heroId, Func<FusionRecipe, bool> isKnown, out Progress best, out int others)
        {
            best = default;
            others = 0;
            if (skill == null || loadout == null || recipes == null) return false;

            int found = 0;
            float bestScore = -1f;
            foreach (FusionRecipe r in recipes)
            {
                if (r == null || !isKnown(r) || !StillPossible(r, loadout, heroId) || !Obtainable(r, loadout)) continue;
                bool isA = r.materialA == skill;
                if (!isA && r.materialB != skill) continue;

                SkillData partner = isA ? r.materialB : r.materialA;
                var p = new Progress
                {
                    Recipe = r,
                    Self = skill,
                    Partner = partner,
                    SelfLevel = nextLevel,
                    SelfNeed = isA ? r.requiredLevelA : r.requiredLevelB,
                    PartnerLevel = loadout.LevelOf(partner),
                    PartnerNeed = isA ? r.requiredLevelB : r.requiredLevelA,
                };

                // 조합 결과(연쇄 금지)는 재료가 못 되니 그 짝은 진행 0으로 본다
                SkillInstance owned = loadout.Find(partner);
                if (owned != null && owned.IsFusionResult) p.PartnerLevel = 0;

                found++;
                if (p.Completion > bestScore)
                {
                    bestScore = p.Completion;
                    best = p;
                }
            }

            others = Mathf.Max(0, found - 1);
            return found > 0;
        }

        /// <summary>
        /// "◆ 강철 회오리 ← 방어력 보유 · 회전 칼날 4/5 · 외 1" (TMP 리치 텍스트).
        /// 고르면 조건이 다 채워지면 "◆ 고르면 다음 레벨업에 뇌화 폭풍!"
        /// </summary>
        public static string CardLine(Progress p, int others)
        {
            string more = others > 0 ? $" · 외 {others}" : string.Empty;
            if (p.Completion >= 1f) return $"◆ 고르면 다음 레벨업에 <color={Ok}>{p.Recipe.result.displayName}</color>!{more}";
            return $"◆ {p.Recipe.result.displayName} ← {Part(p.Partner, p.PartnerLevel, p.PartnerNeed)} · {Part(p.Self, p.SelfLevel, p.SelfNeed)}{more}";
        }

        private const string Ok = "#7CE38B";
        private const string Dim = "#A9B4C2";

        /// <summary>재료 하나: 조건을 채웠으면 초록, 없으면 흐리게 "필요". 보유만 하면 되는 패시브는 "보유"</summary>
        private static string Part(SkillData skill, int level, int need)
        {
            bool ownOnly = need <= 1 && skill.Kind == SkillKind.Passive;
            if (level >= need) return $"<color={Ok}>{skill.displayName} {(ownOnly ? "보유" : $"{need}/{need}")}</color>";
            if (level <= 0) return $"<color={Dim}>{skill.displayName} 필요</color>";
            return $"{skill.displayName} {level}/{need}";
        }

        /// <summary>
        /// chosen을 만들면 사라지는 재료 때문에 이번 판에 못 만들게 되는 발견한 조합들 (A4).
        /// 진화는 액티브(A)가 사라지고, 합체는 A·B 둘 다 사라진다.
        /// </summary>
        public static List<(SkillData lost, FusionRecipe blocked)> BlockedBy(FusionRecipe chosen, SkillLoadout loadout,
            IEnumerable<FusionRecipe> recipes, string heroId, Func<FusionRecipe, bool> isKnown)
        {
            var list = new List<(SkillData, FusionRecipe)>();
            if (chosen == null || loadout == null || recipes == null) return list;

            foreach (FusionRecipe r in recipes)
            {
                if (r == null || r == chosen || !isKnown(r) || !StillPossible(r, loadout, heroId) || !Obtainable(r, loadout)) continue;
                if (r.result == chosen.result) continue; // 같은 결과는 어차피 한 판에 1개

                SkillData lost = Uses(r, chosen.materialA) ? chosen.materialA
                    : chosen.type == FusionType.Merge && Uses(r, chosen.materialB) ? chosen.materialB
                    : null;
                if (lost != null) list.Add((lost, r));
            }

            return list;
        }

        /// <summary>
        /// "※ 고르면 이번 판엔 막혀요\n불꽃 발자국 · 뇌신의 분노". 사라지는 재료는 카드 설명("낙뢰 + 불꽃 장판")에 이미 있다.
        /// </summary>
        public static string BlockedLine(List<(SkillData lost, FusionRecipe blocked)> blocked)
        {
            if (blocked == null || blocked.Count == 0) return string.Empty;
            var names = new List<string>();
            foreach ((SkillData _, FusionRecipe r) in blocked)
            {
                if (!names.Contains(r.result.displayName)) names.Add(r.result.displayName);
            }

            return $"<color=#FFB86B>※ 고르면 이번 판엔 막혀요</color>\n<color=#FFD9A8>{string.Join(" · ", names)}</color>";
        }

        private static bool Uses(FusionRecipe r, SkillData skill) => skill != null && (r.materialA == skill || r.materialB == skill);

        /// <summary>도감용: 이 스킬이 재료인 조합 수와 그중 발견한 수 (A3, 미발견 조합의 결과·짝은 숨긴다)</summary>
        public static (int total, int known) CountFor(SkillData skill, IEnumerable<FusionRecipe> recipes, Func<FusionRecipe, bool> isKnown)
        {
            int total = 0, known = 0;
            if (skill == null || recipes == null) return (0, 0);
            foreach (FusionRecipe r in recipes)
            {
                if (r == null || !Uses(r, skill)) continue;
                total++;
                if (isKnown(r)) known++;
            }

            return (total, known);
        }
    }
}
