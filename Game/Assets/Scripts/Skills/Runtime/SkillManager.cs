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

        public string HeroId => player.Hero != null ? player.Hero.id : string.Empty;
        public Heroes.HeroClass HeroClass => player.Hero != null ? player.Hero.heroClass : Heroes.HeroClass.Archer;

        /// <summary>첫 레벨업에 영웅 전용 액티브가 나올 확률을 조금 높인다 (Skills.md 3장: 영웅 개성을 빨리 느끼게)</summary>
        [SerializeField, Range(0f, 1f)] private float firstOfferExclusiveChance = 0.5f;
        private int _offersMade;

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
            if (_offersMade++ == 0) BoostExclusive(offer, candidates);
            return offer;
        }

        /// <summary>첫 카드 묶음에 전용 액티브가 없으면 확률적으로 마지막 일반 카드와 바꾼다</summary>
        private void BoostExclusive(List<SkillCard> offer, List<SkillCard> candidates)
        {
            SkillData exclusive = player.Hero != null ? player.Hero.exclusiveActive : null;
            if (exclusive == null || offer.Exists(c => c.Skill == exclusive)) return;
            SkillCard card = candidates.Find(c => c.Skill == exclusive);
            if (card == null || UnityEngine.Random.value > firstOfferExclusiveChance) return;

            for (int i = offer.Count - 1; i >= 0; i--)
            {
                if (offer[i].Type == CardType.Fusion) continue;
                offer[i] = card;
                return;
            }
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

        /// <summary>
        /// 개발·테스트용: 스킬을 그 레벨로 바로 맞춘다 (없으면 새로 얻음, 칸이 없으면 무시).
        /// 조합 결과도 넣을 수 있다. 빌드 3종 비교(GameDesign 6-8)를 빨리 하려고 쓴다.
        /// </summary>
        public bool DevSetLevel(SkillData skill, int level, bool isFusionResult = false)
        {
            if (skill == null) return false;
            level = Mathf.Clamp(level, 1, skill.maxLevel);

            SkillInstance owned = Loadout.Find(skill);
            if (owned == null)
            {
                if (!Loadout.HasFreeSlot(skill.Kind)) return false;
                owned = Loadout.AddNew(skill, level);
                owned.IsFusionResult = isFusionResult;
                Install(owned);
                return true;
            }

            owned.Level = level;
            Refresh(owned);
            return true;
        }

        /// <summary>
        /// 가진 스킬을 모두 없애고 새 판처럼 비운다 (효과·이벤트·소환물 정리).
        /// 기록 복원(ApplyRecord)과 빌드 비교 시나리오가 쓴다.
        /// </summary>
        public void ClearAll()
        {
            var owned = new List<SkillInstance>(Loadout.Actives);
            owned.AddRange(Loadout.Passives);
            foreach (SkillInstance instance in owned)
            {
                player.Stats.RemoveAllFrom(instance);
                player.Attack.RemoveAllFrom(instance);
                if (instance.Behaviour != null) instance.Behaviour.Dispose();
            }

            _loadout = new SkillLoadout(activeSlots, passiveSlots);
            player.Health.ClearShield(); // 수호 방패·철벽이 남긴 보호막도 같이
        }

        /// <summary>
        /// 기록의 빌드로 바꾼다 (보스맵, GameDesign 12-2 "Apply 계약"):
        /// ① 지금 상태를 다 비우고 ② 기록의 레벨 기준으로 다시 구성한다. 두 번 불러도 효과가 겹치지 않는다.
        /// 스킬 수치는 언제나 최신 데이터에서 가져온다 (7-5 패치 정책). 검사는 RecordValidator가 먼저 한다.
        /// </summary>
        public void ApplyRecord(IReadOnlyList<Records.RecordSkill> recordSkills, SkillDatabase database)
        {
            ClearAll();
            if (recordSkills == null || database == null) return;

            foreach (Records.RecordSkill saved in recordSkills)
            {
                SkillData data = database.Find(saved.id);
                if (data == null || Loadout.Find(data) != null || !Loadout.HasFreeSlot(data.Kind)) continue;

                SkillInstance instance = Loadout.AddNew(data, Mathf.Clamp(saved.level, 1, data.maxLevel));
                instance.IsFusionResult = saved.fusion;
                Install(instance);
            }
        }

        /// <summary>이번 판에 전설 스킬 카드를 골랐는가 (던전 ★3 "전설 없이 클리어", Modes 3-2)</summary>
        public bool ChoseLegendary { get; private set; }

        public void Choose(SkillCard card)
        {
            if (card.Skill != null && card.Skill.rarity == Rarity.Legendary && card.Type != CardType.Fallback) ChoseLegendary = true;

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
