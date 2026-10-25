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
    }
}
