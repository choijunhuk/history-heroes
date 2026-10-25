using System.Collections.Generic;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class SkillSystemTests
    {
        private static T Make<T>(string id, int maxLevel = 5, string heroOnly = null) where T : SkillData
        {
            var s = ScriptableObject.CreateInstance<T>();
            s.id = id;
            s.maxLevel = maxLevel;
            s.heroOnlyId = heroOnly;
            return s;
        }

        [Test]
        public void MergeStartLevel_UsesExcessLevels()
        {
            var recipe = ScriptableObject.CreateInstance<FusionRecipe>();
            Assert.AreEqual(1, recipe.MergeStartLevel(3, 3));
            Assert.AreEqual(1, recipe.MergeStartLevel(4, 3));
            Assert.AreEqual(2, recipe.MergeStartLevel(4, 4));
            Assert.AreEqual(3, recipe.MergeStartLevel(5, 5));
        }

        [Test]
        public void Loadout_RespectsSlotLimits()
        {
            var loadout = new SkillLoadout(activeSlots: 1, passiveSlots: 1);
            loadout.AddNew(Make<ActiveSkillData>("a"));
            Assert.IsFalse(loadout.HasFreeSlot(SkillKind.Active));
            Assert.IsTrue(loadout.HasFreeSlot(SkillKind.Passive));
        }

        [Test]
        public void Candidates_SkipFullSlotsMaxLevelConsumedAndOtherHeroes()
        {
            var owned = Make<ActiveSkillData>("owned", maxLevel: 2);
            var newActive = Make<ActiveSkillData>("newActive");
            var consumed = Make<PassiveSkillData>("consumed");
            var otherHero = Make<PassiveSkillData>("other", heroOnly: "napoleon");
            var passive = Make<PassiveSkillData>("passive");

            var loadout = new SkillLoadout(activeSlots: 1, passiveSlots: 4);
            loadout.AddNew(owned, 1);
            loadout.MarkConsumed(consumed);

            var cards = SkillPicker.BuildCandidates(loadout, new SkillData[] { owned, newActive, consumed, otherHero, passive }, "khan");

            Assert.AreEqual(2, cards.Count, "owned 레벨업 + passive 신규만");
            Assert.IsTrue(cards.Exists(c => c.Skill == owned && c.Type == CardType.LevelUp && c.NextLevel == 2));
            Assert.IsTrue(cards.Exists(c => c.Skill == passive && c.Type == CardType.NewSkill));

            loadout.Find(owned).Level = 2;
            cards = SkillPicker.BuildCandidates(loadout, new SkillData[] { owned }, "khan");
            Assert.AreEqual(0, cards.Count, "최대 레벨이면 레벨업 카드 없음");
        }

        [Test]
        public void Candidates_IncludeFusionResultLevelUps()
        {
            var merged = Make<ActiveSkillData>("merged", maxLevel: 3);
            var loadout = new SkillLoadout();
            loadout.AddNew(merged, 1).IsFusionResult = true;

            var cards = SkillPicker.BuildCandidates(loadout, new SkillData[0], "khan");
            Assert.AreEqual(1, cards.Count);
            Assert.AreEqual(CardType.LevelUp, cards[0].Type);
        }

        [Test]
        public void PickSkills_NeverReturnsDuplicatesAndPadsWithFallback()
        {
            var a = Make<PassiveSkillData>("a");
            var b = Make<PassiveSkillData>("b");
            var candidates = SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { a, b }, "khan");
            var table = ScriptableObject.CreateInstance<RarityTable>();

            for (int i = 0; i < 50; i++)
            {
                var picked = SkillPicker.PickSkills(candidates, 3, 1, table);
                Assert.AreEqual(3, picked.Count);
                Assert.AreNotSame(picked[0].Skill, picked[1].Skill);
                Assert.AreEqual(CardType.Fallback, picked[2].Type);
            }
        }

        private static RarityTable Table(float common, float rare, float legendary)
        {
            var t = ScriptableObject.CreateInstance<RarityTable>();
            t.common = AnimationCurve.Constant(0f, 100f, common);
            t.rare = AnimationCurve.Constant(0f, 100f, rare);
            t.legendary = AnimationCurve.Constant(0f, 100f, legendary);
            return t;
        }

        private static List<SkillCard> CandidatesOf(params SkillData[] skills)
        {
            return SkillPicker.BuildCandidates(new SkillLoadout(), skills, "khan");
        }

        [Test]
        public void PickSkills_NeverUpgradesToLegendary()
        {
            var legend = Make<ActiveSkillData>("arrow_rain");
            legend.rarity = Rarity.Legendary;
            var picked = SkillPicker.PickSkills(CandidatesOf(legend), 3, 1, Table(common: 100, rare: 0, legendary: 0));
            Assert.IsTrue(picked.TrueForAll(c => c.Type == CardType.Fallback), "일반·희귀가 비어도 전설로 올라가지 않는다");
        }

        [Test]
        public void PickSkills_LegendaryRollFallsDownToCommon()
        {
            var common = Make<PassiveSkillData>("atk_up");
            var picked = SkillPicker.PickSkills(CandidatesOf(common), 1, 1, Table(common: 0, rare: 0, legendary: 100));
            Assert.AreSame(common, picked[0].Skill);
        }

        [Test]
        public void PickSkills_EmptyCommonGoesUpToRare()
        {
            var rare = Make<PassiveSkillData>("lifesteal");
            rare.rarity = Rarity.Rare;
            var picked = SkillPicker.PickSkills(CandidatesOf(rare), 1, 1, Table(common: 100, rare: 0, legendary: 0));
            Assert.AreSame(rare, picked[0].Skill);
        }

        [Test]
        public void PickSkills_RollsRarityByWeight()
        {
            var common = Make<PassiveSkillData>("c");
            var rare = Make<PassiveSkillData>("r");
            rare.rarity = Rarity.Rare;
            var table = Table(common: 0, rare: 100, legendary: 0);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreSame(rare, SkillPicker.PickSkills(CandidatesOf(common, rare), 1, 1, table)[0].Skill);
            }
        }

        private static FusionRecipe Recipe(FusionType type, ActiveSkillData a, int levelA, SkillData b, int levelB, string heroOnly = null)
        {
            var r = ScriptableObject.CreateInstance<FusionRecipe>();
            r.id = "recipe";
            r.type = type;
            r.materialA = a;
            r.requiredLevelA = levelA;
            r.materialB = b;
            r.requiredLevelB = levelB;
            r.result = Make<ActiveSkillData>("result");
            r.heroOnlyId = heroOnly;
            return r;
        }

        [Test]
        public void Fusions_EvolutionNeedsMaxLevelActiveAndPassive()
        {
            var blade = Make<ActiveSkillData>("spin_blade");
            var def = Make<PassiveSkillData>("def_up");
            var recipe = Recipe(FusionType.Evolution, blade, 5, def, 1);
            var loadout = new SkillLoadout();
            loadout.AddNew(blade, 4);
            loadout.AddNew(def, 1);

            Assert.AreEqual(0, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan").Count, "Lv4면 아직");
            loadout.Find(blade).Level = 5;
            Assert.AreEqual(1, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan").Count);
        }

        [Test]
        public void Fusions_MergeNeedsBothLevel3()
        {
            var lightning = Make<ActiveSkillData>("lightning");
            var flame = Make<ActiveSkillData>("flame_field");
            var recipe = Recipe(FusionType.Merge, lightning, 3, flame, 3);
            var loadout = new SkillLoadout();
            loadout.AddNew(lightning, 3);
            loadout.AddNew(flame, 2);

            Assert.AreEqual(0, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan").Count);
            loadout.Find(flame).Level = 3;
            Assert.AreEqual(1, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan").Count);
        }

        [Test]
        public void Fusions_ExcludeHiddenCompletedChainedAndOtherHero()
        {
            var a = Make<ActiveSkillData>("a");
            var b = Make<ActiveSkillData>("b");
            var loadout = new SkillLoadout();
            loadout.AddNew(a, 3);
            loadout.AddNew(b, 3);

            var hidden = Recipe(FusionType.Merge, a, 3, b, 3);
            loadout.Hide(hidden);
            var completed = Recipe(FusionType.Merge, a, 3, b, 3);
            loadout.MarkCompleted(completed);
            var otherHero = Recipe(FusionType.Merge, a, 3, b, 3, heroOnly: "napoleon");
            Assert.AreEqual(0, FusionFinder.FindAvailableFusions(loadout, new[] { hidden, completed, otherHero }, "khan").Count);

            loadout.Find(a).IsFusionResult = true; // 조합 결과는 다시 재료가 되지 않는다
            var chained = Recipe(FusionType.Merge, a, 3, b, 3);
            Assert.AreEqual(0, FusionFinder.FindAvailableFusions(loadout, new[] { chained }, "khan").Count);
        }

        [Test]
        public void PlayerLevel_LevelsUpRepeatedlyFromOneBigGain()
        {
            var level = new PlayerLevel(baseRequirement: 1f);
            int events = 0;
            level.LeveledUp += _ => events++;

            // Lv1 필요 1, Lv2 필요 2^1.3 ≈ 2.46 → 3.5면 Lv3
            level.Add(3.5f);
            Assert.AreEqual(3, level.Level);
            Assert.AreEqual(2, events);
            Assert.Less(level.Experience, level.Required);
        }

        [Test]
        public void SaveCost_FirstLevelPlusPerLevel()
        {
            var merged = Make<ActiveSkillData>("thunder_fire", maxLevel: 3);
            merged.saveCostFirstLevel = 15;
            merged.saveCostPerLevel = 3;
            Assert.AreEqual(15, merged.SaveCostAt(1));
            Assert.AreEqual(21, merged.SaveCostAt(3));
        }

        [Test]
        public void PlayerLevel_AppliesExpGainBonus()
        {
            var level = new PlayerLevel(baseRequirement: 10f);
            level.Add(5f, expGainBonus: 0.5f);
            Assert.AreEqual(7.5f, level.Experience, 0.001f);
        }

        // ---------------- 리롤: 방금 본 카드 빼기 (조사 A1) ----------------

        [Test]
        public void Reroll_ExcludesShownCards_KeepsOthers()
        {
            var a = Make<PassiveSkillData>("a");
            var b = Make<PassiveSkillData>("b");
            var c = Make<PassiveSkillData>("c");
            var d = Make<PassiveSkillData>("d");
            var candidates = new List<SkillCard>
            {
                new SkillCard { Type = CardType.NewSkill, Skill = a }, new SkillCard { Type = CardType.NewSkill, Skill = b },
                new SkillCard { Type = CardType.NewSkill, Skill = c }, new SkillCard { Type = CardType.NewSkill, Skill = d },
            };
            var shown = new List<SkillCard> { new SkillCard { Type = CardType.NewSkill, Skill = a }, new SkillCard { Type = CardType.LevelUp, Skill = b }, SkillCard.Fallback() };

            SkillPicker.ExcludeShown(candidates, shown);

            Assert.AreEqual(2, candidates.Count);
            Assert.IsFalse(candidates.Exists(x => x.Skill == a || x.Skill == b));
        }

        [Test]
        public void Reroll_FewCandidates_StillFillsThreeCards()
        {
            var a = Make<PassiveSkillData>("a");
            var candidates = new List<SkillCard> { new SkillCard { Type = CardType.NewSkill, Skill = a } };
            SkillPicker.ExcludeShown(candidates, new List<SkillCard> { new SkillCard { Type = CardType.NewSkill, Skill = a } });

            List<SkillCard> picked = SkillPicker.PickSkills(candidates, 3, 1, null);

            Assert.AreEqual(3, picked.Count, "후보가 없어도 기본 카드로 채운다");
            Assert.IsTrue(picked.TrueForAll(x => x.Type == CardType.Fallback));
        }

        [Test]
        public void Reroll_FusionCardsAreNotExcluded()
        {
            var a = Make<PassiveSkillData>("a");
            var candidates = new List<SkillCard> { new SkillCard { Type = CardType.NewSkill, Skill = a } };
            var fusion = new SkillCard { Type = CardType.Fusion, Skill = a };
            SkillPicker.ExcludeShown(candidates, new List<SkillCard> { fusion });
            Assert.AreEqual(1, candidates.Count, "조합 카드는 고정이므로 빼기 대상이 아니다");
        }
    }
}
