using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HistoryHeroes.Common;
using HistoryHeroes.Player;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class CardBanTests
    {
        private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
        private readonly List<GameObject> _gameObjects = new List<GameObject>();
        private string _folder;
        private bool _previousTutorial;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "hh-card-ban-" + Guid.NewGuid().ToString("N"));
            _previousTutorial = GameSession.Tutorial;
            SetTutorial(false);
        }

        [TearDown]
        public void TearDown()
        {
            SetTutorial(_previousTutorial);
            foreach (GameObject go in _gameObjects) UnityEngine.Object.DestroyImmediate(go);
            _gameObjects.Clear();
            foreach (ScriptableObject asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private T Make<T>(string id, Rarity rarity = Rarity.Common) where T : SkillData
        {
            var skill = ScriptableObject.CreateInstance<T>();
            skill.id = id;
            skill.rarity = rarity;
            _assets.Add(skill);
            return skill;
        }

        private RarityTable Table(float common, float rare, float legendary)
        {
            var table = ScriptableObject.CreateInstance<RarityTable>();
            table.common = AnimationCurve.Constant(0f, 100f, common);
            table.rare = AnimationCurve.Constant(0f, 100f, rare);
            table.legendary = AnimationCurve.Constant(0f, 100f, legendary);
            _assets.Add(table);
            return table;
        }

        private static SkillCard Card(CardType type, SkillData skill = null) =>
            new SkillCard { Type = type, Skill = skill };

        private static void SetTutorial(bool value)
        {
            PropertyInfo property = typeof(GameSession).GetProperty("Tutorial", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(property);
            property.SetValue(null, value);
        }

        private SkillManager Manager(List<SkillData> pool, RarityTable table)
        {
            var go = new GameObject("CardBanTest");
            go.SetActive(false);
            _gameObjects.Add(go);
            var player = go.AddComponent<PlayerController>();
            var manager = go.AddComponent<SkillManager>();
            SetField(manager, "player", player);
            SetField(manager, "skillPool", pool);
            SetField(manager, "rarityTable", table);
            return manager;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private List<SkillData> CommonPool(int count)
        {
            var pool = new List<SkillData>();
            for (int i = 0; i < count; i++) pool.Add(Make<PassiveSkillData>("skill_" + i));
            return pool;
        }

        [Test]
        public void NewLoadoutStartsWithTwoBans()
        {
            Assert.AreEqual(SkillLoadout.BansPerRun, new SkillLoadout().BansLeft);
        }

        [Test]
        public void BanningNewSkillRemovesItFromCandidates()
        {
            var skill = Make<PassiveSkillData>("atk_up");
            var loadout = new SkillLoadout();

            Assert.IsTrue(loadout.TryBan(Card(CardType.NewSkill, skill)));
            Assert.IsEmpty(SkillPicker.BuildCandidates(loadout, new[] { skill }, "khan"));
        }

        [Test]
        public void BanningOwnedSkillRemovesItsLevelUpCandidate()
        {
            var skill = Make<ActiveSkillData>("spin_blade");
            var loadout = new SkillLoadout();
            loadout.AddNew(skill, 1);

            Assert.IsTrue(loadout.TryBan(Card(CardType.LevelUp, skill)));
            Assert.IsEmpty(SkillPicker.BuildCandidates(loadout, new[] { skill }, "khan"));
            Assert.AreEqual(1, loadout.LevelOf(skill), "이미 배운 스킬의 효과는 남는다");
        }

        [Test]
        public void BanningFusionResultRemovesItsLevelUpCandidateOutsidePool()
        {
            var result = Make<ActiveSkillData>("thunder_fire");
            var loadout = new SkillLoadout();
            loadout.AddNew(result, 1).IsFusionResult = true;
            Assert.AreEqual(1, SkillPicker.BuildCandidates(loadout, Array.Empty<SkillData>(), "khan").Count);

            Assert.IsTrue(loadout.TryBan(Card(CardType.LevelUp, result)));
            Assert.IsEmpty(SkillPicker.BuildCandidates(loadout, Array.Empty<SkillData>(), "khan"));
        }

        [Test]
        public void BannedMaterialPreventsOtherwiseReadyFusion()
        {
            var blade = Make<ActiveSkillData>("spin_blade");
            var defense = Make<PassiveSkillData>("def_up");
            var recipe = ScriptableObject.CreateInstance<FusionRecipe>();
            recipe.id = "steel_whirl";
            recipe.materialA = blade;
            recipe.materialB = defense;
            recipe.result = Make<ActiveSkillData>("steel_whirl");
            recipe.requiredLevelA = 5;
            recipe.requiredLevelB = 1;
            _assets.Add(recipe);
            var loadout = new SkillLoadout();
            loadout.AddNew(blade, 5);
            loadout.AddNew(defense);
            Assert.AreEqual(1, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan").Count);

            Assert.IsTrue(loadout.TryBan(Card(CardType.LevelUp, defense)));
            Assert.IsEmpty(FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan"));
        }

        [Test]
        public void TwoSuccessfulBansExhaustAllowance()
        {
            var loadout = new SkillLoadout();
            var a = Make<PassiveSkillData>("a");
            var b = Make<PassiveSkillData>("b");
            var c = Make<PassiveSkillData>("c");

            Assert.IsTrue(loadout.TryBan(Card(CardType.NewSkill, a)));
            Assert.AreEqual(1, loadout.BansLeft);
            Assert.IsTrue(loadout.TryBan(Card(CardType.NewSkill, b)));
            Assert.AreEqual(0, loadout.BansLeft);
            Assert.IsFalse(loadout.TryBan(Card(CardType.NewSkill, c)));
            Assert.IsFalse(loadout.IsBanned(c));
        }

        [Test]
        public void DuplicateBanDoesNotSpendAllowance()
        {
            var skill = Make<PassiveSkillData>("atk_up");
            var loadout = new SkillLoadout();
            loadout.TryBan(Card(CardType.NewSkill, skill));

            Assert.IsFalse(loadout.TryBan(Card(CardType.LevelUp, skill)));
            Assert.AreEqual(1, loadout.BansLeft);
        }

        [TestCase(CardType.Fusion)]
        [TestCase(CardType.Fallback)]
        [TestCase(CardType.RiftBoost)]
        public void NonSkillCardsCannotBeBanned(CardType type)
        {
            var loadout = new SkillLoadout();
            var skill = Make<PassiveSkillData>("atk_up");

            Assert.IsFalse(loadout.TryBan(Card(type, skill)));
            Assert.AreEqual(2, loadout.BansLeft);
        }

        [Test]
        public void MissingSkillIdCannotBeBanned()
        {
            var loadout = new SkillLoadout();
            var skill = Make<PassiveSkillData>(string.Empty);

            Assert.IsFalse(loadout.TryBan(Card(CardType.NewSkill, skill)));
            Assert.AreEqual(2, loadout.BansLeft);
        }

        [Test]
        public void ReplacementWithInvalidIndexDoesNotSpendAllowance()
        {
            var loadout = new SkillLoadout();
            var current = new List<SkillCard> { Card(CardType.NewSkill, Make<PassiveSkillData>("atk_up")) };

            Assert.IsFalse(SkillPicker.TryBanAndReplace(loadout, Array.Empty<SkillData>(),
                "khan", current, 1, 1, null));
            Assert.AreEqual(2, loadout.BansLeft);
        }

        [Test]
        public void ReplacementExcludesAllShownSkillIds()
        {
            var banned = Make<PassiveSkillData>("banned");
            var shown = Make<PassiveSkillData>("shown");
            var replacement = Make<PassiveSkillData>("replacement");
            var current = new List<SkillCard> { Card(CardType.NewSkill, banned), Card(CardType.NewSkill, shown) };
            var loadout = new SkillLoadout();

            Assert.IsTrue(SkillPicker.TryBanAndReplace(loadout, new SkillData[] { banned, shown, replacement },
                "khan", current, 0, 1, Table(100, 0, 0)));
            Assert.AreSame(replacement, current[0].Skill);
            Assert.IsTrue(loadout.IsBanned(banned));
        }

        [Test]
        public void ReplacementKeepsOtherDisplayedCardObject()
        {
            var banned = Make<PassiveSkillData>("banned");
            var shown = Card(CardType.NewSkill, Make<PassiveSkillData>("shown"));
            var replacement = Make<PassiveSkillData>("replacement");
            var current = new List<SkillCard> { Card(CardType.NewSkill, banned), shown };

            Assert.IsTrue(SkillPicker.TryBanAndReplace(new SkillLoadout(), new SkillData[] { banned, shown.Skill, replacement },
                "khan", current, 0, 1, Table(100, 0, 0)));
            Assert.AreSame(shown, current[1]);
        }

        [Test]
        public void ReplacementUsesFallbackWhenNoEligibleSkillRemains()
        {
            var skill = Make<PassiveSkillData>("only_skill");
            var current = new List<SkillCard> { Card(CardType.NewSkill, skill) };

            Assert.IsTrue(SkillPicker.TryBanAndReplace(new SkillLoadout(), new[] { skill },
                "khan", current, 0, 1, Table(100, 0, 0)));
            Assert.AreEqual(CardType.Fallback, current[0].Type);
        }

        [Test]
        public void ReplacementNeverPromotesCommonRollToLegendary()
        {
            var banned = Make<PassiveSkillData>("banned");
            var legendary = Make<ActiveSkillData>("arrow_rain", Rarity.Legendary);
            var current = new List<SkillCard> { Card(CardType.NewSkill, banned) };

            Assert.IsTrue(SkillPicker.TryBanAndReplace(new SkillLoadout(), new SkillData[] { banned, legendary },
                "khan", current, 0, 1, Table(100, 0, 0)));
            Assert.AreEqual(CardType.Fallback, current[0].Type);
        }

        [Test]
        public void DetourOfferBanLeavesNormalCardRandomStreamUntouched()
        {
            SkillManager manager = Manager(CommonPool(6), Table(100, 0, 0));
            manager.DetourOffersNext = 1;
            RunRandom.Begin(341);
            List<SkillCard> offer = manager.CreateOffer(1);
            List<string> before = RunRandom.Capture();

            Assert.IsTrue(manager.TryBanCard(offer, 0, 1));
            List<string> after = RunRandom.Capture();

            Assert.AreEqual(before[2], after[2], "특수 방 빼기는 일반 카드 난수를 밀지 않는다");
            Assert.AreNotEqual(before[3], after[3], "교체 카드는 갈림길 난수에서 뽑는다");
        }

        [Test]
        public void NormalOfferBanConsumesOnlyNormalCardRandomStream()
        {
            SkillManager manager = Manager(CommonPool(6), Table(100, 0, 0));
            RunRandom.Begin(342);
            List<SkillCard> offer = manager.CreateOffer(1);
            List<string> before = RunRandom.Capture();

            Assert.IsTrue(manager.TryBanCard(offer, 0, 1));
            List<string> after = RunRandom.Capture();

            Assert.AreNotEqual(before[2], after[2], "일반 레벨업 빼기는 일반 카드 난수를 쓴다");
            Assert.AreEqual(before[3], after[3], "갈림길 난수는 그대로 둔다");
        }

        [Test]
        public void SnapshotJsonRoundTripKeepsBannedIdsAndRemainingCount()
        {
            var snapshot = new RunSnapshot { bannedSkillIds = new List<string> { "atk_up", "spin_blade" }, bansLeft = 0 };

            RunSnapshot restored = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(snapshot));

            CollectionAssert.AreEquivalent(snapshot.bannedSkillIds, restored.bannedSkillIds);
            Assert.AreEqual(0, restored.bansLeft);
        }

        [Test]
        public void RestoredBansExcludeSameCandidates()
        {
            var banned = Make<PassiveSkillData>("atk_up");
            var allowed = Make<PassiveSkillData>("def_up");
            var snapshot = new RunSnapshot { bannedSkillIds = new List<string> { banned.id }, bansLeft = 1 };
            RunSnapshot restored = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(snapshot));
            var loadout = new SkillLoadout();
            loadout.RestoreBans(restored.bannedSkillIds, restored.bansLeft);

            List<SkillCard> candidates = SkillPicker.BuildCandidates(loadout, new SkillData[] { banned, allowed }, "khan");
            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(allowed, candidates[0].Skill);
        }

        [Test]
        public void ZeroRemainingBansSurvivesJsonAndRestore()
        {
            var snapshot = new RunSnapshot { bansLeft = 0 };
            RunSnapshot restored = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(snapshot));
            var loadout = new SkillLoadout();
            loadout.RestoreBans(restored.bannedSkillIds, restored.bansLeft);

            Assert.AreEqual(0, loadout.BansLeft);
            Assert.IsFalse(loadout.TryBan(Card(CardType.NewSkill, Make<PassiveSkillData>("atk_up"))));
        }

        [Test]
        public void RiftCheckpointJsonRoundTripKeepsBannedIdsAndZeroAllowance()
        {
            var checkpoint = new RiftCheckpoint
            {
                valid = true, floor = 20, bannedSkillIds = new List<string> { "atk_up", "spin_blade" }, bansLeft = 0,
            };

            RiftCheckpoint restored = JsonUtility.FromJson<RiftCheckpoint>(JsonUtility.ToJson(checkpoint));

            CollectionAssert.AreEquivalent(checkpoint.bannedSkillIds, restored.bannedSkillIds);
            Assert.AreEqual(0, restored.bansLeft);
        }

        [Test]
        public void OldRiftCheckpointWithoutBanFieldsStartsWithTwoBans()
        {
            RiftCheckpoint old = JsonUtility.FromJson<RiftCheckpoint>("{\"valid\":true,\"floor\":20}");
            var loadout = new SkillLoadout();
            loadout.RestoreBans(old.bannedSkillIds, old.bansLeft);

            Assert.AreEqual(2, loadout.BansLeft);
            Assert.IsEmpty(loadout.BannedSkillIds);
        }

        [Test]
        public void OldSnapshotWithoutBanFieldsStartsWithTwoBans()
        {
            RunSnapshot old = JsonUtility.FromJson<RunSnapshot>("{\"valid\":true,\"room\":4}");
            var loadout = new SkillLoadout();
            loadout.RestoreBans(old.bannedSkillIds, old.bansLeft);

            Assert.AreEqual(2, loadout.BansLeft);
            Assert.IsEmpty(loadout.BannedSkillIds);
        }

        [Test]
        public void RestoreBansClampsRemainingCount()
        {
            var loadout = new SkillLoadout();

            loadout.RestoreBans(Array.Empty<string>(), 99);
            Assert.AreEqual(SkillLoadout.BansPerRun, loadout.BansLeft);
            loadout.RestoreBans(Array.Empty<string>(), -1);
            Assert.AreEqual(0, loadout.BansLeft);
        }

        [Test]
        public void SignedSchemaFiveSaveMigratesBanFields()
        {
            var store = new SaveStore(_folder);
            Directory.CreateDirectory(_folder);
            // 실제 구조 5 파일처럼 새 필드 자체가 없는 JSON을 서명한다.
            const string oldJson = "{\"schemaVersion\":5,\"integrity\":\"\",\"run\":{\"valid\":true,\"room\":4},\"rift\":{\"checkpoint\":{\"valid\":true,\"floor\":20}}}";
            File.WriteAllText(store.MainPath, SaveStore.Sign(oldJson));

            SaveData loaded = store.Load();

            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.schemaVersion);
            Assert.AreEqual(2, loaded.run.bansLeft);
            Assert.IsEmpty(loaded.run.bannedSkillIds);
            Assert.AreEqual(2, loaded.rift.checkpoint.bansLeft);
            Assert.IsEmpty(loaded.rift.checkpoint.bannedSkillIds);
            Assert.IsEmpty(loaded.targetRecipes);
        }
    }
}
