using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Heroes;
using HistoryHeroes.Player;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class AthenaCombatTests
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
        public void ShieldNeedsOneSecondAndBlocksExactlyOneEnemyProjectile()
        {
            var state = new AthenaShieldState();
            state.Tick(0.7f, true, 0.7f, 1f);
            Assert.IsFalse(state.IsReady);
            state.Tick(0.3f, true, 1f, 1f);
            Assert.IsTrue(state.IsReady);
            Assert.IsTrue(state.TryBlock(1f, 8f));
            Assert.IsFalse(state.TryBlock(1f, 8f));
            Assert.AreEqual(8f, state.CooldownRemaining(1f), 0.0001f);
            state.Tick(20f, true, 8.9f, 1f);
            Assert.IsFalse(state.IsReady, "정지를 반복해도 성공 뒤 8초가 지나기 전에 다시 차단하지 않는다");
            state.Tick(0.9f, true, 9.9f, 1f);
            Assert.IsFalse(state.IsReady);
            state.Tick(0.1f, true, 10f, 1f);
            Assert.IsTrue(state.IsReady);
        }

        [Test]
        public void MovementCancelsPreparationButPreservesReuseCooldown()
        {
            var state = new AthenaShieldState();
            state.Tick(1f, true, 1f, 1f);
            Assert.IsTrue(state.IsReady);
            state.Tick(0f, false, 1.1f, 1f);
            Assert.IsFalse(state.IsReady);
            Assert.IsFalse(state.TryBlock(1.1f, 8f));
            state.Tick(1f, true, 2.1f, 1f);
            Assert.IsTrue(state.TryBlock(2.1f, 8f));
            state.CancelPreparation();
            state.Tick(1f, true, 3.1f, 1f);
            Assert.IsFalse(state.IsReady);
            Assert.AreEqual(7f, state.CooldownRemaining(3.1f), 0.0001f);
        }

        [Test]
        public void SpearOnlyHitsShortStraightPath()
        {
            Vector2 origin = Vector2.zero;
            Vector2 aim = Vector2.right;
            Assert.IsTrue(PlayerController.InSpearPath(origin, aim, new Vector2(2f, 0.2f), 2.5f, 0.6f));
            Assert.IsFalse(PlayerController.InSpearPath(origin, aim, new Vector2(2f, 0.4f), 2.5f, 0.6f));
            Assert.IsFalse(PlayerController.InSpearPath(origin, aim, new Vector2(3f, 0f), 2.5f, 0.6f));
            Assert.IsFalse(PlayerController.InSpearPath(origin, aim, new Vector2(-1f, 0f), 2.5f, 0.6f));
        }

        [TestCase(0, 2.5f)]
        [TestCase(1, 3.5f)]
        [TestCase(3, 3.5f)]
        public void PierceExtendsSpearReachOnlyOnce(int pierce, float expected)
        {
            Assert.AreEqual(expected, PlayerController.SpearReach(2.5f, pierce, 0.4f), 0.0001f);
        }

        [Test]
        public void PierceReachesRearEnemyWithoutWideningSpear()
        {
            Vector2 origin = Vector2.zero;
            Vector2 aim = Vector2.right;
            float reach = PlayerController.SpearReach(2.5f, 1, 0.4f);
            Assert.IsFalse(PlayerController.InSpearPath(origin, aim, new Vector2(3f, 0f), 2.5f, 0.6f));
            Assert.IsTrue(PlayerController.InSpearPath(origin, aim, new Vector2(3f, 0f), reach, 0.6f));
            Assert.IsFalse(PlayerController.InSpearPath(origin, aim, new Vector2(3f, 0.4f), reach, 0.6f));
            Assert.IsFalse(PlayerController.InSpearPath(origin, aim, new Vector2(3.6f, 0f), reach, 0.6f));
        }

        [TestCase(2.5f, 0f, true)]
        [TestCase(2.5001f, 0f, false)]
        [TestCase(2.5f, 0.2f, false)]
        [TestCase(2.4f, 0.2f, true)]
        public void SpearAndTargetSelectionUseTheSameCenterRange(float x, float y, bool expected)
        {
            Vector2 origin = new Vector2(1f, -2f);
            Vector2 point = origin + new Vector2(x, y);
            Assert.AreEqual(expected, TargetSelector.IsWithinRange(origin, point, 2.5f));
            Assert.AreEqual(expected, PlayerController.InSpearPath(origin, Vector2.right, point, 2.5f, 0.6f));
        }

        [Test]
        public void SpearSkipsOutOfRangeCenterWhileProjectileStillTargetsColliderEdge()
        {
            const int TestLayer = 29;
            var enemyObject = new GameObject("SpearRangeEdge") { layer = TestLayer };
            try
            {
                enemyObject.transform.position = new Vector3(2.75f, 0f, 0f);
                enemyObject.AddComponent<CircleCollider2D>().radius = 0.5f;
                Health enemy = enemyObject.AddComponent<Health>();
                enemy.Init(Team.Enemy, 100f);
                Physics2D.SyncTransforms();

                var selector = new TargetSelector(1 << TestLayer, 0);
                Assert.AreSame(enemy, selector.Find(Vector2.zero, 2.5f));
                Assert.IsNull(selector.Find(Vector2.zero, 2.5f, requireCenterInRange: true),
                    "창은 충돌체 끝만 닿은 적을 계속 겨냥하지 않는다");
                Assert.AreSame(enemy, selector.Find(Vector2.zero, 3.5f, requireCenterInRange: true),
                    "관통으로 늘어난 사거리에서는 같은 적을 고를 수 있다");
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void OwlMarksExpireOnPooledLifeAndOnlyBasicAttacksGainBonus()
        {
            var marks = new AthenaOwlState();
            object first = new object(), second = new object(), third = new object(), fourth = new object();
            marks.Begin(new object());
            marks.Add(first, 1, 0f, 3f, 1);
            Assert.AreEqual(0.2f, marks.BonusFor(first, 1, 2f, DamageKind.BasicAttack, 0.2f), 0.0001f);
            Assert.AreEqual(0f, marks.BonusFor(first, 1, 2f, DamageKind.Active, 0.2f));
            Assert.AreEqual(0f, marks.BonusFor(first, 2, 2f, DamageKind.BasicAttack, 0.2f));
            Assert.AreEqual(0f, marks.BonusFor(first, 1, 3f, DamageKind.BasicAttack, 0.2f));
            marks.Clear();
            marks.Add(first, 1, 4f, 3f, 3);
            marks.Add(second, 1, 4f, 3f, 3);
            marks.Add(third, 1, 4f, 3f, 3);
            marks.Add(fourth, 1, 4f, 3f, 3);
            Assert.AreEqual(3, marks.Count);
            Assert.IsFalse(marks.IsMarked(fourth, 1, 5f));
            Assert.IsTrue(marks.IsMarked(third, 1, 5f));
        }

        [Test]
        public void LateDisposalOfOldEyeDoesNotClearNewFusionMarks()
        {
            var marks = new AthenaOwlState();
            object oldEye = new object(), newGaze = new object(), enemy = new object();
            marks.Begin(oldEye);
            marks.Add(enemy, 1, 0f, 3f, 1);
            marks.Begin(newGaze);
            marks.Add(enemy, 1, 1f, 3f, 3);
            marks.ClearFor(oldEye);
            Assert.IsTrue(marks.IsMarked(enemy, 1, 2f));
            marks.ClearFor(newGaze);
            Assert.IsFalse(marks.IsMarked(enemy, 1, 2f));
        }

        [Test]
        public void AthenaSkillsAndStarValuesKeepHumanGrowthLimits()
        {
            HeroData hero = Make<HeroData>();
            AthenaDefinition.ConfigureHero(hero);
            Assert.AreEqual("athena", hero.id);
            Assert.AreEqual(HeroClass.Warrior, hero.heroClass);
            Assert.AreEqual(BasicAttackKind.MeleeThrust, hero.attackKind);
            Assert.AreEqual(1250f, hero.maxHp);
            Assert.AreEqual(108f, hero.attack);
            Assert.AreEqual(2.5f, hero.range);
            Assert.AreEqual(3.5f, PlayerController.SpearReach(hero.range, 1, hero.spearPierceReachBonus), 0.0001f);
            Assert.IsTrue(hero.godShardUnlock);
            Assert.AreEqual(1f, HeroSignature.For(hero, 1).ShieldPrepareSeconds, 0.0001f);
            Assert.AreEqual(0.8f, HeroSignature.For(hero, 3).ShieldPrepareSeconds, 0.0001f);
            Assert.AreEqual(0.2f, HeroSignature.For(hero, 1).OwlBonusCoefficient, 0.0001f);
            Assert.AreEqual(0.25f, HeroSignature.For(hero, 5).OwlBonusCoefficient, 0.0001f);

            ActiveSkillData eye = Make<ActiveSkillData>();
            ActiveSkillData gaze = Make<ActiveSkillData>();
            AthenaDefinition.ConfigureOwlEye(eye);
            AthenaDefinition.ConfigureGuardianGaze(gaze);
            PassiveSkillData defense = Make<PassiveSkillData>();
            defense.id = "def_up";
            FusionRecipe recipe = Make<FusionRecipe>();
            AthenaDefinition.ConfigureRecipe(recipe, eye, defense, gaze);
            Assert.AreEqual(10f, eye.cooldown);
            Assert.AreEqual(3f, eye.DurationAt(1));
            Assert.AreEqual(3f, eye.DurationAt(5));
            Assert.AreEqual(5f, eye.RadiusAt(1));
            Assert.AreEqual(7f, eye.RadiusAt(5));
            StringAssert.Contains("표적 거리 5 → 5.5", SkillCardView.UpgradeLine(eye, 1, 2));
            Assert.AreEqual(1, eye.CountAt(5));
            Assert.AreEqual(3, gaze.CountAt(1));
            Assert.AreEqual(0f, gaze.DamageAt(1));
            Assert.AreEqual(5, eye.maxLevel);
            Assert.AreEqual(3, eye.saveCostFirstLevel);
            Assert.AreEqual(3, eye.saveCostPerLevel);
            Assert.AreEqual(1, gaze.maxLevel);
            Assert.AreEqual(10, gaze.saveCostFirstLevel);
            Assert.AreEqual(0, gaze.saveCostPerLevel);
            Assert.AreEqual("athena", eye.heroOnlyId);
            Assert.AreEqual("athena", gaze.heroOnlyId);
            Assert.AreEqual("athena", recipe.heroOnlyId);
            Assert.AreSame(eye, recipe.materialA);
            Assert.AreSame(defense, recipe.materialB);
            Assert.AreEqual(5, recipe.requiredLevelA);
            Assert.AreEqual(1, recipe.requiredLevelB);
            Assert.AreSame(gaze, recipe.result);
            Assert.AreEqual(1, SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { eye }, "athena").Count);
            Assert.IsEmpty(SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { eye }, "thor"));
            var loadout = new SkillLoadout();
            loadout.AddNew(eye, 5);
            loadout.AddNew(defense);
            CollectionAssert.Contains(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "athena"), recipe);
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "thor"));
        }
    }
}
