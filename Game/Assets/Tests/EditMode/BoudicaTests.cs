using System.Collections.Generic;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Missions;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using HistoryHeroes.Story;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>새 영웅의 공통 규칙을 메모리 데이터로 확인한다. 장면·생성 에셋에 의존하지 않는다.</summary>
    public sealed class BoudicaTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private HeroData _hero;
        private ActiveSkillData _rally;
        private ActiveSkillData _uprising;
        private PassiveSkillData _attack;
        private FusionRecipe _recipe;

        private T Create<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }

        [SetUp]
        public void SetUp()
        {
            _hero = Create<HeroData>();
            BoudicaDefinition.ConfigureHero(_hero);
            _rally = Create<ActiveSkillData>();
            BoudicaDefinition.ConfigureRally(_rally);
            _hero.exclusiveActive = _rally;
            _attack = Create<PassiveSkillData>();
            _attack.id = "atk_up";
            _uprising = Create<ActiveSkillData>();
            BoudicaDefinition.ConfigureUprising(_uprising);
            _recipe = Create<FusionRecipe>();
            BoudicaDefinition.ConfigureRecipe(_recipe, _rally, _attack, _uprising);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject value in _created) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void MissionOrderContainsSixteenUniqueHeroesWithAthenaLast()
        {
            MissionTable missions = Create<MissionTable>();
            string[] ids = { "khan", "guan", "napoleon", "tesla", "yisunsin", "yiseonggye", "davinci", "zhuge", "jeanne", "seondeok", "boudica", "thor", "cleopatra", "anubis", "susanoo", "athena" };
            CollectionAssert.AreEqual(ids, missions.heroOrder);
            Assert.AreEqual(16, new HashSet<string>(missions.heroOrder).Count);
        }

        [Test]
        public void CharacterDefinitionsContainSixteenHeroesAndFiveWomen()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Khan", "Guan", "Napoleon", "Tesla", "YiSunsin", "YiSeonggye", "Davinci", "Zhuge", "Jeanne", "Seondeok", "Boudica", "Thor", "Cleopatra", "Anubis", "Susanoo", "Athena"
            }, System.Array.ConvertAll(Art3DCharacters.Heroes, look => look.name));
            Assert.AreEqual(5, System.Array.FindAll(Art3DCharacters.Heroes, look => look.female).Length);
        }

        [Test]
        public void BoudicaUsesFemaleRangerLongHairBronzeBracersAndCelticSword()
        {
            Art3DCharacters.Look look = System.Array.Find(Art3DCharacters.Heroes, item => item.name == "Boudica");
            Assert.IsNotNull(look);
            Assert.IsTrue(look.female);
            Assert.AreEqual("Female_Ranger", look.body);
            Assert.AreEqual("T_Ranger_BaseColor", look.bodyTex);
            CollectionAssert.Contains(look.hide, "Female_Ranger_Head_Hood");
            CollectionAssert.AreEquivalent(new[] { "Hair_Long", "Eyebrows_Female" }, look.hair);
            CollectionAssert.AreEquivalent(new[] { "Female_Ranger_Acc_Pauldrons", "Female_Ranger_Arms_Bracer" }, look.metalParts);
            Assert.Greater(look.hairColor.r, look.hairColor.g);
            Assert.Greater(look.hairColor.g, look.hairColor.b);
            Assert.Greater(look.metalColor.r, look.metalColor.g);
            Assert.Greater(look.metalColor.g, look.metalColor.b);
            // 켈트 잎 모양 검 (블렌더 스크립트 Tools/blender/hero_props.py, 2026-10-29) — 손 뼈가 등록돼 있어야 붙는다
            CollectionAssert.AreEqual(new[] { "Boudica_Sword" }, look.props);
            Assert.AreEqual("hand_r", Art3DProps.Bones["Boudica_Sword"]);
            Assert.AreEqual("Sword_Regular_A", look.attack);
        }

        [Test]
        public void ProductionHeroDefinitionStaysWithinTheWarriorBand()
        {
            Assert.AreEqual("boudica", _hero.id);
            Assert.AreEqual("부디카", _hero.displayName);
            Assert.AreEqual(HeroClass.Warrior, _hero.heroClass);
            Assert.AreEqual(BasicAttackKind.MeleeArc, _hero.attackKind);
            Assert.AreEqual(1300f, _hero.maxHp);
            Assert.AreEqual(110f, _hero.attack);
            Assert.AreEqual(0.85f, _hero.attackSpeed);
            Assert.AreEqual(5f, _hero.moveSpeed);
            Assert.AreEqual(2.2f, _hero.range);
            Assert.AreEqual(120f, _hero.meleeArcDegrees);
            Assert.AreEqual("되찾는 자리", _hero.signatureName);
            Assert.IsTrue(_hero.gemsOnly);
            Assert.AreEqual(2000, _hero.unlockGems);
        }

        [Test]
        public void ProductionSkillsAndRecipeKeepTheirDamageRangeCountAndSaveCosts()
        {
            Assert.AreEqual("boudica_rally", _rally.id);
            Assert.AreEqual("boudica_uprising", _uprising.id);
            foreach (ActiveSkillData skill in new[] { _rally, _uprising })
            {
                Assert.AreEqual("boudica", skill.heroOnlyId);
                Assert.AreEqual(Rarity.Rare, skill.rarity);
                Assert.AreEqual(SkillRole.Area, skill.role);
                Assert.IsTrue(skill.recordable);
                Assert.AreEqual(8f, skill.cooldown);
            }
            Assert.AreEqual(5, _rally.maxLevel);
            Assert.AreEqual(3f, _rally.RadiusAt(1));
            Assert.AreEqual(1, _rally.CountAt(1));
            Assert.AreEqual(1, _rally.CountAt(5));
            Assert.AreEqual(1.3f, _rally.DamageAt(1), 1e-5f);
            Assert.AreEqual(2.3f, _rally.DamageAt(5), 1e-5f);
            Assert.AreEqual(3, _rally.saveCostFirstLevel);
            Assert.AreEqual(3, _rally.saveCostPerLevel);
            Assert.AreEqual(3, _uprising.maxLevel);
            Assert.AreEqual(3.2f, _uprising.RadiusAt(1));
            Assert.AreEqual(3, _uprising.CountAt(1));
            Assert.AreEqual(3, _uprising.CountAt(3));
            Assert.AreEqual(1f, _uprising.DamageAt(1));
            Assert.AreEqual(1.4f, _uprising.DamageAt(3), 1e-5f);
            Assert.AreEqual(12, _uprising.saveCostFirstLevel);
            Assert.AreEqual(4, _uprising.saveCostPerLevel);
            Assert.Greater(_uprising.DamageAt(1) * _uprising.CountAt(1), _rally.DamageAt(5));
            Assert.AreEqual(FusionType.Evolution, _recipe.type);
            Assert.AreEqual("boudica", _recipe.heroOnlyId);
            Assert.AreSame(_rally, _recipe.materialA);
            Assert.AreEqual(5, _recipe.requiredLevelA);
            Assert.AreSame(_attack, _recipe.materialB);
            Assert.AreEqual(1, _recipe.requiredLevelB);
            Assert.AreSame(_uprising, _recipe.result);
        }

        [Test]
        public void ChapterProgressCannotBypassTwoThousandGemUnlock()
        {
            var save = new SaveData { unlockedChapter = 99 };
            Assert.IsFalse(HeroUnlock.IsUnlocked(save, _hero));
            Wallet.AddFreeGems(save, 1999);
            Assert.IsFalse(HeroUnlock.TryBuy(save, _hero));
            Assert.AreEqual(1999, Wallet.Gems(save));
            Wallet.AddFreeGems(save, 1);
            Assert.IsTrue(HeroUnlock.TryBuy(save, _hero));
            Assert.AreEqual(0, Wallet.Gems(save));
            Assert.IsTrue(HeroUnlock.IsUnlocked(save, _hero));
            Assert.IsFalse(HeroUnlock.TryBuy(save, _hero));
            StringAssert.Contains("보석 2,000", HeroUnlock.Condition(_hero));
        }

        [Test]
        public void TenRunsOfferOneFreeChoiceAcrossGemHeroes()
        {
            var save = new SaveData { dungeonRuns = 9 };
            Assert.IsFalse(HeroUnlock.CanClaimFree(save, _hero));
            save.dungeonRuns = 10;
            Assert.IsTrue(HeroUnlock.TryClaimFree(save, _hero));
            Assert.IsTrue(HeroUnlock.IsUnlocked(save, _hero));
            Assert.AreEqual(0, Wallet.Gems(save));
            foreach (string id in new[] { "davinci", "zhuge", "jeanne", "seondeok", "boudica" })
            {
                HeroData hero = Create<HeroData>();
                hero.id = id;
                hero.gemsOnly = true;
                Assert.IsFalse(HeroUnlock.CanClaimFree(save, hero), id);
            }

            var alreadyClaimed = new SaveData { dungeonRuns = 100, freeHeroClaimed = true };
            Assert.IsFalse(HeroUnlock.TryClaimFree(alreadyClaimed, _hero));
        }

        [Test]
        public void ExclusiveActiveAppearsOnlyForBoudicaAcrossTheWholeRoster()
        {
            foreach (string heroId in Create<MissionTable>().heroOrder)
            {
                var candidates = SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { _rally }, heroId);
                Assert.AreEqual(heroId == "boudica" ? 1 : 0, candidates.Count, heroId);
            }
            Assert.IsEmpty(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { _rally }, null));
        }

        [Test]
        public void UprisingNeedsHerOwnLevelFiveActiveAndAttackPassive()
        {
            var loadout = new SkillLoadout();
            loadout.AddNew(_attack);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { _recipe }, _hero.id));
            loadout.AddNew(_rally, 4);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { _recipe }, _hero.id));
            loadout.Find(_rally).Level = 5;
            CollectionAssert.Contains(FusionFinder.FindAvailableFusions(loadout, new[] { _recipe }, _hero.id), _recipe);
            foreach (string heroId in Create<MissionTable>().heroOrder)
                if (heroId != "boudica")
                    Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { _recipe }, heroId), heroId);

            var missingPassive = new SkillLoadout();
            missingPassive.AddNew(_rally, 5);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(missingPassive, new[] { _recipe }, _hero.id));

            var differentActive = Create<ActiveSkillData>();
            differentActive.id = "jeanne_wave";
            differentActive.maxLevel = 5;
            var wrongActive = new SkillLoadout();
            wrongActive.AddNew(differentActive, 5);
            wrongActive.AddNew(_attack);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(wrongActive, new[] { _recipe }, _hero.id));
        }

        [Test]
        public void EveryStoryMomentHasABoudicaLineInsteadOfTheUnknownHeroFallback()
        {
            foreach (LineMoment moment in System.Enum.GetValues(typeof(LineMoment)))
            {
                string line = StoryLines.For("boudica", moment);
                Assert.IsNotEmpty(line, moment.ToString());
                Assert.AreNotEqual(StoryLines.For("unknown", moment), line, moment.ToString());
            }
        }
    }
}
