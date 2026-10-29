using System.Collections.Generic;
using System.Linq;
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
    /// <summary>생성 에셋을 열지 않고 세 인간 영웅의 해금과 전용 기술 연결을 검사한다.</summary>
    public sealed class HumansCDefinitionsTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private readonly HeroData[] _heroes = new HeroData[3];
        private readonly ActiveSkillData[] _actives = new ActiveSkillData[3];
        private readonly ActiveSkillData[] _fusions = new ActiveSkillData[3];
        private readonly PassiveSkillData[] _passives = new PassiveSkillData[3];
        private readonly FusionRecipe[] _recipes = new FusionRecipe[3];

        private T Create<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }

        [SetUp]
        public void SetUp()
        {
            for (int i = 0; i < 3; i++)
            {
                _heroes[i] = Create<HeroData>();
                _actives[i] = Create<ActiveSkillData>();
                _fusions[i] = Create<ActiveSkillData>();
                _passives[i] = Create<PassiveSkillData>();
                _recipes[i] = Create<FusionRecipe>();
            }
            TomoeDefinition.ConfigureHero(_heroes[0]);
            TomoeDefinition.ConfigureVolley(_actives[0]);
            TomoeDefinition.ConfigureCrosswind(_fusions[0]);
            _passives[0].id = "aspd_up";
            TomoeDefinition.ConfigureRecipe(_recipes[0], _actives[0], _passives[0], _fusions[0]);

            WuzetianDefinition.ConfigureHero(_heroes[1]);
            WuzetianDefinition.ConfigureDecree(_actives[1]);
            WuzetianDefinition.ConfigureOrder(_fusions[1]);
            _passives[1].id = "def_up";
            WuzetianDefinition.ConfigureRecipe(_recipes[1], _actives[1], _passives[1], _fusions[1]);

            CurieDefinition.ConfigureHero(_heroes[2]);
            CurieDefinition.ConfigurePrism(_actives[2]);
            CurieDefinition.ConfigureSpectrum(_fusions[2]);
            _passives[2].id = "active_dmg";
            CurieDefinition.ConfigureRecipe(_recipes[2], _actives[2], _passives[2], _fusions[2]);

            for (int i = 0; i < 3; i++) _heroes[i].exclusiveActive = _actives[i];
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject value in _created) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void MissionOrderContainsTheOldSixteenThenThreeDistinctHumans()
        {
            MissionTable table = Create<MissionTable>();
            CollectionAssert.AreEqual(new[]
            {
                "khan", "guan", "napoleon", "tesla", "yisunsin", "yiseonggye", "davinci", "zhuge",
                "jeanne", "seondeok", "boudica", "thor", "cleopatra", "anubis", "susanoo", "athena",
                "tomoe", "wuzetian", "curie",
            }, table.heroOrder);
            Assert.AreEqual(19, table.heroOrder.Distinct().Count());
        }

        [TestCase(0, "tomoe", "토모에 고젠", HeroClass.Archer, 950f, 100f, 6f, 16f)]
        [TestCase(1, "wuzetian", "무측천", HeroClass.Mage, 900f, 90f, 5.5f, 12f)]
        [TestCase(2, "curie", "마리 퀴리", HeroClass.Mage, 900f, 90f, 5.5f, 14f)]
        public void EachHeroStaysInsideItsClassBand(int index, string id, string name, HeroClass heroClass,
            float hp, float attack, float range, float projectileSpeed)
        {
            HeroData hero = _heroes[index];
            Assert.AreEqual(id, hero.id);
            Assert.AreEqual(name, hero.displayName);
            Assert.AreEqual(heroClass, hero.heroClass);
            Assert.AreEqual(BasicAttackKind.Projectile, hero.attackKind);
            Assert.AreEqual(hp, hero.maxHp);
            Assert.AreEqual(attack, hero.attack);
            Assert.AreEqual(1f, hero.attackSpeed);
            Assert.AreEqual(5f, hero.moveSpeed);
            Assert.AreEqual(range, hero.range);
            Assert.AreEqual(projectileSpeed, hero.projectileSpeed);
            Assert.AreEqual(0.2f, hero.firstShotDelay);
            Assert.IsFalse(hero.godShardUnlock);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TwoThousandGemsUnlockWithoutChapterProgress(int index)
        {
            HeroData hero = _heroes[index];
            var save = new SaveData { unlockedChapter = 99 };
            Assert.IsTrue(hero.gemsOnly);
            Assert.AreEqual(2000, hero.unlockGems);
            Assert.IsFalse(HeroUnlock.IsUnlocked(save, hero));
            Wallet.AddFreeGems(save, 1999);
            Assert.IsFalse(HeroUnlock.TryBuy(save, hero));
            Wallet.AddFreeGems(save, 1);
            Assert.IsTrue(HeroUnlock.TryBuy(save, hero));
            Assert.AreEqual(0, Wallet.Gems(save));
            Assert.IsTrue(HeroUnlock.IsUnlocked(save, hero));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TenRunChoiceCanSelectEachHeroButUnlocksOnlyOne(int chosen)
        {
            var save = new SaveData { dungeonRuns = 9 };
            Assert.IsFalse(HeroUnlock.CanClaimFree(save, _heroes[chosen]));
            save.dungeonRuns = 10;
            Assert.IsTrue(HeroUnlock.TryClaimFree(save, _heroes[chosen]));
            Assert.IsTrue(HeroUnlock.IsUnlocked(save, _heroes[chosen]));
            for (int i = 0; i < _heroes.Length; i++)
                if (i != chosen)
                {
                    Assert.IsFalse(HeroUnlock.CanClaimFree(save, _heroes[i]));
                    Assert.IsFalse(HeroUnlock.TryClaimFree(save, _heroes[i]));
                }
        }

        [TestCase(0, "tomoe_volley", "tomoe_crosswind", "aspd_up", 7f, 3, 5, 0.55f, 0.6f)]
        [TestCase(1, "wuzetian_decree", "wuzetian_order", "def_up", 8f, 1, 3, 1f, 0.65f)]
        [TestCase(2, "curie_prism", "curie_spectrum", "active_dmg", 8f, 1, 3, 1.1f, 0.7f)]
        public void ExclusiveActiveAndFusionKeepTheirOwnRecipe(int index, string activeId, string fusionId,
            string passiveId, float cooldown, int activeCount, int fusionCount, float activeDamage, float fusionDamage)
        {
            HeroData hero = _heroes[index];
            ActiveSkillData active = _actives[index];
            ActiveSkillData result = _fusions[index];
            FusionRecipe recipe = _recipes[index];
            Assert.AreSame(active, hero.exclusiveActive);
            Assert.AreEqual(activeId, active.id);
            Assert.AreEqual(fusionId, result.id);
            Assert.AreEqual(hero.id, active.heroOnlyId);
            Assert.AreEqual(hero.id, result.heroOnlyId);
            Assert.AreEqual(hero.id, recipe.heroOnlyId);
            Assert.AreEqual(Rarity.Rare, active.rarity);
            Assert.AreEqual(Rarity.Rare, result.rarity);
            Assert.AreEqual(SkillRole.Area, active.role);
            Assert.AreEqual(SkillRole.Area, result.role);
            Assert.IsTrue(active.recordable);
            Assert.IsTrue(result.recordable);
            Assert.AreEqual(3, active.saveCostFirstLevel);
            Assert.AreEqual(3, active.saveCostPerLevel);
            Assert.AreEqual(12, result.saveCostFirstLevel);
            Assert.AreEqual(4, result.saveCostPerLevel);
            Assert.AreEqual(5, active.maxLevel);
            Assert.AreEqual(3, result.maxLevel);
            Assert.AreEqual(cooldown, active.cooldown);
            Assert.AreEqual(cooldown, result.cooldown);
            Assert.AreEqual(activeCount, active.CountAt(1));
            Assert.AreEqual(fusionCount, result.CountAt(1));
            Assert.AreEqual(activeDamage, active.DamageAt(1), 1e-5f);
            Assert.AreEqual(fusionDamage, result.DamageAt(1), 1e-5f);
            Assert.AreSame(active, recipe.materialA);
            Assert.AreEqual(5, recipe.requiredLevelA);
            Assert.AreSame(_passives[index], recipe.materialB);
            Assert.AreEqual(passiveId, recipe.materialB.id);
            Assert.AreEqual(1, recipe.requiredLevelB);
            Assert.AreSame(result, recipe.result);
            Assert.AreEqual(FusionType.Evolution, recipe.type);
        }

        [TestCase(0, 0.95f, 0.84f, 0f)]
        [TestCase(1, 1.8f, 0.95f, 1.5f)]
        [TestCase(2, 1.9f, 1f, 1.6f)]
        public void SkillMaxLevelValuesMatchTheDesign(int index, float activeDamage, float fusionDamage, float radius)
        {
            Assert.AreEqual(activeDamage, _actives[index].DamageAt(5), 1e-5f);
            Assert.AreEqual(fusionDamage, _fusions[index].DamageAt(3), 1e-5f);
            Assert.AreEqual(radius, _actives[index].RadiusAt(1), 1e-5f);
            Assert.AreEqual(radius, _fusions[index].RadiusAt(1), 1e-5f);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ExclusiveActiveAppearsOnlyForItsHero(int index)
        {
            string owner = _heroes[index].id;
            foreach (string heroId in Create<MissionTable>().heroOrder)
            {
                int count = SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { _actives[index] }, heroId).Count;
                Assert.AreEqual(heroId == owner ? 1 : 0, count, heroId);
            }
            Assert.IsEmpty(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { _actives[index] }, null));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void FusionRequiresItsOwnLevelFiveActiveAndPassive(int index)
        {
            var loadout = new SkillLoadout();
            loadout.AddNew(_actives[index], 4);
            loadout.AddNew(_passives[index]);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { _recipes[index] }, _heroes[index].id));
            loadout.Find(_actives[index]).Level = 5;
            CollectionAssert.Contains(FusionFinder.FindAvailableFusions(loadout, new[] { _recipes[index] }, _heroes[index].id), _recipes[index]);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { _recipes[index] }, "khan"));
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { _recipes[index] }, null));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void FusionRejectsMissingPassiveOrOtherHeroesActive(int index)
        {
            var noPassive = new SkillLoadout();
            noPassive.AddNew(_actives[index], 5);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(noPassive, new[] { _recipes[index] }, _heroes[index].id));

            var wrongActive = new SkillLoadout();
            wrongActive.AddNew(_actives[(index + 1) % 3], 5);
            wrongActive.AddNew(_passives[index]);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(wrongActive, new[] { _recipes[index] }, _heroes[index].id));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void AllSevenStoryMomentsHaveOwnLines(int index)
        {
            foreach (LineMoment moment in System.Enum.GetValues(typeof(LineMoment)))
                Assert.IsNotEmpty(StoryLines.For(_heroes[index].id, moment), moment.ToString());
        }

        [TestCase(0, "70%", "80%")]
        [TestCase(1, "0.8초", "3")]
        [TestCase(2, "55%", "1.6")]
        public void StarTextShowsBothHeroSpecificUpgrades(int index, string star3, string star5)
        {
            string text = HeroSignature.StarText(_heroes[index]);
            StringAssert.Contains("★3", text);
            StringAssert.Contains(star3, text);
            StringAssert.Contains("★5", text);
            StringAssert.Contains(star5, text);
        }

        [Test]
        public void TomoeLookUsesHoodlessRangerAndFourRegisteredProps()
        {
            Art3DCharacters.Look look = Art3DCharacters.Heroes.Single(item => item.name == "Tomoe");
            Assert.IsTrue(look.female);
            Assert.AreEqual("Female_Ranger", look.body);
            CollectionAssert.Contains(look.hide, "Female_Ranger_Head_Hood");
            CollectionAssert.AreEquivalent(new[] { "Hair_Long", "Eyebrows_Female" }, look.hair);
            Assert.That(look.hairColor.r, Is.LessThan(0.12f));
            CollectionAssert.AreEquivalent(new[] { "Yi_Bow", "Khan_Quiver", "Tomoe_Sode_L", "Tomoe_Sode_R" }, look.props);
            Assert.AreEqual("hand_l", Art3DProps.Bones["Yi_Bow"]);
            Assert.AreEqual("spine_03", Art3DProps.Bones["Khan_Quiver"]);
            Assert.AreEqual("upperarm_l", Art3DProps.Bones["Tomoe_Sode_L"]);
            Assert.AreEqual("upperarm_r", Art3DProps.Bones["Tomoe_Sode_R"]);
        }

        [Test]
        public void WuzetianLookUsesFemalePeasantRedRobeCrownAndScroll()
        {
            Art3DCharacters.Look look = Art3DCharacters.Heroes.Single(item => item.name == "Wuzetian");
            Assert.IsTrue(look.female);
            Assert.AreEqual("Female_Peasant", look.body);
            CollectionAssert.AreEquivalent(new[] { "Hair_Buns", "Eyebrows_Female" }, look.hair);
            CollectionAssert.DoesNotContain(look.goldParts ?? new string[0], "Female_Peasant_Body", "같은 몸의 선덕여왕(금빛 윗옷)과 구분 — 짙은 붉은 옷");
            Assert.Greater(look.tint.r, look.tint.g * 2f);
            Assert.That(look.hairColor.r, Is.LessThan(0.12f));
            CollectionAssert.AreEquivalent(new[] { "WuZetian_Crown", "Davinci_Scroll" }, look.props);
            Assert.AreEqual("Head", Art3DProps.Bones["WuZetian_Crown"]);
            Assert.AreEqual("hand_l", Art3DProps.Bones["Davinci_Scroll"]);
        }

        [Test]
        public void CurieLookUsesFemalePeasantNavyAndFlask()
        {
            Art3DCharacters.Look look = Art3DCharacters.Heroes.Single(item => item.name == "Curie");
            Assert.IsTrue(look.female);
            Assert.AreEqual("Female_Peasant", look.body);
            CollectionAssert.AreEquivalent(new[] { "Hair_Buns", "Eyebrows_Female" }, look.hair);
            Assert.That(look.tint.b, Is.GreaterThan(look.tint.r));
            CollectionAssert.AreEquivalent(new[] { "Curie_Flask" }, look.props);
            Assert.AreEqual("hand_l", Art3DProps.Bones["Curie_Flask"]);
        }
    }
}
