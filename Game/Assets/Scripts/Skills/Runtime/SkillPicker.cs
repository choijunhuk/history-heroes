using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 같은 등급 안 후보 하나에 곱할 배율 (룬, Runes 9장). 1.0 = 영향 없음, 최대 3.0.
    /// **등급 추첨(RollRarity)은 이걸 받지 않는다** → "전설이 나올 확률은 룬이 있어도 같다"가 구조로 지켜진다.
    /// </summary>
    public interface IWeightModifier
    {
        float GetMultiplier(SkillData candidate, SkillLoadout currentLoadout);
    }

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
                if (skill == null || !skill.IsAvailableFor(heroId) || loadout.IsConsumed(skill) || loadout.IsBanned(skill)) continue;

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
                if (owned.IsFusionResult && !owned.IsMaxLevel && !loadout.IsBanned(owned.Data))
                {
                    candidates.Add(new SkillCard { Type = CardType.LevelUp, Skill = owned.Data, NextLevel = owned.Level + 1 });
                }
            }

            return candidates;
        }

        /// <summary>고른 카드 한 장만 빼고 바꾼다. 나머지 카드와 등급·룬 추첨 규칙은 그대로 둔다.</summary>
        public static bool TryBanAndReplace(SkillLoadout loadout, IEnumerable<SkillData> pool, string heroId,
            List<SkillCard> current, int index, int floor, RarityTable rarityTable, IWeightModifier modifier = null)
        {
            if (current == null || index < 0 || index >= current.Count || !loadout.TryBan(current[index])) return false;
            List<SkillCard> candidates = BuildCandidates(loadout, pool, heroId);
            ExcludeShown(candidates, current);
            current[index] = PickSkills(candidates, 1, floor, rarityTable, modifier, loadout)[0];
            return true;
        }

        /// <summary>
        /// 리롤: 방금 화면에 있던 일반 카드의 스킬은 후보에서 뺀다 (VS·Archero 2 방식, 조사 A1).
        /// 광고를 보고도 같은 카드가 또 뜨는 일이 없게. 후보가 모자라면 등급 대체 → 기본 카드로 채운다 (PickSkills).
        /// </summary>
        public static void ExcludeShown(List<SkillCard> candidates, IEnumerable<SkillCard> shown)
        {
            var seen = new HashSet<SkillData>();
            foreach (SkillCard c in shown)
            {
                if (c.Type != CardType.Fusion && c.Type != CardType.Fallback && c.Skill != null) seen.Add(c.Skill);
            }

            candidates.RemoveAll(c => c.Skill != null && seen.Contains(c.Skill));
        }

        /// <summary>
        /// 2단계: 후보에서 count장 뽑기 (GameDesign 6-4).
        ///   1. 카드 한 장마다 층별 확률로 **등급을 먼저** 뽑는다 (RarityTable).
        ///   2. 그 등급의 후보 중 하나를 무작위로 고른다. 뽑힌 카드는 후보에서 빠진다 (같은 스킬 두 장 금지).
        ///   3. 그 등급에 후보가 없으면 전설 → 희귀 → 일반으로 내려가고, 일반이 비면 희귀로 올라간다.
        ///      **전설로는 절대 올라가지 않는다.** ("전설은 귀하다")
        ///   4. 그래도 없으면 기본 카드(체력 회복)로 채운다. 기본 카드는 중복돼도 된다.
        /// </summary>
        /// <param name="modifier">룬 (없으면 같은 등급 안에서 균등). 등급 추첨에는 쓰지 않는다</param>
        public static List<SkillCard> PickSkills(List<SkillCard> candidates, int count, int floor, RarityTable rarityTable,
            IWeightModifier modifier = null, SkillLoadout loadout = null)
        {
            var pool = new List<SkillCard>(candidates);
            var picked = new List<SkillCard>(count);

            for (int i = 0; i < count; i++)
            {
                Rarity rolled = RollRarity(floor, rarityTable);
                SkillCard card = modifier != null ? TakeWeighted(pool, FallbackOrder(rolled), modifier, loadout, Common.RunRandom.Card01()) : TakeFrom(pool, FallbackOrder(rolled));
                picked.Add(card ?? SkillCard.Fallback());
            }

            return picked;
        }

        /// <summary>
        /// ②단계 (룬): order 순서대로 그 등급 후보를 찾아, 후보마다 배율(최대 3.0)을 가중치로 하나를 꺼낸다.
        /// roll01 = 0~1 난수 (테스트에서 정해 넣는다). 배율이 1보다 큰 카드는 RuneBoosted 표시.
        /// </summary>
        public static SkillCard TakeWeighted(List<SkillCard> pool, Rarity[] order, IWeightModifier modifier, SkillLoadout loadout, float roll01)
        {
            foreach (Rarity rarity in order)
            {
                float total = 0f;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i].Skill != null && pool[i].Skill.rarity == rarity) total += Weight(pool[i], modifier, loadout);
                }

                if (total <= 0f) continue;

                float target = roll01 * total;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i].Skill == null || pool[i].Skill.rarity != rarity) continue;
                    float w = Weight(pool[i], modifier, loadout);
                    if (target >= w)
                    {
                        target -= w;
                        continue;
                    }

                    SkillCard card = pool[i];
                    pool.RemoveAt(i);
                    card.RuneBoosted = w > 1f;
                    return card;
                }

                // 반올림 오차: 그 등급의 마지막 후보
                for (int i = pool.Count - 1; i >= 0; i--)
                {
                    if (pool[i].Skill == null || pool[i].Skill.rarity != rarity) continue;
                    SkillCard card = pool[i];
                    pool.RemoveAt(i);
                    card.RuneBoosted = Weight(card, modifier, loadout) > 1f;
                    return card;
                }
            }

            return null;
        }

        public const float MaxRuneMultiplier = 3f;

        private static float Weight(SkillCard card, IWeightModifier modifier, SkillLoadout loadout)
        {
            return Mathf.Clamp(modifier.GetMultiplier(card.Skill, loadout), 0f, MaxRuneMultiplier);
        }

        private static readonly Rarity[] FromLegendary = { Rarity.Legendary, Rarity.Rare, Rarity.Common };
        private static readonly Rarity[] FromRare = { Rarity.Rare, Rarity.Common };
        private static readonly Rarity[] FromCommon = { Rarity.Common, Rarity.Rare };

        /// <summary>뽑힌 등급에 후보가 없을 때 찾아볼 순서. 어떤 경우에도 전설로는 올라가지 않는다.</summary>
        public static Rarity[] FallbackOrder(Rarity rolled)
        {
            switch (rolled)
            {
                case Rarity.Legendary: return FromLegendary;
                case Rarity.Rare: return FromRare;
                default: return FromCommon;
            }
        }

        /// <summary>가중치 합 × 0~1 난수 → 누적 가중치가 그 값을 넘는 등급.</summary>
        public static Rarity RollRarity(int floor, RarityTable table)
        {
            if (table == null) return Rarity.Common;

            float legendary = table.WeightOf(Rarity.Legendary, floor);
            float rare = table.WeightOf(Rarity.Rare, floor);
            float common = table.WeightOf(Rarity.Common, floor);
            float total = legendary + rare + common;
            if (total <= 0f) return Rarity.Common;

            float roll = Common.RunRandom.Card01() * total; // 카드 난수 갈래 (일일 도전: 같은 날 = 같은 카드 순서)
            if (roll < legendary) return Rarity.Legendary;
            if (roll < legendary + rare) return Rarity.Rare;
            return Rarity.Common;
        }

        /// <summary>order 순서대로 그 등급의 후보를 찾아 하나를 무작위로 꺼낸다. 없으면 null.</summary>
        private static SkillCard TakeFrom(List<SkillCard> pool, Rarity[] order)
        {
            foreach (Rarity rarity in order)
            {
                int matches = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i].Skill != null && pool[i].Skill.rarity == rarity) matches++;
                }

                if (matches == 0) continue;

                // 그 등급 후보 중 n번째를 고른다 (목록을 새로 만들지 않고)
                int target = Common.RunRandom.CardRange(0, matches);
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i].Skill == null || pool[i].Skill.rarity != rarity) continue;
                    if (target-- > 0) continue;

                    SkillCard card = pool[i];
                    pool.RemoveAt(i);
                    return card;
                }
            }

            return null;
        }
    }
}
