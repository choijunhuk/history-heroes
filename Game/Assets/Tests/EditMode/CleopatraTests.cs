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
    /// <summary>새 영웅의 수치와 진화 조건을 장면 생성 없이 확인한다.</summary>
    public sealed class CleopatraTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private HeroData _hero;
        private ActiveSkillData _pharos;
        private ActiveSkillData _library;
        private PassiveSkillData _activeDamage;
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
            CleopatraDefinition.ConfigureHero(_hero);
            _pharos = Create<ActiveSkillData>();
            CleopatraDefinition.ConfigurePharos(_pharos);
            _hero.exclusiveActive = _pharos;
            _library = Create<ActiveSkillData>();
            CleopatraDefinition.ConfigureLibrary(_library);
            _activeDamage = Create<PassiveSkillData>();
            _activeDamage.id = "active_dmg";
            _recipe = Create<FusionRecipe>();
            CleopatraDefinition.ConfigureRecipe(_recipe, _pharos, _activeDamage, _library);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject value in _created) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void MissionOrderContainsNineteenUniqueHeroesWithCurieLast()
        {
            MissionTable missions = Create<MissionTable>();
            string[] ids = { "khan", "guan", "napoleon", "tesla", "yisunsin", "yiseonggye", "davinci", "zhuge", "jeanne", "seondeok", "boudica", "thor", "cleopatra", "anubis", "susanoo", "athena", "tomoe", "wuzetian", "curie" };
            CollectionAssert.AreEqual(ids, missions.heroOrder);
            Assert.AreEqual(19, new HashSet<string>(missions.heroOrder).Count);
        }

        [Test]
        public void CharacterDefinitionsContainNineteenHeroesAndEightWomen()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Khan", "Guan", "Napoleon", "Tesla", "YiSunsin", "YiSeonggye", "Davinci", "Zhuge", "Jeanne", "Seondeok", "Boudica", "Thor", "Cleopatra", "Anubis", "Susanoo", "Athena", "Tomoe", "Wuzetian", "Curie"
            }, System.Array.ConvertAll(Art3DCharacters.Heroes, look => look.name));
            Assert.AreEqual(8, System.Array.FindAll(Art3DCharacters.Heroes, look => look.female).Length);
        }

        [Test]
        public void CharacterUsesFemalePeasantHairCrownCollarAndScroll()
        {
            Art3DCharacters.Look look = System.Array.Find(Art3DCharacters.Heroes, item => item.name == "Cleopatra");
            Assert.IsNotNull(look);
            Assert.IsTrue(look.female);
            Assert.AreEqual("Female_Peasant", look.body);
            CollectionAssert.AreEquivalent(new[] { "Hair_Long", "Eyebrows_Female" }, look.hair);
            CollectionAssert.DoesNotContain(look.goldParts ?? new string[0], "Female_Peasant_Body", "같은 몸의 선덕여왕(금빛 윗옷)과 구분 — 상아 윗옷");
            Assert.Greater(look.tint.r, 0.9f);
            Assert.Less(look.hairColor.r, 0.1f);
            Assert.Less(look.hairColor.g, 0.1f);
            Assert.Less(look.hairColor.b, 0.1f);
            CollectionAssert.Contains(look.props, "Cleopatra_Crown");
            CollectionAssert.Contains(look.props, "Cleopatra_Collar");
            CollectionAssert.Contains(look.props, "Davinci_Scroll");
            Assert.AreEqual("Head", Art3DProps.Bones["Cleopatra_Crown"]);
            Assert.AreEqual("spine_03", Art3DProps.Bones["Cleopatra_Collar"]);
            Assert.AreEqual("hand_l", Art3DProps.Bones["Davinci_Scroll"]);
        }

        [Test]
        public void HeroUsesTheSummonerBandWithoutGodShardRules()
        {
            Assert.AreEqual("cleopatra", _hero.id);
            Assert.AreEqual("클레오파트라", _hero.displayName);
            Assert.AreEqual(HeroClass.Summoner, _hero.heroClass);
            Assert.AreEqual(BasicAttackKind.Projectile, _hero.attackKind);
            Assert.AreEqual(900f, _hero.maxHp);
            Assert.AreEqual(80f, _hero.attack);
            Assert.AreEqual(1f, _hero.attackSpeed);
            Assert.AreEqual(5f, _hero.range);
            Assert.AreEqual(5f, _hero.moveSpeed);
            Assert.AreEqual(15f, _hero.projectileSpeed);
            Assert.AreEqual(0.2f, _hero.firstShotDelay);
            Assert.AreEqual("기록의 수호상", _hero.signatureName);
            Assert.AreEqual(3, _hero.guardianAttacks);
            Assert.AreEqual(2f, _hero.guardianNearbyRadius);
            Assert.IsFalse(_hero.godShardUnlock);
            Assert.AreEqual(0, _hero.unlockAfterBossChapter);
        }

        [Test]
        public void PharosHasFourRareAreaPulsesAtEightSecondCooldown()
        {
            Assert.AreSame(_pharos, _hero.exclusiveActive);
            Assert.AreEqual("cleopatra_pharos", _pharos.id);
            Assert.AreEqual("파로스의 빛", _pharos.displayName);
            Assert.AreEqual("cleopatra", _pharos.heroOnlyId);
            Assert.AreEqual(Rarity.Rare, _pharos.rarity);
            Assert.AreEqual(SkillTag.Lightning, _pharos.tags);
            Assert.AreEqual(SkillRole.Area, _pharos.role);
            Assert.IsTrue(_pharos.recordable);
            Assert.AreEqual(8f, _pharos.cooldown);
            Assert.AreEqual(5, _pharos.maxLevel);
            Assert.AreEqual(4, _pharos.CountAt(1));
            Assert.AreEqual(4, _pharos.CountAt(5));
            Assert.AreEqual(4f, _pharos.DurationAt(1));
            Assert.AreEqual(0.6f, _pharos.RadiusAt(1));
            Assert.AreEqual(0.45f, _pharos.DamageAt(1), 1e-5f);
            Assert.AreEqual(0.85f, _pharos.DamageAt(5), 1e-5f);
            Assert.AreEqual(3, _pharos.saveCostFirstLevel);
            Assert.AreEqual(3, _pharos.saveCostPerLevel);
        }

        [Test]
        public void LibraryKeepsFourPulsesAndExpandsTheirArea()
        {
            Assert.AreEqual("cleopatra_library", _library.id);
            Assert.AreEqual("알렉산드리아의 서고", _library.displayName);
            Assert.AreEqual("cleopatra", _library.heroOnlyId);
            Assert.AreEqual(Rarity.Rare, _library.rarity);
            Assert.AreEqual(SkillTag.Lightning, _library.tags);
            Assert.AreEqual(SkillRole.Area, _library.role);
            Assert.IsTrue(_library.recordable);
            Assert.AreEqual(8f, _library.cooldown);
            Assert.AreEqual(3, _library.maxLevel);
            Assert.AreEqual(4, _library.CountAt(1));
            Assert.AreEqual(4f, _library.DurationAt(1));
            Assert.AreEqual(1.6f, _library.RadiusAt(1));
            Assert.AreEqual(0.7f, _library.DamageAt(1), 1e-5f);
            Assert.AreEqual(1f, _library.DamageAt(3), 1e-5f);
            Assert.AreEqual(12, _library.saveCostFirstLevel);
            Assert.AreEqual(4, _library.saveCostPerLevel);
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
            StringAssert.Contains("보석 2,000", HeroUnlock.Condition(_hero));
        }

        [Test]
        public void TenRunsOfferOneSharedFreeChoice()
        {
            var save = new SaveData { dungeonRuns = 9 };
            Assert.IsFalse(HeroUnlock.CanClaimFree(save, _hero));
            save.dungeonRuns = 10;
            Assert.IsTrue(HeroUnlock.TryClaimFree(save, _hero));
            Assert.IsTrue(HeroUnlock.IsUnlocked(save, _hero));
            Assert.AreEqual(0, Wallet.Gems(save));
            foreach (string id in new[] { "davinci", "zhuge", "jeanne", "seondeok", "boudica" })
            {
                HeroData other = Create<HeroData>();
                other.id = id;
                other.gemsOnly = true;
                Assert.IsFalse(HeroUnlock.CanClaimFree(save, other), id);
            }
            Assert.IsFalse(HeroUnlock.TryClaimFree(new SaveData { dungeonRuns = 100, freeHeroClaimed = true }, _hero));
        }

        [Test]
        public void ExclusiveActiveAppearsOnlyForCleopatra()
        {
            foreach (string heroId in Create<MissionTable>().heroOrder)
            {
                var candidates = SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { _pharos }, heroId);
                Assert.AreEqual(heroId == "cleopatra" ? 1 : 0, candidates.Count, heroId);
            }
            Assert.IsEmpty(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { _pharos }, null));
        }

        [Test]
        public void LibraryRequiresHerLevelFiveActiveAndActiveDamagePassive()
        {
            Assert.AreEqual(FusionType.Evolution, _recipe.type);
            Assert.AreEqual("cleopatra", _recipe.heroOnlyId);
            Assert.AreSame(_pharos, _recipe.materialA);
            Assert.AreEqual(5, _recipe.requiredLevelA);
            Assert.AreSame(_activeDamage, _recipe.materialB);
            Assert.AreEqual("active_dmg", _recipe.materialB.id);
            Assert.AreEqual(1, _recipe.requiredLevelB);
            Assert.AreSame(_library, _recipe.result);
            var loadout = new SkillLoadout();
            loadout.AddNew(_pharos, 4);
            loadout.AddNew(_activeDamage);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { _recipe }, _hero.id));
            loadout.Find(_pharos).Level = 5;
            CollectionAssert.Contains(FusionFinder.FindAvailableFusions(loadout, new[] { _recipe }, _hero.id), _recipe);
            foreach (string heroId in Create<MissionTable>().heroOrder)
                if (heroId != "cleopatra")
                    Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { _recipe }, heroId), heroId);
        }

        [Test]
        public void LibraryCannotFuseWithoutThePassiveOrWithAnotherActive()
        {
            var missingPassive = new SkillLoadout();
            missingPassive.AddNew(_pharos, 5);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(missingPassive, new[] { _recipe }, _hero.id));

            ActiveSkillData wrongActive = Create<ActiveSkillData>();
            wrongActive.id = "seondeok_starlight";
            wrongActive.maxLevel = 5;
            var wrongLoadout = new SkillLoadout();
            wrongLoadout.AddNew(wrongActive, 5);
            wrongLoadout.AddNew(_activeDamage);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(wrongLoadout, new[] { _recipe }, _hero.id));
        }

        [Test]
        public void EveryStoryMomentHasACleopatraLine()
        {
            foreach (LineMoment moment in System.Enum.GetValues(typeof(LineMoment)))
            {
                string line = StoryLines.For("cleopatra", moment);
                Assert.IsNotEmpty(line, moment.ToString());
                Assert.AreNotEqual(StoryLines.For("unknown", moment), line, moment.ToString());
            }
        }
    }
}
