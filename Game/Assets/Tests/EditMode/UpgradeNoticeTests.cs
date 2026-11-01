using System;
using HistoryHeroes.Economy;
using HistoryHeroes.Equipment;
using HistoryHeroes.Heroes;
using HistoryHeroes.Meta;
using HistoryHeroes.Records;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class UpgradeNoticeTests
    {
        private HeroRoster _roster;
        private HeroData _hero;
        private MetaTable _table;
        private EquipmentTable _equipment;
        private SaveData _save;

        [SetUp]
        public void SetUp()
        {
            _roster = ScriptableObject.CreateInstance<HeroRoster>();
            _hero = ScriptableObject.CreateInstance<HeroData>();
            _hero.id = "khan";
            _hero.gemsOnly = false;
            _hero.unlockAfterBossChapter = 0;
            _roster.heroes.Add(_hero);
            _table = ScriptableObject.CreateInstance<MetaTable>();
            _equipment = ScriptableObject.CreateInstance<EquipmentTable>();
            _table.equipment = _equipment;
            _save = new SaveData();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_equipment);
            UnityEngine.Object.DestroyImmediate(_table);
            UnityEngine.Object.DestroyImmediate(_hero);
            UnityEngine.Object.DestroyImmediate(_roster);
        }

        [Test]
        public void HasHeroUpgrade_LevelAffordableWithoutChangingSave()
        {
            _save.gold = _table.HeroLevelCost(1);
            Assert.IsTrue(UpgradeNotice.HasHeroUpgrade(_save, _table, _roster));
            Assert.IsEmpty(_save.heroProgress, "알림 계산이 성장 저장 항목을 만들면 안 된다");
            _save.gold--;
            Assert.IsFalse(UpgradeNotice.HasHeroUpgrade(_save, _table, _roster));
        }

        [Test]
        public void HasHeroUpgrade_LockedHeroDoesNotShowBadge()
        {
            _hero.gemsOnly = true;
            _save.gold = _table.HeroLevelCost(1);
            Assert.IsFalse(UpgradeNotice.HasHeroUpgrade(_save, _table, _roster));
        }

        [Test]
        public void HasHeroUpgrade_AwakeningShardsEnough()
        {
            _table.maxHeroLevel = 1;
            int cost = _table.AwakenCost(1);
            _save.AddItem(Wallet.HeroShard("khan"), cost);
            Assert.IsTrue(UpgradeNotice.HasHeroUpgrade(_save, _table, _roster));
            _save.AddItem(Wallet.HeroShard("khan"), -1);
            Assert.IsFalse(UpgradeNotice.HasHeroUpgrade(_save, _table, _roster));
        }

        [Test]
        public void HasHeroUpgrade_EquippedGearCanBeEnhanced()
        {
            _table.maxHeroLevel = 1;
            _table.awakenShards = new[] { 999 };
            _save.unlockedChapter = 2;
            _save.gold = _equipment.EnhanceCost(1);
            var weapon = ScriptableObject.CreateInstance<EquipmentData>();
            try
            {
                weapon.id = "test_weapon";
                weapon.slot = EquipSlot.Weapon;
                weapon.rarity = EquipRarity.Common;
                _equipment.items.Add(weapon);
                _save.equipment.items.Add(new EquipmentItem { uid = "e1", dataId = weapon.id });
                _save.equipment.loadouts.Add(new HeroLoadout { heroId = "khan", uids = new System.Collections.Generic.List<string> { "e1", "", "", "", "", "" } });
                Assert.IsTrue(UpgradeNotice.HasHeroUpgrade(_save, _table, _roster));
                _save.gold--;
                Assert.IsFalse(UpgradeNotice.HasHeroUpgrade(_save, _table, _roster));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void HasNewcomerReward_BonusReadyAndClaimed()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.FromHours(9));
            _save.newcomer.startDay = "2026-10-31";
            _save.newcomer.activeDays = 7;
            for (int day = 1; day <= 7; day++) _save.newcomer.claimedDays.Add(day);
            Assert.IsTrue(UpgradeNotice.HasNewcomerReward(_save, now));
            _save.newcomer.bonusClaimed = true;
            Assert.IsFalse(UpgradeNotice.HasNewcomerReward(_save, now));
        }
    }
}
