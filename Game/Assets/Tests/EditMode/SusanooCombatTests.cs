using System.Collections.Generic;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class SusanooCombatTests
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
        public void WindGapChangesOnlyAfterActualSlashAndMovementStartsAgain()
        {
            var state = new SusanooWindState();
            SusanooWindState.SlashShape first = state.RecordSlash(120f, 0.7f, 70f, 1.15f);
            Assert.AreEqual(120f, first.Arc);
            Assert.AreEqual(0.7f, first.Coefficient, 0.0001f);
            Assert.AreEqual(1, state.Count);
            SusanooWindState.SlashShape second = state.RecordSlash(120f, 0.7f, 70f, 1.15f);
            Assert.AreEqual(70f, second.Arc);
            Assert.AreEqual(1.15f, second.Coefficient, 0.0001f);
            state.Moved();
            Assert.AreEqual(0, state.Count);
            Assert.AreEqual(0.7f, state.RecordSlash(120f, 0.7f, 70f, 1.15f).Coefficient, 0.0001f);
        }

        [Test]
        public void HeroAndStarsStayInWarriorRange()
        {
            HeroData hero = Make<HeroData>();
            SusanooDefinition.ConfigureHero(hero);
            Assert.AreEqual("susanoo", hero.id);
            Assert.AreEqual(HeroClass.Warrior, hero.heroClass);
            Assert.AreEqual(BasicAttackKind.MeleeArc, hero.attackKind);
            Assert.AreEqual(1200f, hero.maxHp);
            Assert.AreEqual(110f, hero.attack);
            Assert.AreEqual(2.2f, hero.range);
            Assert.IsTrue(hero.godShardUnlock);
            HeroSignature one = HeroSignature.For(hero, 1);
            HeroSignature three = HeroSignature.For(hero, 3);
            HeroSignature five = HeroSignature.For(hero, 5);
            Assert.AreEqual(0.7f, one.WindFirstCoefficient, 0.0001f);
            Assert.AreEqual(1.15f, one.WindFollowCoefficient, 0.0001f);
            Assert.AreEqual(0.85f, three.WindFirstCoefficient, 0.0001f);
            Assert.AreEqual(1.25f, five.WindFollowCoefficient, 0.0001f);
        }

        [Test]
        public void StraightWavesHaveOneDirectionAndExclusiveEvolution()
        {
            ActiveSkillData wind = Make<ActiveSkillData>();
            ActiveSkillData storm = Make<ActiveSkillData>();
            SusanooDefinition.ConfigureWindSlash(wind);
            SusanooDefinition.ConfigureStormPath(storm);
            PassiveSkillData pierce = Make<PassiveSkillData>();
            pierce.id = "pierce";
            FusionRecipe recipe = Make<FusionRecipe>();
            SusanooDefinition.ConfigureRecipe(recipe, wind, pierce, storm);

            Assert.AreEqual("wind_slash", wind.id);
            Assert.AreEqual("storm_path", storm.id);
            Assert.AreEqual(7f, wind.cooldown);
            Assert.AreEqual(0.75f, wind.DamageAt(1), 0.0001f);
            Assert.AreEqual(1.15f, wind.DamageAt(5), 0.0001f);
            Assert.AreEqual(0.35f, wind.pushDistance, 0.0001f);
            Assert.AreEqual(5.5f, wind.waveLength, 0.0001f);
            Assert.AreEqual(3, storm.CountAt(1));
            Assert.AreEqual(0.6f, storm.DamageAt(1), 0.0001f);
            Assert.AreEqual(0.22f, storm.waveInterval, 0.0001f);
            Assert.AreEqual(5, wind.maxLevel);
            Assert.AreEqual(3, wind.saveCostFirstLevel);
            Assert.AreEqual(3, wind.saveCostPerLevel);
            Assert.AreEqual(1, storm.maxLevel);
            Assert.AreEqual(10, storm.saveCostFirstLevel);
            Assert.AreEqual(0, storm.saveCostPerLevel);
            Assert.AreEqual("susanoo", wind.heroOnlyId);
            Assert.AreEqual("susanoo", storm.heroOnlyId);
            Assert.AreEqual("susanoo", recipe.heroOnlyId);
            Assert.AreEqual(FusionType.Evolution, recipe.type);
            Assert.AreSame(wind, recipe.materialA);
            Assert.AreSame(pierce, recipe.materialB);
            Assert.AreEqual(5, recipe.requiredLevelA);
            Assert.AreEqual(1, recipe.requiredLevelB);
            Assert.AreSame(storm, recipe.result);
            Assert.AreEqual(1, SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { wind }, "susanoo").Count);
            Assert.IsEmpty(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { wind }, "guan"));
            var loadout = new SkillLoadout();
            loadout.AddNew(wind, 5);
            loadout.AddNew(pierce);
            CollectionAssert.Contains(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "susanoo"), recipe);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "guan"));
        }
    }
}
