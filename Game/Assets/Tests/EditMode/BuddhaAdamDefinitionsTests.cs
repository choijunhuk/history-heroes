using System;
using System.Collections.Generic;
using System.Linq;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Missions;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using HistoryHeroes.Story;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>장면을 열지 않고 두 영웅의 승인 수치와 전용 연결을 검사한다.</summary>
    public sealed class BuddhaAdamDefinitionsTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private readonly HeroData[] _heroes = new HeroData[2];
        private readonly ActiveSkillData[] _actives = new ActiveSkillData[2];
        private readonly ActiveSkillData[] _fusions = new ActiveSkillData[2];
        private readonly PassiveSkillData[] _passives = new PassiveSkillData[2];
        private readonly FusionRecipe[] _recipes = new FusionRecipe[2];

        private T Create<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }

        [SetUp]
        public void SetUp()
        {
            for (int i = 0; i < 2; i++)
            {
                _heroes[i] = Create<HeroData>();
                _actives[i] = Create<ActiveSkillData>();
                _fusions[i] = Create<ActiveSkillData>();
                _passives[i] = Create<PassiveSkillData>();
                _recipes[i] = Create<FusionRecipe>();
            }
            BuddhaDefinition.ConfigureHero(_heroes[0]);
            BuddhaDefinition.ConfigureRest(_actives[0]);
            BuddhaDefinition.ConfigureBreath(_fusions[0]);
            _passives[0].id = "regen";
            BuddhaDefinition.ConfigureRecipe(_recipes[0], _actives[0], _passives[0], _fusions[0]);
            AdamDefinition.ConfigureHero(_heroes[1]);
            AdamDefinition.ConfigurePath(_actives[1]);
            AdamDefinition.ConfigureWay(_fusions[1]);
            _passives[1].id = "dodge";
            AdamDefinition.ConfigureRecipe(_recipes[1], _actives[1], _passives[1], _fusions[1]);
            for (int i = 0; i < 2; i++) _heroes[i].exclusiveActive = _actives[i];
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject value in _created) UnityEngine.Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void RosterOrderAddsTwoHeroesAfterCurie()
        {
            MissionTable table = Create<MissionTable>();
            string[] ids =
            {
                "khan", "guan", "napoleon", "tesla", "yisunsin", "yiseonggye", "davinci", "zhuge",
                "jeanne", "seondeok", "boudica", "thor", "cleopatra", "anubis", "susanoo", "athena",
                "tomoe", "wuzetian", "curie", "buddha", "adam"
            };
            CollectionAssert.AreEqual(ids, table.heroOrder);
            Assert.AreEqual(21, table.heroOrder.Distinct().Count());
            CollectionAssert.AreEqual(new[] { "Buddha", "Adam" }, Art3DCharacters.Heroes.Skip(19).Select(look => look.name));
        }

        [Test]
        public void MissionBuilderCopiesTheCurrentRosterOrder()
        {
            HeroRoster roster = Create<HeroRoster>();
            roster.heroes.Add(_heroes[0]);
            roster.heroes.Add(_heroes[1]);
            CollectionAssert.AreEqual(new[] { "buddha", "adam" }, SceneBuilder.MissionHeroOrder(roster));
            roster.heroes.Reverse();
            CollectionAssert.AreEqual(new[] { "adam", "buddha" }, SceneBuilder.MissionHeroOrder(roster));
        }

        [Test]
        public void HeroBaseStatsAndSignatureValuesMatchDesign()
        {
            HeroData buddha = _heroes[0], adam = _heroes[1];
            Assert.AreEqual("부처", buddha.displayName);
            Assert.AreEqual(HeroClass.Mage, buddha.heroClass);
            Assert.AreEqual(BasicAttackKind.Projectile, buddha.attackKind);
            Assert.AreEqual(950f, buddha.maxHp);
            Assert.AreEqual(85f, buddha.attack);
            Assert.AreEqual(5.5f, buddha.range);
            Assert.AreEqual(13f, buddha.projectileSpeed);
            Assert.AreEqual(3, buddha.breathAttacks);
            Assert.AreEqual(0.05f, buddha.breathShieldFraction);
            Assert.AreEqual(2f, buddha.breathShieldDuration);
            Assert.AreEqual(8f, buddha.breathCooldown);
            Assert.AreEqual(2.5f, buddha.star3BreathShieldDuration);
            Assert.AreEqual(0.06f, buddha.star5BreathShieldFraction);

            Assert.AreEqual("아담", adam.displayName);
            Assert.AreEqual(HeroClass.Warrior, adam.heroClass);
            Assert.AreEqual(BasicAttackKind.MeleeArc, adam.attackKind);
            Assert.AreEqual(1200f, adam.maxHp);
            Assert.AreEqual(105f, adam.attack);
            Assert.AreEqual(5.2f, adam.moveSpeed);
            Assert.AreEqual(2f, adam.range);
            Assert.AreEqual(120f, adam.meleeArcDegrees);
            Assert.AreEqual(0.8f, adam.footstepMoveSeconds);
            Assert.AreEqual(0.1f, adam.footstepDodgeBonus);
            Assert.AreEqual(1.5f, adam.footstepDodgeDuration);
            Assert.AreEqual(6f, adam.footstepCooldown);
            Assert.AreEqual(2f, adam.star3FootstepDodgeDuration);
            Assert.AreEqual(0.12f, adam.star5FootstepDodgeBonus);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void TwoThousandGemsOrOneTenRunPickUnlocksHero(int index)
        {
            HeroData hero = _heroes[index];
            var save = new SaveData { unlockedChapter = 99, dungeonRuns = 9 };
            Assert.IsTrue(hero.gemsOnly);
            Assert.AreEqual(2000, hero.unlockGems);
            Assert.IsFalse(HeroUnlock.IsUnlocked(save, hero));
            Assert.IsFalse(HeroUnlock.CanClaimFree(save, hero));
            Wallet.AddFreeGems(save, 1999);
            Assert.IsFalse(HeroUnlock.TryBuy(save, hero));
            Wallet.AddFreeGems(save, 1);
            Assert.IsTrue(HeroUnlock.TryBuy(save, hero));
            Assert.AreEqual(0, Wallet.Gems(save));

            var free = new SaveData { dungeonRuns = HeroUnlock.FreePickRuns };
            Assert.IsTrue(HeroUnlock.TryClaimFree(free, hero));
            Assert.IsFalse(HeroUnlock.TryClaimFree(free, _heroes[1 - index]));
        }

        [TestCase(0, "buddha", "regen")]
        [TestCase(1, "adam", "dodge")]
        public void OnlyOwnerCanDrawActiveAndCompleteEvolution(int index, string owner, string passiveId)
        {
            ActiveSkillData active = _actives[index], fusion = _fusions[index];
            FusionRecipe recipe = _recipes[index];
            Assert.AreSame(active, _heroes[index].exclusiveActive);
            Assert.AreEqual(owner, active.heroOnlyId);
            Assert.AreEqual(owner, fusion.heroOnlyId);
            Assert.AreEqual(owner, recipe.heroOnlyId);
            Assert.AreEqual(passiveId, recipe.materialB.id);
            Assert.AreEqual(FusionType.Evolution, recipe.type);
            Assert.AreEqual(5, recipe.requiredLevelA);
            Assert.AreEqual(1, recipe.requiredLevelB);
            Assert.AreSame(active, recipe.materialA);
            Assert.AreSame(fusion, recipe.result);
            Assert.AreEqual(1, SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { active }, owner).Count);
            Assert.IsEmpty(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { active }, _heroes[1 - index].id));
            var loadout = new SkillLoadout();
            loadout.AddNew(active, 5);
            loadout.AddNew(_passives[index], 1);
            Assert.AreEqual(1, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, owner).Count);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, _heroes[1 - index].id));
        }

        [Test]
        public void RestAndBreathHealWithoutDamageAndCardShowsHealing()
        {
            Assert.AreEqual(12f, _actives[0].cooldown);
            Assert.AreEqual(12f, _fusions[0].cooldown);
            Assert.AreEqual(6f, _actives[0].RadiusAt(1));
            Assert.AreEqual(6f, _fusions[0].RadiusAt(1));
            Assert.AreEqual(0f, _actives[0].DamageAt(5));
            Assert.AreEqual(0f, _fusions[0].DamageAt(3));
            Assert.AreEqual(0.02f, _actives[0].HealFractionAt(1));
            Assert.AreEqual(0.04f, _actives[0].HealFractionAt(5));
            Assert.AreEqual(0.025f, _fusions[0].HealFractionAt(1));
            Assert.AreEqual(0.035f, _fusions[0].HealFractionAt(3));
            Assert.AreEqual(1, _actives[0].CountAt(1));
            Assert.AreEqual(2, _fusions[0].CountAt(1));
            Assert.AreEqual(0.6f, _fusions[0].DurationAt(1));
            Assert.AreEqual("회당 회복(최대체력) 2% → 2.5%", SkillCardView.UpgradeLine(_actives[0], 1, 2));
            StringAssert.DoesNotContain("피해", SkillCardView.UpgradeLine(_actives[0], 1, 2));
        }

        [Test]
        public void PathAndWayUseFixedArcHitCountsAndNoKnockback()
        {
            Assert.AreEqual(7f, _actives[1].cooldown);
            Assert.AreEqual(7f, _fusions[1].cooldown);
            Assert.AreEqual(2.8f, _actives[1].RadiusAt(1));
            Assert.AreEqual(3f, _fusions[1].RadiusAt(1));
            Assert.AreEqual(1, _actives[1].CountAt(1));
            Assert.AreEqual(2, _fusions[1].CountAt(1));
            Assert.AreEqual(0.3f, _fusions[1].DurationAt(1));
            Assert.AreEqual(1f, _actives[1].DamageAt(1));
            Assert.AreEqual(1.8f, _actives[1].DamageAt(5));
            Assert.AreEqual(1.1f, _fusions[1].DamageAt(1));
            Assert.AreEqual(1.5f, _fusions[1].DamageAt(3));
            Assert.AreEqual(0f, _actives[1].pushDistance);
            Assert.AreEqual(0f, _fusions[1].pushDistance);
        }

        [Test]
        public void LooksUseOnlyApprovedBodiesHairAndProps()
        {
            Art3DCharacters.Look buddha = Art3DCharacters.Heroes.Single(look => look.name == "Buddha");
            Assert.AreEqual("Male_Peasant", buddha.body);
            Assert.AreEqual("T_Peasant_BaseColor", buddha.bodyTex);
            CollectionAssert.AreEquivalent(new[] { "Hair_Buzzed", "Eyebrows" }, buddha.hair);
            CollectionAssert.AreEquivalent(new[] { "Buddha_Bowl" }, buddha.props);
            Assert.AreEqual("hand_l", Art3DProps.Bones["Buddha_Bowl"]);
            Assert.AreEqual("Spell_Simple_Shoot", buddha.attack);

            Art3DCharacters.Look adam = Art3DCharacters.Heroes.Single(look => look.name == "Adam");
            Assert.AreEqual("Male_Peasant", adam.body);
            Assert.AreEqual("T_Peasant_2_BaseColor", adam.bodyTex);
            CollectionAssert.AreEquivalent(new[] { "Hair_Long", "Hair_Beard", "Eyebrows" }, adam.hair);
            CollectionAssert.AreEquivalent(new[] { "Adam_Staff" }, adam.props);
            Assert.AreEqual("hand_r", Art3DProps.Bones["Adam_Staff"]);
            Assert.AreEqual("Sword_Regular_A", adam.attack);
        }

        [Test]
        public void SevenLinesPerHeroUseTheApprovedStory()
        {
            Assert.AreEqual("서로 지킨 기억을 살펴봅시다.", StoryLines.For("buddha", LineMoment.RunStart));
            Assert.AreEqual("잠시 물러나겠소. 기록을 부탁하오.", StoryLines.For("adam", LineMoment.Death));
            foreach (HeroData hero in _heroes)
                foreach (LineMoment moment in Enum.GetValues(typeof(LineMoment)))
                    Assert.IsNotEmpty(StoryLines.For(hero.id, moment), $"{hero.id}: {moment}");
        }
    }
}
