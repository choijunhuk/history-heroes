using System.Collections.Generic;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Enemy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using HistoryHeroes.Story;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class AnubisCombatTests
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
        public void ThreeActualHitsPrepareNextHitAndExtraDamageDoesNotCount()
        {
            var state = new AnubisMarkState();
            var target = new object();
            Assert.IsFalse(state.RecordHit(target, 1, 0f, true, 3, 6f));
            Assert.IsFalse(state.RecordHit(target, 1, 0f, false, 3, 6f), "추가 탄과 연쇄 피해는 횟수를 쌓지 않는다");
            Assert.IsFalse(state.RecordHit(target, 1, 1f, true, 3, 6f));
            Assert.IsFalse(state.RecordHit(target, 1, 2f, true, 3, 6f));
            Assert.IsTrue(state.IsReady(2f, 3));
            Assert.IsTrue(state.RecordHit(target, 1, 3f, true, 3, 6f));
            Assert.AreEqual(0, state.Hits);
            Assert.IsFalse(state.RecordHit(target, 1, 3f, false, 3, 6f));
        }

        [Test]
        public void OnlyOneTargetBuildsMarkAndOriginalTargetKeepsCooldown()
        {
            var state = new AnubisMarkState();
            var one = new object();
            var two = new object();
            for (int i = 0; i < 3; i++) state.RecordHit(one, 1, i, true, 3, 6f);
            Assert.AreEqual(3, state.Hits);
            Assert.IsFalse(state.RecordHit(two, 1, 3f, true, 3, 6f));
            Assert.AreEqual(1, state.Hits, "새 대상은 기존 표식을 이어받지 않는다");
            for (int i = 0; i < 2; i++) state.RecordHit(two, 1, 4f + i, true, 3, 6f);
            Assert.IsTrue(state.IsReady(5f, 3));
            Assert.IsTrue(state.RecordHit(two, 1, 6f, true, 3, 6f));
            Assert.IsFalse(state.RecordHit(one, 1, 7f, true, 3, 6f));
            Assert.AreEqual(1, state.Hits, "다시 돌아와도 예전 표식을 이어받지 않는다");
            for (int i = 0; i < 2; i++) state.RecordHit(one, 1, 8f + i, true, 3, 6f);
            Assert.IsTrue(state.IsReady(9f, 3));
            Assert.IsTrue(state.RecordHit(one, 1, 10f, true, 3, 6f));
            Assert.IsFalse(state.RecordHit(two, 1, 10.5f, true, 3, 6f));
            for (int i = 0; i < 2; i++) state.RecordHit(two, 1, 10.6f + i * 0.1f, true, 3, 6f);
            Assert.IsFalse(state.RecordHit(two, 1, 11f, true, 3, 6f), "대상을 바꿔도 6초 재발동 제한이 남는다");
        }

        [Test]
        public void SameTargetCannotRetriggerWithinSixSecondsAndPooledLifeResets()
        {
            var state = new AnubisMarkState();
            var target = new object();
            for (int i = 0; i < 3; i++) state.RecordHit(target, 1, 0f, true, 3, 6f);
            Assert.IsTrue(state.RecordHit(target, 1, 0f, true, 3, 6f));
            for (int i = 0; i < 3; i++) state.RecordHit(target, 1, 3f, true, 3, 6f);
            Assert.IsFalse(state.IsReady(3f, 3));
            Assert.IsFalse(state.RecordHit(target, 1, 3f, true, 3, 6f));
            for (int i = 0; i < 3; i++) state.RecordHit(target, 1, 6f, true, 3, 6f);
            Assert.IsTrue(state.RecordHit(target, 1, 6f, true, 3, 6f));
            Assert.IsFalse(state.RecordHit(target, 2, 7f, true, 3, 6f));
            Assert.AreEqual(1, state.Hits, "풀에서 다시 나온 적은 새 생명으로 센다");
        }

        [Test]
        public void StatueRefreshDoesNotExtendMarkSlow()
        {
            var channels = new SlowChannels();
            var mark = new object();
            var statue = new object();
            channels.Apply(0.35f, 0.8f, 0f, mark);
            for (int i = 0; i < 40; i++) channels.Apply(0.2f, 0.2f, i * 0.1f, statue);
            Assert.AreEqual(0.35f, channels.FractionAt(0.5f), 0.0001f);
            Assert.AreEqual(0.2f, channels.FractionAt(0.9f), 0.0001f);
            Assert.AreEqual(0.2f, channels.FractionAt(3.9f), 0.0001f);
            Assert.AreEqual(0f, channels.FractionAt(4.2f), 0.0001f);
        }

        [Test]
        public void ExistingUnkeyedSlowKeepsStrongerValueAndLatestTime()
        {
            var channels = new SlowChannels();
            channels.Apply(0.4f, 1f, 0f);
            channels.Apply(0.2f, 2f, 0.5f);
            Assert.AreEqual(0.4f, channels.FractionAt(1.2f), 0.0001f);
            Assert.AreEqual(0f, channels.FractionAt(2.5f), 0.0001f);
            channels.Clear();
            Assert.AreEqual(0f, channels.FractionAt(0.5f), 0.0001f);
        }

        [Test]
        public void StarLevelsChangeOnlyMarkDurationAndStrengthWithinCap()
        {
            HeroData hero = Make<HeroData>();
            AnubisDefinition.ConfigureHero(hero);
            Assert.AreEqual("anubis", hero.id);
            Assert.AreEqual(HeroClass.Mage, hero.heroClass);
            Assert.AreEqual(BasicAttackKind.Projectile, hero.attackKind);
            Assert.AreEqual(950f, hero.maxHp);
            Assert.AreEqual(90f, hero.attack);
            Assert.IsTrue(hero.godShardUnlock);
            HeroSignature one = HeroSignature.For(hero, 1);
            HeroSignature three = HeroSignature.For(hero, 3);
            HeroSignature five = HeroSignature.For(hero, 5);
            Assert.AreEqual(3, one.MarkHits);
            Assert.AreEqual(6f, one.MarkCooldown);
            Assert.AreEqual(0.25f, one.MarkSlow, 0.0001f);
            Assert.AreEqual(0.6f, one.MarkDuration, 0.0001f);
            Assert.AreEqual(0.8f, three.MarkDuration, 0.0001f);
            Assert.AreEqual(0.35f, five.MarkSlow, 0.0001f);
            Assert.AreEqual(0.075f, AnubisMarkState.EffectiveSlow(one, hero.markBossScale, true), 0.0001f);
            Assert.AreEqual(0.25f, AnubisMarkState.EffectiveSlow(one, hero.markBossScale, false), 0.0001f);
            Assert.LessOrEqual(AnubisMarkState.EffectiveDuration(five), 1f);
            hero.markDuration = 1.5f;
            Assert.AreEqual(1f, AnubisMarkState.EffectiveDuration(HeroSignature.For(hero, 1)), 0.0001f);
            StringAssert.Contains("★3", HeroSignature.StarText(hero));
            StringAssert.Contains("★5", HeroSignature.StarText(hero));
        }

        [Test]
        public void StatueAndGateStayExclusiveAndGateNeedsHealthPassive()
        {
            ActiveSkillData statue = Make<ActiveSkillData>();
            AnubisDefinition.ConfigureStatue(statue);
            ActiveSkillData gate = Make<ActiveSkillData>();
            AnubisDefinition.ConfigureGate(gate);
            PassiveSkillData health = Make<PassiveSkillData>();
            health.id = "hp_up";
            FusionRecipe recipe = Make<FusionRecipe>();
            AnubisDefinition.ConfigureRecipe(recipe, statue, health, gate);

            Assert.AreEqual("sand_statue", statue.id);
            Assert.AreEqual("sand_gate", gate.id);
            Assert.AreEqual("anubis", statue.heroOnlyId);
            Assert.AreEqual("anubis", gate.heroOnlyId);
            Assert.AreEqual(Rarity.Rare, statue.rarity);
            Assert.AreEqual(9f, statue.cooldown);
            Assert.AreEqual(4f, statue.DurationAt(1));
            Assert.AreEqual(5, statue.maxLevel);
            Assert.AreEqual(3, statue.saveCostFirstLevel);
            Assert.AreEqual(3, statue.saveCostPerLevel);
            Assert.AreEqual(0f, statue.DamageAt(1), 0.0001f);
            Assert.AreEqual(0.2f, statue.SlowAt(1), 0.0001f);
            Assert.AreEqual(0.3f, statue.SlowAt(5), 0.0001f);
            Assert.AreEqual(0.35f, gate.DamageAt(1), 0.0001f);
            Assert.AreEqual(0.6f, gate.waveRadius, 0.0001f);
            Assert.AreEqual(1, gate.maxLevel);
            Assert.AreEqual(10, gate.saveCostFirstLevel);
            Assert.AreEqual(0, gate.saveCostPerLevel);
            Assert.AreEqual(FusionType.Evolution, recipe.type);
            Assert.AreSame(statue, recipe.materialA);
            Assert.AreEqual(5, recipe.requiredLevelA);
            Assert.AreSame(health, recipe.materialB);
            Assert.AreEqual(1, recipe.requiredLevelB);
            Assert.AreSame(gate, recipe.result);
            Assert.AreEqual("anubis", recipe.heroOnlyId);
            Assert.AreEqual(1, SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { statue }, "anubis").Count);
            Assert.IsEmpty(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { statue }, "thor"));
            var loadout = new SkillLoadout();
            loadout.AddNew(statue, 5);
            loadout.AddNew(health);
            CollectionAssert.Contains(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "anubis"), recipe);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "thor"));
        }

        [Test]
        public void AnubisHasOriginalRespectfulLineAtEveryMoment()
        {
            foreach (LineMoment moment in System.Enum.GetValues(typeof(LineMoment)))
                Assert.IsNotEmpty(StoryLines.For("anubis", moment), moment.ToString());
        }
    }
}
