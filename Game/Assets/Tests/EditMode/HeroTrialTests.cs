using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Equipment;
using HistoryHeroes.Heroes;
using HistoryHeroes.Meta;
using HistoryHeroes.Modes;
using HistoryHeroes.Pets;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>체험 설정과 성장을 확인한다. 장면이나 기기의 저장 파일은 열지 않는다.</summary>
    public sealed class HeroTrialTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private MetaSnapshot _devMeta;

        private T Create<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _objects.Add(value);
            return value;
        }

        [SetUp]
        public void SetUp()
        {
            GameSession.PrepareDungeon(1);
            _devMeta = BattleBootstrap.DevMeta;
            BattleBootstrap.DevMeta = null;
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.PrepareDungeon(1);
            BattleBootstrap.DevMeta = _devMeta;
            foreach (Object value in _objects) Object.DestroyImmediate(value);
            _objects.Clear();
        }

        [TestCase(0)]
        [TestCase(5)]
        public void TrialUsesPracticeWithoutSpendingHeartOrChangingSave(int hearts)
        {
            var save = new SaveData { hearts = hearts, unlockedChapter = 3, selectedHero = "khan" };
            save.run = new RunSnapshot { valid = true, chapter = 2, room = 4, heroId = "khan" };
            string before = JsonUtility.ToJson(save);

            GameSession.PrepareHeroTrial(save, "guan");

            Assert.AreEqual("guan", GameSession.TrialHeroId);
            Assert.IsTrue(GameSession.IsHeroTrial);
            Assert.IsTrue(GameSession.Practice);
            Assert.IsFalse(GameSession.HeartSpent);
            Assert.IsFalse(GameSession.Tutorial);
            Assert.AreEqual(BattleMode.Dungeon, GameSession.Mode);
            Assert.AreEqual(3, GameSession.Chapter);
            Assert.IsNull(GameSession.BossRecordId);
            Assert.IsNull(GameSession.Resuming);
            Assert.AreEqual(before, JsonUtility.ToJson(save), "하트·영웅 선택·기존 이어하기까지 그대로 둔다");
        }

        [TestCase(-1, 1)]
        [TestCase(1, 1)]
        [TestCase(4, 4)]
        [TestCase(6, 5)]
        public void TrialClampsCurrentlyChallengingChapter(int unlocked, int expected)
        {
            GameSession.PrepareHeroTrial(new SaveData { unlockedChapter = unlocked }, "guan");
            Assert.AreEqual(expected, GameSession.Chapter);
        }

        [Test]
        public void NormalDungeonClearsTrialAndPractice()
        {
            GameSession.PrepareHeroTrial(new SaveData(), "guan");
            GameSession.PrepareDungeon(2);

            Assert.IsNull(GameSession.TrialHeroId);
            Assert.IsFalse(GameSession.IsHeroTrial);
            Assert.IsFalse(GameSession.Practice);
            Assert.AreEqual(BattleMode.Dungeon, GameSession.Mode);
            Assert.AreEqual(2, GameSession.Chapter);
        }

        [Test]
        public void ClearSpecialRunClearsTrialForOtherModesAndStaticReset()
        {
            GameSession.PrepareHeroTrial(new SaveData(), "guan");
            GameSession.ClearSpecialRun();
            Assert.IsNull(GameSession.TrialHeroId);
            Assert.IsFalse(GameSession.IsHeroTrial);
        }

        [Test]
        public void TrialOverridesLockedHeroWithoutChangingSelectedHero()
        {
            HeroRoster roster = Create<HeroRoster>();
            HeroData khan = Create<HeroData>();
            khan.id = "khan";
            HeroData guan = Create<HeroData>();
            guan.id = "guan";
            guan.unlockAfterBossChapter = 1;
            roster.heroes.AddRange(new[] { khan, guan });
            var save = new SaveData { selectedHero = "khan" };
            Assert.IsFalse(HeroUnlock.IsUnlocked(save, guan));

            GameSession.PrepareHeroTrial(save, guan.id);
            Assert.AreSame(guan, BattleBootstrap.HeroForThisBattle(roster, save));
            Assert.AreEqual("khan", save.selectedHero);

            GameSession.PrepareDungeon(1);
            Assert.AreSame(khan, BattleBootstrap.HeroForThisBattle(roster, save));
        }

        [Test]
        public void TrialUsesLevelOneStarOneAndKeepsAccountGrowth()
        {
            var save = new SaveData { unlockedChapter = 4 };
            save.Hero("guan").level = 30;
            save.Hero("guan").stars = 5;
            save.training.AddRange(new[] { 3, 2, 1 });
            save.relics.owned.Add(new RelicStar { id = "relic", stars = 2 });
            MetaTable table = Create<MetaTable>();
            table.equipment = Create<EquipmentTable>();
            EquipmentData weapon = Create<EquipmentData>();
            weapon.id = "weapon";
            weapon.slot = EquipSlot.Weapon;
            table.equipment.items.Add(weapon);
            save.equipment.items.Add(new EquipmentItem { uid = "equipped", dataId = weapon.id });
            EquipmentInventory.LoadoutOf(save, "guan").uids[0] = "equipped";
            table.pets = Create<PetTable>();
            PetData pet = Create<PetData>();
            pet.id = "falcon";
            pet.bondHeroId = "guan";
            table.pets.pets.Add(pet);
            save.pets.owned.Add(new PetLevel { id = pet.id, level = 7 });
            save.pets.loadouts.Add(new HeroPet { heroId = "guan", petId = pet.id });
            string before = JsonUtility.ToJson(save);

            GameSession.PrepareHeroTrial(save, "guan");
            MetaSnapshot snapshot = BattleBootstrap.MetaForThisBattle(table, "guan", save);

            Assert.AreEqual(1, snapshot.heroLevel);
            Assert.AreEqual(1, snapshot.heroStars);
            CollectionAssert.AreEqual(save.training, snapshot.training);
            Assert.AreEqual("weapon", snapshot.equipment[0].dataId);
            Assert.AreEqual("relic", snapshot.relics[0].id);
            Assert.AreEqual(2, snapshot.relics[0].stars);
            Assert.AreEqual("falcon", snapshot.petId);
            Assert.AreEqual(7, snapshot.petLevel);
            Assert.IsTrue(snapshot.petBond);
            Assert.AreEqual(before, JsonUtility.ToJson(save), "저장된 레벨과 별은 바꾸지 않는다");

            GameSession.PrepareDungeon(1);
            MetaSnapshot normal = BattleBootstrap.MetaForThisBattle(table, "guan", save);
            Assert.AreEqual(30, normal.heroLevel);
            Assert.AreEqual(5, normal.heroStars);
        }

        [Test]
        public void TrialLimitIsExactly180Seconds()
        {
            Assert.AreEqual(180f, GameSession.HeroTrialSeconds);
            Assert.IsFalse(GameSession.HeroTrialExpired(180f));
            GameSession.PrepareHeroTrial(new SaveData(), "guan");
            Assert.IsFalse(GameSession.HeroTrialExpired(179.99f));
            Assert.IsTrue(GameSession.HeroTrialExpired(180f));
            Assert.IsTrue(GameSession.HeroTrialExpired(181f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UnlockMethodTextCoversGemAndBossHeroes(bool gemsOnly)
        {
            HeroData hero = Create<HeroData>();
            hero.gemsOnly = gemsOnly;
            hero.unlockGems = 1500;
            hero.unlockAfterBossChapter = 2;

            string text = HeroUnlock.LockText(hero);
            Assert.IsNotEmpty(text);
            StringAssert.Contains("보석", text);
            if (!gemsOnly) StringAssert.Contains("2구간 보스", text);
        }

        [Test]
        public void TrialDoveTreasureKeepsFruitButNeverAwardsGold()
        {
            GameSession.PrepareHeroTrial(new SaveData(), "guan");
            Assert.AreEqual(0, DoveBehaviour.RollTreasure(5, true, true, 0.9f, 0f, GameSession.Practice).gold);
            Assert.IsTrue(DoveBehaviour.RollTreasure(5, true, true, 0f, 0f, GameSession.Practice).fruit);
            Assert.AreEqual(70, DoveBehaviour.RollTreasure(5, true, true, 0.9f, 0f).gold, "일반 판의 보물은 그대로");
        }
    }
}
