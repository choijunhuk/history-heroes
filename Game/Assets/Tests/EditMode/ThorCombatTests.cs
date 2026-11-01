using System.Collections.Generic;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using HistoryHeroes.Story;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class ThorCombatTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        private T Make<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject value in _created) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void ThorHasWarriorStatsAndOneStationaryAttackSignature()
        {
            HeroData hero = Make<HeroData>();
            ThorDefinition.ConfigureHero(hero);
            Assert.AreEqual("thor", hero.id);
            Assert.AreEqual(HeroClass.Warrior, hero.heroClass);
            Assert.AreEqual(BasicAttackKind.MeleeArc, hero.attackKind);
            Assert.That(hero.maxHp, Is.InRange(1200f, 1400f));
            Assert.That(hero.attack, Is.InRange(105f, 130f));
            Assert.That(hero.attackSpeed, Is.InRange(0.8f, 0.9f));
            Assert.That(hero.range, Is.InRange(2f, 2.2f));
            Assert.AreEqual(3, hero.thunderEvery);
            Assert.AreEqual("천둥의 무게", hero.signatureName);
            Assert.IsTrue(hero.godShardUnlock);
        }

        [Test]
        public void ThirdStationaryAttackTriggersOnceAndMovementResetsCount()
        {
            var state = new ThorThunderState();
            Assert.IsFalse(state.RecordAttack(3));
            Assert.IsFalse(state.RecordAttack(3));
            Assert.AreEqual(2, state.Count);
            state.Moved();
            Assert.AreEqual(0, state.Count);
            Assert.IsFalse(state.RecordAttack(3));
            Assert.IsFalse(state.RecordAttack(3));
            Assert.IsTrue(state.RecordAttack(3));
            Assert.AreEqual(0, state.Count);
            Assert.IsFalse(state.RecordAttack(3), "한 번의 기본 공격에 추가 베기가 생겨도 다시 세지 않는다");
        }

        [Test]
        public void StarUpgradesRemainInsideHumanSignatureBudget()
        {
            HeroData hero = Make<HeroData>();
            ThorDefinition.ConfigureHero(hero);
            HeroSignature one = HeroSignature.For(hero, 1);
            HeroSignature three = HeroSignature.For(hero, 3);
            HeroSignature five = HeroSignature.For(hero, 5);
            Assert.AreEqual(1.15f, one.ThunderRadius, 0.0001f);
            Assert.AreEqual(1.4f, three.ThunderRadius, 0.0001f);
            Assert.AreEqual(0.4f, three.ThunderCoefficient, 0.0001f);
            Assert.AreEqual(0.5f, five.ThunderCoefficient, 0.0001f);
            StringAssert.Contains("★3", HeroSignature.StarText(hero));
            StringAssert.Contains("★5", HeroSignature.StarText(hero));
        }

        [Test]
        public void ExclusiveHammerAndEvolutionFollowHeroOnlyRecipeRules()
        {
            HeroData hero = Make<HeroData>();
            ThorDefinition.ConfigureHero(hero);
            ActiveSkillData hammer = Make<ActiveSkillData>();
            ThorDefinition.ConfigureHammer(hammer);
            ActiveSkillData evolved = Make<ActiveSkillData>();
            ThorDefinition.ConfigureReturn(evolved);
            PassiveSkillData speed = Make<PassiveSkillData>();
            speed.id = "aspd_up";
            FusionRecipe recipe = Make<FusionRecipe>();
            ThorDefinition.ConfigureRecipe(recipe, hammer, speed, evolved);

            Assert.AreEqual("thor_hammer", hammer.id);
            Assert.AreEqual("thor_return", evolved.id);
            Assert.AreEqual("thor", hammer.heroOnlyId);
            Assert.AreEqual("thor", evolved.heroOnlyId);
            Assert.AreEqual(Rarity.Rare, hammer.rarity);
            Assert.AreEqual(8f, hammer.cooldown);
            Assert.AreEqual(5, hammer.maxLevel);
            Assert.AreEqual(3, hammer.saveCostFirstLevel);
            Assert.AreEqual(3, hammer.saveCostPerLevel);
            Assert.AreEqual(1, evolved.maxLevel);
            Assert.AreEqual(10, evolved.saveCostFirstLevel);
            Assert.AreEqual(0, evolved.saveCostPerLevel);
            Assert.AreEqual(0.75f, evolved.returnImpactCoefficient, 0.0001f);
            Assert.AreEqual(0.8f, evolved.RadiusAt(1), 0.0001f);
            Assert.AreEqual(FusionType.Evolution, recipe.type);
            Assert.AreSame(hammer, recipe.materialA);
            Assert.AreEqual(5, recipe.requiredLevelA);
            Assert.AreSame(speed, recipe.materialB);
            Assert.AreEqual(1, recipe.requiredLevelB);
            Assert.AreSame(evolved, recipe.result);

            var candidates = SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { hammer }, hero.id);
            Assert.AreEqual(1, candidates.Count);
            Assert.IsEmpty(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { hammer }, "guan"));
            var loadout = new SkillLoadout();
            loadout.AddNew(hammer, 5);
            loadout.AddNew(speed);
            CollectionAssert.Contains(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, hero.id), recipe);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "guan"));
        }

        [Test]
        public void ThorHasOwnRespectfulLineForEveryStoryMoment()
        {
            foreach (LineMoment moment in System.Enum.GetValues(typeof(LineMoment)))
                Assert.IsNotEmpty(StoryLines.For("thor", moment), moment.ToString());
        }
    }
}
