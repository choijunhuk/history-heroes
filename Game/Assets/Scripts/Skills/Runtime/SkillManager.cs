using System;
using System.Collections.Generic;
using HistoryHeroes.Player;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 판 안 스킬 관리: 카드 만들기 → 고른 카드 적용 → 패시브 효과·액티브 동작 붙이기 → 조합 실행.
    /// </summary>
    public sealed class SkillManager : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private List<SkillData> skillPool = new List<SkillData>();
        [SerializeField] private List<FusionRecipe> recipes = new List<FusionRecipe>();
        [SerializeField] private RarityTable rarityTable;
        [SerializeField] private LayerMask enemyMask;
        [SerializeField, Min(1)] private int cardsPerLevel = 3;
        [Tooltip("기본 카드(체력 회복)가 회복하는 최대체력 비율")]
        [SerializeField, Range(0f, 1f)] private float fallbackHeal = 0.2f;

        [Header("슬롯 (모드마다 다름: 던전 4·4, 서바이벌 6·6 — GameDesign 6-1)")]
        [SerializeField, Min(1)] private int activeSlots = 4;
        [SerializeField, Min(1)] private int passiveSlots = 4;

        private SkillLoadout _loadout;
        public SkillLoadout Loadout => _loadout ??= new SkillLoadout(activeSlots, passiveSlots);

        /// <summary>조합을 처음 만들었을 때 (도감 기록·연출용)</summary>
        public event Action<FusionRecipe> FusionCompleted;

        private string HeroId => player.Hero != null ? player.Hero.id : string.Empty;

        /// <summary>레벨업 카드 만들기 (GameDesign 6-4). 조합 조건을 채웠으면 1장은 조합 카드로 확정.</summary>
        public List<SkillCard> CreateOffer(int floor)
        {
            var offer = new List<SkillCard>(cardsPerLevel);

            List<FusionRecipe> fusions = FusionFinder.FindAvailableFusions(Loadout, recipes, HeroId);
            if (fusions.Count > 0)
            {
                FusionRecipe chosen = fusions[UnityEngine.Random.Range(0, fusions.Count)];
                offer.Add(new SkillCard { Type = CardType.Fusion, Recipe = chosen });
            }

            List<SkillCard> candidates = SkillPicker.BuildCandidates(Loadout, skillPool, HeroId);
            offer.AddRange(SkillPicker.PickSkills(candidates, cardsPerLevel - offer.Count, floor, rarityTable));
            return offer;
        }

        /// <summary>리롤: 조합 카드는 고정하고 나머지만 다시 뽑는다 (GameDesign 6-7).</summary>
        public List<SkillCard> Reroll(List<SkillCard> current, int floor)
        {
            var kept = current.FindAll(c => c.Type == CardType.Fusion);
            List<SkillCard> candidates = SkillPicker.BuildCandidates(Loadout, skillPool, HeroId);
            kept.AddRange(SkillPicker.PickSkills(candidates, cardsPerLevel - kept.Count, floor, rarityTable));
            return kept;
        }

        /// <summary>
        /// 조합 카드의 "이번 판에서 숨기기" (GameDesign 6-2). 그 자리는 다른 조합이 있으면 그 조합으로,
        /// 없으면 일반 카드 1장으로 채운다. 나머지 카드는 그대로 둔다 (숨기기로 공짜 리롤이 되지 않게).
        /// </summary>
        public List<SkillCard> HideFusion(List<SkillCard> current, int floor)
        {
            var result = new List<SkillCard>(current.Count);
            foreach (SkillCard card in current)
            {
                if (card.Type == CardType.Fusion) Loadout.Hide(card.Recipe);
                else result.Add(card);
            }

            List<FusionRecipe> fusions = FusionFinder.FindAvailableFusions(Loadout, recipes, HeroId);
            if (fusions.Count > 0)
            {
                result.Insert(0, new SkillCard { Type = CardType.Fusion, Recipe = fusions[UnityEngine.Random.Range(0, fusions.Count)] });
                return result;
            }

            List<SkillCard> candidates = SkillPicker.BuildCandidates(Loadout, skillPool, HeroId);
            candidates.RemoveAll(c => result.Exists(r => r.Skill != null && r.Skill == c.Skill));
            result.AddRange(SkillPicker.PickSkills(candidates, cardsPerLevel - result.Count, floor, rarityTable));
            return result;
        }

        /// <summary>**발견한** 조합의 재료인가 (카드에 "조합 재료" 표시용, UX.md 3장 · GameDesign 6-2)</summary>
        public bool IsFusionMaterial(SkillData skill)
        {
            if (skill == null) return false;
            foreach (FusionRecipe r in recipes)
            {
                if (r == null || !RecipeBook.IsDiscovered(r)) continue;
                if (!string.IsNullOrEmpty(r.heroOnlyId) && r.heroOnlyId != HeroId) continue;
                if (r.materialA == skill || r.materialB == skill) return true;
            }

            return false;
        }

        public void Choose(SkillCard card)
        {
            switch (card.Type)
            {
                case CardType.NewSkill:
                    Install(Loadout.AddNew(card.Skill));
                    break;

                case CardType.LevelUp:
                    SkillInstance owned = Loadout.Find(card.Skill);
                    if (owned == null) break;
                    owned.Level = Mathf.Min(owned.Level + 1, owned.Data.maxLevel);
                    Refresh(owned);
                    break;

                case CardType.Fusion:
                    ExecuteFusion(card.Recipe);
                    break;

                case CardType.Fallback:
                    player.Health.Heal(player.Health.Max * fallbackHeal);
                    break;
            }
        }

        private void Install(SkillInstance instance)
        {
            if (instance.Data is ActiveSkillData active && active.behaviourPrefab != null)
            {
                ActiveSkillBehaviour behaviour = Instantiate(active.behaviourPrefab, player.transform);
                behaviour.Bind(player, active, instance.Level, enemyMask);
                instance.Behaviour = behaviour;
            }

            Refresh(instance);
        }

        /// <summary>현재 레벨 기준으로 효과를 다시 건다 (두 번 불러도 중복 없음).</summary>
        private void Refresh(SkillInstance instance)
        {
            if (instance.Data is PassiveSkillData passive) passive.Apply(player, instance, instance.Level);
            if (instance.Behaviour != null) instance.Behaviour.SetLevel(instance.Level);
        }

        private void Uninstall(SkillInstance instance)
        {
            player.Stats.RemoveAllFrom(instance);
            player.Attack.RemoveAllFrom(instance);
            if (instance.Behaviour != null) instance.Behaviour.Dispose();
            Loadout.Remove(instance);
            Loadout.MarkConsumed(instance.Data);
        }

        /// <summary>
        /// 진화: 액티브 A가 결과로 바뀐다 (패시브 B는 남음).
        /// 합체: A와 B가 하나로 합쳐지고 액티브 칸 1개가 빈다. 시작 레벨은 초과 레벨을 반영.
        /// 재료는 그 판에서 다시 안 나오고, 같은 결과는 한 판에 1개만 (GameDesign 6-2).
        /// </summary>
        private void ExecuteFusion(FusionRecipe recipe)
        {
            SkillInstance a = Loadout.Find(recipe.materialA);
            SkillInstance b = Loadout.Find(recipe.materialB);
            if (a == null || b == null || recipe.result == null) return;

            int startLevel = 1;
            if (recipe.type == FusionType.Merge)
            {
                startLevel = recipe.MergeStartLevel(a.Level, b.Level);
                Uninstall(b);
            }

            Uninstall(a);

            SkillInstance result = Loadout.AddNew(recipe.result, startLevel);
            result.IsFusionResult = true;
            Install(result);

            Loadout.MarkCompleted(recipe);
            RecipeBook.Discover(recipe);
            FusionCompleted?.Invoke(recipe);
        }
    }
}
