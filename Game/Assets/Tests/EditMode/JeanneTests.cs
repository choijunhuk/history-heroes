using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>잔 다르크의 깃발, 전용 스킬, 해금과 장면 빌더 결과를 확인한다.</summary>
    public sealed class JeanneTests
    {
        private HeroData _hero;
        private JeanneBannerState _banner;

        [SetUp]
        public void SetUp()
        {
            _hero = ScriptableObject.CreateInstance<HeroData>();
            _hero.id = "jeanne";
            _hero.bannerDeploySeconds = 1f;
            _hero.bannerRadius = 2.5f;
            _hero.bannerDuration = 6f;
            _hero.bannerAttackBonus = 0.2f;
            _hero.bannerDamageReduction = 0.1f;
            _hero.star3BannerDuration = 8f;
            _hero.star5BannerAttackBonus = 0.3f;
            _banner = new JeanneBannerState();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hero);

        private static HeroRoster Roster() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<HeroRoster>("Assets/Data/Heroes/HeroRoster.asset");

        private static SkillDatabase Database() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");

        [Test]
        public void RosterContainsSixteenHeroesIncludingJeanneSeondeokBoudicaAndFourGods()
        {
            HeroRoster roster = Roster();
            Assert.IsNotNull(roster, "장면 빌더가 만든 영웅 명단");
            CollectionAssert.AreEqual(new[]
            {
                "khan", "guan", "napoleon", "tesla", "yisunsin", "yiseonggye", "davinci", "zhuge", "jeanne", "seondeok", "boudica", "thor", "cleopatra", "anubis", "susanoo", "athena"
            }, roster.heroes.ConvertAll(hero => hero.id));
            Assert.AreEqual("jeanne", roster.Find("jeanne").id, "없는 ID를 기본 영웅으로 돌려주는 오류도 잡는다");
        }

        [Test]
        public void JeanneUsesWarriorStatsAndGemsOnlyUnlock()
        {
            HeroData hero = Roster().Find("jeanne");
            Assert.AreEqual("jeanne", hero.id);
            Assert.AreEqual(HeroClass.Warrior, hero.heroClass);
            Assert.AreEqual(BasicAttackKind.MeleeArc, hero.attackKind);
            Assert.AreEqual(1200f, hero.maxHp);
            Assert.AreEqual(105f, hero.attack);
            Assert.AreEqual(0.9f, hero.attackSpeed);
            Assert.AreEqual(2.2f, hero.range);
            Assert.IsTrue(hero.gemsOnly);
            Assert.AreEqual(2000, hero.unlockGems);
            Assert.IsNotNull(hero.model3D, "여자 3D 영웅 프리팹");
            Assert.AreEqual(0.1f, hero.bannerDamageReduction, 1e-5f);
        }

        [Test]
        public void JeanneCannotUnlockByChapterAndCostsExactlyTwoThousandGems()
        {
            HeroData hero = Roster().Find("jeanne");
            var save = new SaveData { unlockedChapter = 9 };
            Assert.IsFalse(HeroUnlock.IsUnlocked(save, hero));
            Wallet.AddFreeGems(save, 1999);
            Assert.IsFalse(HeroUnlock.TryBuy(save, hero));
            Wallet.AddFreeGems(save, 1);
            Assert.IsTrue(HeroUnlock.TryBuy(save, hero));
            Assert.AreEqual(0, Wallet.Gems(save));
        }

        [Test]
        public void JeanneIsEligibleForSingleFreeGemHeroPickAfterTenRuns()
        {
            HeroData hero = Roster().Find("jeanne");
            var save = new SaveData { dungeonRuns = 9 };
            Assert.IsFalse(HeroUnlock.CanClaimFree(save, hero));
            save.dungeonRuns = HeroUnlock.FreePickRuns;
            Assert.IsTrue(HeroUnlock.CanClaimFree(save, hero));
            Assert.IsTrue(HeroUnlock.TryClaimFree(save, hero));
            Assert.IsTrue(HeroUnlock.IsUnlocked(save, hero));
            Assert.IsFalse(HeroUnlock.CanClaimFree(save, Roster().Find("davinci")), "무료 선택은 계정당 한 명");
        }

        [Test]
        public void ExclusiveWaveAppearsOnlyForJeanne()
        {
            HeroData hero = Roster().Find("jeanne");
            ActiveSkillData wave = hero.exclusiveActive;
            Assert.IsNotNull(wave);
            Assert.AreEqual("jeanne_wave", wave.id);
            Assert.AreEqual("jeanne", wave.heroOnlyId);
            Assert.AreEqual(5, wave.maxLevel);
            Assert.AreEqual(SkillRole.Area, wave.role);
            Assert.IsNotNull(wave.behaviourPrefab);
            CollectionAssert.Contains(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { wave }, "jeanne").ConvertAll(c => c.Skill), wave);
            CollectionAssert.IsEmpty(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { wave }, "guan"));
        }

        [Test]
        public void OrleansRecipeNeedsJeanneWaveAtLevelFiveAndDefense()
        {
            SkillDatabase db = Database();
            Assert.IsNotNull(db);
            FusionRecipe recipe = db.FindRecipe("jeanne_orleans");
            Assert.IsNotNull(recipe);
            Assert.AreEqual(FusionType.Evolution, recipe.type);
            Assert.AreEqual("jeanne", recipe.heroOnlyId);
            Assert.AreEqual("jeanne_wave", recipe.materialA.id);
            Assert.AreEqual(5, recipe.requiredLevelA);
            Assert.AreEqual("def_up", recipe.materialB.id);
            Assert.AreEqual(1, recipe.requiredLevelB);
            Assert.AreEqual("jeanne_orleans", recipe.result.id);
            Assert.AreEqual(SkillRole.Area, recipe.result.role);
            Assert.IsNotNull(recipe.result.behaviourPrefab);
            CollectionAssert.IsEmpty(db.FindProblems(), "추가 스킬과 조합의 ID 및 참조가 유효해야 한다");

            var loadout = new SkillLoadout();
            loadout.AddNew(recipe.materialA, 4);
            loadout.AddNew(recipe.materialB);
            CollectionAssert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "jeanne"));
            loadout.Find(recipe.materialA).Level = 5;
            CollectionAssert.Contains(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "jeanne"), recipe);
            CollectionAssert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "guan"));
        }

        [Test]
        public void BannerStarsChangeDurationAndBasicAttackBonusOnlyAtSpecifiedStars()
        {
            HeroSignature one = HeroSignature.For(_hero, 1);
            HeroSignature two = HeroSignature.For(_hero, 2);
            HeroSignature three = HeroSignature.For(_hero, 3);
            HeroSignature four = HeroSignature.For(_hero, 4);
            HeroSignature five = HeroSignature.For(_hero, 5);
            Assert.AreEqual(6f, one.BannerDuration);
            Assert.AreEqual(one.BannerDuration, two.BannerDuration);
            Assert.AreEqual(8f, three.BannerDuration);
            Assert.AreEqual(three.BannerDuration, four.BannerDuration);
            Assert.AreEqual(0.2f, one.BannerAttackBonus);
            Assert.AreEqual(one.BannerAttackBonus, four.BannerAttackBonus);
            Assert.AreEqual(0.3f, five.BannerAttackBonus);
            StringAssert.Contains("★3 깃발 지속 8초", HeroSignature.StarText(_hero));
            StringAssert.Contains("★5 깃발 안 기본 공격력 +30%", HeroSignature.StarText(_hero));
        }

        [TestCase(1, 6f)]
        [TestCase(3, 8f)]
        [TestCase(5, 8f)]
        public void BannerLifetimeUsesAwakenedDuration(int stars, float duration)
        {
            HeroSignature sig = HeroSignature.For(_hero, stars);
            Assert.IsTrue(_banner.Tick(1f, true, Vector2.zero, _hero, sig));
            Assert.AreEqual(duration, _banner.TimeLeft);
            Assert.IsTrue(_banner.Tick(duration - 0.5f, true, Vector2.zero, _hero, sig));
            Assert.IsFalse(_banner.Tick(0.5f, true, Vector2.zero, _hero, sig));
            Assert.IsFalse(_banner.IsPlanted);
        }

        [Test]
        public void ZeroOrNegativeTimeCannotAdvanceDeploymentOrExtendLifetime()
        {
            HeroSignature sig = HeroSignature.For(_hero, 1);
            Assert.IsFalse(_banner.Tick(0f, true, Vector2.zero, _hero, sig));
            Assert.IsFalse(_banner.Tick(-1f, true, Vector2.zero, _hero, sig));
            Assert.IsTrue(_banner.Tick(1f, true, Vector2.zero, _hero, sig));
            Assert.IsTrue(_banner.Tick(0f, true, Vector2.zero, _hero, sig));
            Assert.IsTrue(_banner.Tick(-1f, true, Vector2.zero, _hero, sig));
            Assert.AreEqual(6f, _banner.TimeLeft);
        }

        [Test]
        public void BannerRequiresOneContinuousStoppedSecond()
        {
            HeroSignature sig = HeroSignature.For(_hero, 1);
            Assert.IsFalse(_banner.Tick(0.6f, true, Vector2.zero, _hero, sig));
            Assert.IsFalse(_banner.Tick(0.1f, false, Vector2.zero, _hero, sig));
            Assert.IsFalse(_banner.Tick(0.9f, true, Vector2.zero, _hero, sig));
            Assert.IsFalse(_banner.IsPlanted);
            Assert.IsTrue(_banner.Tick(0.1f, true, Vector2.zero, _hero, sig));
            Assert.IsTrue(_banner.IsPlanted);
        }

        [Test]
        public void MovingDisablesBannerBonusButReturningInsideRestoresIt()
        {
            HeroSignature sig = HeroSignature.For(_hero, 1);
            Assert.IsTrue(_banner.Tick(1f, true, Vector2.zero, _hero, sig));
            Assert.IsFalse(_banner.Tick(0.2f, false, new Vector2(4f, 0f), _hero, sig));
            Assert.IsTrue(_banner.IsPlanted);
            Assert.AreEqual(Vector2.zero, _banner.Center, "깃발은 따라오지 않는다");
            Assert.IsFalse(_banner.Tick(0.2f, true, new Vector2(4f, 0f), _hero, sig));
            Assert.IsTrue(_banner.Tick(0.2f, true, new Vector2(2.5f, 0f), _hero, sig));
        }

        [Test]
        public void BannerExpiresAndNeedsFreshOneSecondToReplant()
        {
            HeroSignature sig = HeroSignature.For(_hero, 1);
            Assert.IsTrue(_banner.Tick(1f, true, Vector2.zero, _hero, sig));
            Assert.IsFalse(_banner.Tick(6f, true, Vector2.zero, _hero, sig));
            Assert.IsFalse(_banner.IsPlanted);
            Assert.IsFalse(_banner.Tick(0.9f, true, Vector2.zero, _hero, sig));
            Assert.IsTrue(_banner.Tick(0.1f, true, Vector2.zero, _hero, sig));
        }

        [Test]
        public void ResetRemovesBannerAndItsBonus()
        {
            HeroSignature sig = HeroSignature.For(_hero, 1);
            Assert.IsTrue(_banner.Tick(1f, true, Vector2.zero, _hero, sig));
            _banner.Reset();
            Assert.IsFalse(_banner.IsPlanted);
            Assert.IsFalse(_banner.Tick(0f, true, Vector2.zero, _hero, sig));
        }

        [Test]
        public void NonJeanneHeroDoesNotPlantBanner()
        {
            var other = ScriptableObject.CreateInstance<HeroData>();
            try
            {
                Assert.IsFalse(_banner.Tick(20f, true, Vector2.zero, other, HeroSignature.For(other, 5)));
                Assert.IsFalse(_banner.IsPlanted);
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }
    }
}
