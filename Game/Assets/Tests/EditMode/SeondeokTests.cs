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
    /// <summary>장면 빌더 재생성 뒤 영웅·해금·스킬 연결을 확인한다. 전투 장면은 열지 않는다.</summary>
    public sealed class SeondeokTests
    {
        private static HeroRoster Roster() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<HeroRoster>("Assets/Data/Heroes/HeroRoster.asset");

        private static SkillDatabase Database() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");

        private static HeroData Hero()
        {
            HeroRoster roster = Roster();
            Assert.IsNotNull(roster, "장면 빌더를 먼저 실행한다");
            HeroData hero = roster.Find("seondeok");
            Assert.IsNotNull(hero);
            Assert.AreEqual("seondeok", hero.id, "없는 ID가 기본 영웅으로 대체되어도 실패해야 한다");
            return hero;
        }

        [Test]
        public void RosterContainsExactlyFourteenHeroesAndMissionOrderMatches()
        {
            HeroRoster roster = Roster();
            Assert.IsNotNull(roster);
            string[] ids = { "khan", "guan", "napoleon", "tesla", "yisunsin", "yiseonggye", "davinci", "zhuge", "jeanne", "seondeok", "boudica", "thor", "cleopatra", "anubis" };
            CollectionAssert.AreEqual(ids, roster.heroes.ConvertAll(hero => hero.id));
            var missions = ScriptableObject.CreateInstance<MissionTable>();
            try { CollectionAssert.AreEqual(ids, missions.heroOrder); }
            finally { Object.DestroyImmediate(missions); }
        }

        [Test]
        public void SeondeokUsesMageStatsAndObservationInsteadOfChainOrCharge()
        {
            HeroData hero = Hero();
            Assert.AreEqual("선덕여왕", hero.displayName);
            Assert.AreEqual(HeroClass.Mage, hero.heroClass);
            Assert.AreEqual(BasicAttackKind.Projectile, hero.attackKind);
            Assert.AreEqual(900f, hero.maxHp);
            Assert.AreEqual(90f, hero.attack);
            Assert.AreEqual(1f, hero.attackSpeed);
            Assert.AreEqual(5f, hero.moveSpeed);
            Assert.AreEqual(6f, hero.range);
            Assert.AreEqual(1f, hero.observationSeconds);
            Assert.AreEqual(0f, hero.observationAttackBonus);
            Assert.AreEqual(0.15f, hero.star3ObservationAttackBonus, 1e-5f);
            Assert.AreEqual(0.3f, hero.star5ObservationAttackBonus, 1e-5f);
            Assert.AreEqual(0, hero.chainBounces);
            Assert.IsFalse(hero.chargeShot);
            Assert.IsNotNull(hero.projectilePrefab);
            Assert.IsNotNull(hero.model3D, "3D 빌더를 장면 빌더보다 먼저 실행한다");
        }

        [Test]
        public void UnlockCostsTwoThousandGemsAndCannotBeBypassedByChapter()
        {
            HeroData hero = Hero();
            Assert.IsTrue(hero.gemsOnly);
            Assert.AreEqual(2000, hero.unlockGems);
            var save = new SaveData { unlockedChapter = 99 };
            Assert.IsFalse(HeroUnlock.IsUnlocked(save, hero));
            Wallet.AddFreeGems(save, 1999);
            Assert.IsFalse(HeroUnlock.TryBuy(save, hero));
            Assert.AreEqual(1999, Wallet.Gems(save));
            Wallet.AddFreeGems(save, 1);
            Assert.IsTrue(HeroUnlock.TryBuy(save, hero));
            Assert.AreEqual(0, Wallet.Gems(save));
            Assert.IsTrue(HeroUnlock.IsUnlocked(save, hero));
            StringAssert.Contains("보석 2,000", HeroUnlock.Condition(hero));
        }

        [Test]
        public void FreePickAtTenRunsRemainsOneChoiceAcrossAllGemHeroes()
        {
            HeroData hero = Hero();
            var save = new SaveData { dungeonRuns = 9 };
            Assert.IsFalse(HeroUnlock.CanClaimFree(save, hero));
            save.dungeonRuns = 10;
            Assert.IsTrue(HeroUnlock.TryClaimFree(save, hero));
            Assert.IsTrue(HeroUnlock.IsUnlocked(save, hero));
            foreach (string id in new[] { "davinci", "zhuge", "jeanne", "seondeok", "boudica" })
                Assert.IsFalse(HeroUnlock.CanClaimFree(save, Roster().Find(id)), id);

            var alreadyPicked = new SaveData { dungeonRuns = 100, freeHeroClaimed = true };
            Assert.IsFalse(HeroUnlock.TryClaimFree(alreadyPicked, hero), "업데이트로 무료 선택을 다시 주지 않는다");
        }

        [Test]
        public void StarlightIsHeroOnlyAndHasExpectedLevelsAndBehaviour()
        {
            ActiveSkillData skill = Hero().exclusiveActive;
            Assert.IsNotNull(skill);
            Assert.AreEqual("seondeok_starlight", skill.id);
            Assert.AreEqual("seondeok", skill.heroOnlyId);
            Assert.AreEqual(5, skill.maxLevel);
            Assert.AreEqual(Rarity.Rare, skill.rarity);
            Assert.AreEqual(SkillRole.Area, skill.role);
            Assert.AreEqual(8f, skill.cooldown);
            Assert.AreEqual(2f, skill.radius);
            Assert.AreEqual(1.4f, skill.DamageAt(1), 1e-5f);
            Assert.AreEqual(2.4f, skill.DamageAt(5), 1e-5f);
            Assert.IsInstanceOf<ConstellationBehaviour>(skill.behaviourPrefab);
            foreach (HeroData hero in Roster().heroes)
            {
                var candidates = SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { skill }, hero.id);
                Assert.AreEqual(hero.id == "seondeok" ? 1 : 0, candidates.Count, hero.id);
            }
        }

        [Test]
        public void ConstellationNeedsHerLevelFiveActiveAndPierce()
        {
            SkillDatabase db = Database();
            Assert.IsNotNull(db);
            FusionRecipe recipe = db.FindRecipe("seondeok_constellation");
            Assert.IsNotNull(recipe);
            Assert.AreEqual(FusionType.Evolution, recipe.type);
            Assert.AreEqual("seondeok", recipe.heroOnlyId);
            Assert.AreEqual("seondeok_starlight", recipe.materialA.id);
            Assert.AreEqual(5, recipe.requiredLevelA);
            Assert.AreEqual("pierce", recipe.materialB.id);
            Assert.AreEqual(1, recipe.requiredLevelB);
            Assert.AreSame(Hero().exclusiveActive, recipe.materialA);

            var loadout = new SkillLoadout();
            loadout.AddNew(recipe.materialB);
            CollectionAssert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "seondeok"));
            loadout.AddNew(recipe.materialA, 4);
            CollectionAssert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "seondeok"));
            loadout.Find(recipe.materialA).Level = 5;
            CollectionAssert.Contains(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "seondeok"), recipe);
            foreach (HeroData hero in Roster().heroes)
                if (hero.id != "seondeok")
                    CollectionAssert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, hero.id), hero.id);

            var noPassive = new SkillLoadout();
            noPassive.AddNew(recipe.materialA, 5);
            CollectionAssert.IsEmpty(FusionFinder.FindAvailableFusions(noPassive, new[] { recipe }, "seondeok"));

            ActiveSkillData result = recipe.result;
            Assert.AreEqual("seondeok_constellation", result.id);
            Assert.AreEqual("seondeok", result.heroOnlyId);
            Assert.AreEqual(3, result.maxLevel);
            Assert.AreEqual(12, result.saveCostFirstLevel);
            Assert.AreEqual(4, result.saveCostPerLevel);
            Assert.AreEqual(8f, result.cooldown);
            Assert.AreEqual(3, result.CountAt(1));
            Assert.AreEqual(1.4f, result.RadiusAt(1));
            Assert.AreEqual(0.9f, result.DamageAt(1), 1e-5f);
            Assert.AreEqual(1.35f, result.DamageAt(3), 1e-5f);
            Assert.AreEqual(SkillRole.Area, result.role);
            Assert.IsInstanceOf<ConstellationBehaviour>(result.behaviourPrefab);
            CollectionAssert.Contains(db.skills, result);
            CollectionAssert.IsEmpty(db.FindProblems());
        }

        [Test]
        public void EveryStoryMomentHasASeondeokLine()
        {
            foreach (LineMoment moment in System.Enum.GetValues(typeof(LineMoment)))
                Assert.IsNotEmpty(StoryLines.For("seondeok", moment), moment.ToString());
        }

        [Test]
        public void ConstellationPointsStayOnTheCastLineAroundTheFixedTarget()
        {
            Vector2 center = new Vector2(4f, 5f);
            Assert.AreEqual(center, ConstellationBehaviour.StarPosition(center, Vector2.right, 0, 1, 1.2f));
            Assert.That(Vector2.Distance(new Vector2(2.8f, 5f), ConstellationBehaviour.StarPosition(center, Vector2.right, 0, 3, 1.2f)), Is.LessThan(1e-5f));
            Assert.AreEqual(center, ConstellationBehaviour.StarPosition(center, Vector2.right, 1, 3, 1.2f));
            Assert.That(Vector2.Distance(new Vector2(5.2f, 5f), ConstellationBehaviour.StarPosition(center, Vector2.right, 2, 3, 1.2f)), Is.LessThan(1e-5f));
            Assert.That(Vector2.Distance(new Vector2(4f, 6.2f), ConstellationBehaviour.StarPosition(center, Vector2.up * 4f, 2, 3, 1.2f)), Is.LessThan(1e-5f));
        }

        [Test]
        public void ConstellationLevelOneOutdamagesItsMaterialOnAStationaryTarget()
        {
            FusionRecipe recipe = Database().FindRecipe("seondeok_constellation");
            Assert.IsNotNull(recipe);
            ActiveSkillData result = recipe.result;
            Assert.IsInstanceOf<ConstellationBehaviour>(result.behaviourPrefab);
            var settings = new UnityEditor.SerializedObject(result.behaviourPrefab);
            float spacing = settings.FindProperty("starSpacing").floatValue;
            Assert.AreEqual(1.2f, spacing, 1e-5f);
            Assert.AreEqual(0.25f, settings.FindProperty("starInterval").floatValue, 1e-5f);
            float total = 0f;
            for (int i = 0; i < result.CountAt(1); i++)
            {
                Vector2 point = ConstellationBehaviour.StarPosition(Vector2.zero, Vector2.right, i, result.CountAt(1), spacing);
                if (point.sqrMagnitude <= result.radius * result.radius) total += result.DamageAt(1);
            }

            Assert.AreEqual(2.7f, total, 1e-5f, "중앙의 적이 움직이지 않으면 세 별이 모두 맞아야 한다");
            Assert.Greater(total, recipe.materialA.DamageAt(5), "진화가 원래 액티브보다 약해지지 않는다");
        }
    }
}
