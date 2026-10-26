using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Economy;
using HistoryHeroes.Equipment;
using HistoryHeroes.Heroes;
using HistoryHeroes.Meta;
using HistoryHeroes.Records;
using HistoryHeroes.Stats;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>v0.4 장비·판 밖 성장 규칙 (Equipment 3~10장, Stats 4~6장)</summary>
    public sealed class EquipmentTests
    {
        private EquipmentTable _table;
        private MetaTable _meta;

        private EquipmentData Make(string id, EquipSlot slot, EquipRarity rarity, StatType? secondary = null, float value = 0f,
            ModifierKind kind = ModifierKind.Flat)
        {
            var e = ScriptableObject.CreateInstance<EquipmentData>();
            e.id = id;
            e.displayName = id;
            e.slot = slot;
            e.rarity = rarity;
            e.hasSecondary = secondary.HasValue;
            e.secondaryStat = secondary ?? StatType.Attack;
            e.secondaryValue = value;
            e.secondaryKind = kind;
            _table.items.Add(e);
            return e;
        }

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<EquipmentTable>();
            // Equipment 7장 21종과 같은 부가 능력치
            Make("w_c", EquipSlot.Weapon, EquipRarity.Common);
            Make("w_u1", EquipSlot.Weapon, EquipRarity.Uncommon, StatType.CritDamage, 0.05f);
            Make("w_u2", EquipSlot.Weapon, EquipRarity.Uncommon, StatType.CooldownReduction, 0.01f);
            Make("w_r1", EquipSlot.Weapon, EquipRarity.Rare, StatType.CritDamage, 0.10f);
            Make("w_r2", EquipSlot.Weapon, EquipRarity.Rare, StatType.CooldownReduction, 0.02f);
            Make("w_l1", EquipSlot.Weapon, EquipRarity.Legendary, StatType.CritRate, 0.03f);
            Make("w_l2", EquipSlot.Weapon, EquipRarity.Legendary, StatType.CritDamage, 0.20f);
            Make("a_c", EquipSlot.Armor, EquipRarity.Common);
            Make("a_u1", EquipSlot.Armor, EquipRarity.Uncommon, StatType.DamageReduction, 0.01f);
            Make("a_u2", EquipSlot.Armor, EquipRarity.Uncommon, StatType.MoveSpeed, 0.01f, ModifierKind.Percent);
            Make("a_r1", EquipSlot.Armor, EquipRarity.Rare, StatType.DamageReduction, 0.03f);
            Make("a_r2", EquipSlot.Armor, EquipRarity.Rare, StatType.MoveSpeed, 0.015f, ModifierKind.Percent);
            Make("a_l1", EquipSlot.Armor, EquipRarity.Legendary, StatType.DamageReduction, 0.06f);
            Make("a_l2", EquipSlot.Armor, EquipRarity.Legendary, StatType.MoveSpeed, 0.02f, ModifierKind.Percent);
            Make("x_c", EquipSlot.Accessory, EquipRarity.Common);
            Make("x_u1", EquipSlot.Accessory, EquipRarity.Uncommon, StatType.CritRate, 0.01f);
            Make("x_u2", EquipSlot.Accessory, EquipRarity.Uncommon, StatType.MoveSpeed, 0.02f, ModifierKind.Percent);
            Make("x_r1", EquipSlot.Accessory, EquipRarity.Rare, StatType.CritRate, 0.015f);
            Make("x_r2", EquipSlot.Accessory, EquipRarity.Rare, StatType.CooldownReduction, 0.02f);
            Make("x_l1", EquipSlot.Accessory, EquipRarity.Legendary, StatType.CritRate, 0.02f);
            Make("x_l2", EquipSlot.Accessory, EquipRarity.Legendary, StatType.MoveSpeed, 0.03f, ModifierKind.Percent);

            _meta = ScriptableObject.CreateInstance<MetaTable>();
            _meta.equipment = _table;
        }

        private static SaveData Unlocked()
        {
            var save = new SaveData();
            save.defeatedBosses.Add("rift_warden");
            save.unlockedChapter = 2;
            return save;
        }

        // ---------------------------------------------------------------- 숫자 (3장)

        [Test]
        public void MainPercent_BaseAndEnhanceCappedByRarity()
        {
            Assert.AreEqual(0.055f, _table.MainPercent(_table.Find("w_c"), 10), 1e-5f, "일반 Lv10 = 3 + 2.5");
            Assert.AreEqual(0.11f, _table.MainPercent(_table.Find("w_u1"), 25), 1e-5f, "고급은 Lv20까지만: 6 + 5");
            Assert.AreEqual(0.25f, _table.MainPercent(_table.Find("w_l1"), 40), 1e-5f, "전설 Lv40 = 15 + 10");
            Assert.AreEqual(0.08f, _table.MainPercent(_table.Find("x_l2"), 40), 1e-5f, "장신구 전설 Lv40 = 6 + 2");
            Assert.AreEqual(100, _table.EnhanceCost(0));
            Assert.AreEqual(3900, _table.EnhanceCost(39));
        }

        /// <summary>힘 예산 (원칙 1장, 3-5장): 어떤 조합으로도 장비 몫을 넘지 않는다</summary>
        [Test]
        public void Budget_BestPossibleCombinationStaysInsideEquipmentShare()
        {
            var shares = new Dictionary<StatType, float>
            {
                [StatType.CritRate] = 0.05f, [StatType.CritDamage] = 0.20f, [StatType.CooldownReduction] = 0.04f,
                [StatType.MoveSpeed] = 0.05f, [StatType.DamageReduction] = 0.06f,
            };

            foreach (KeyValuePair<StatType, float> share in shares)
            {
                // 칸마다 그 능력치가 가장 큰 장비를 고른 합
                float best = 0f;
                for (int s = 0; s < 3; s++)
                {
                    float slotBest = 0f;
                    foreach (EquipmentData d in _table.items)
                    {
                        if ((int)d.slot == s && d.hasSecondary && d.secondaryStat == share.Key) slotBest = Mathf.Max(slotBest, d.secondaryValue);
                    }

                    best += slotBest;
                }

                Assert.LessOrEqual(best, share.Value + 1e-5f, $"{share.Key} 장비 최대 합 {best}");
            }

            float atk = 0f, hp = 0f, aspd = 0f;
            foreach (EquipmentData d in _table.items)
            {
                float main = _table.MainPercent(d, 40);
                if (d.slot == EquipSlot.Weapon) atk = Mathf.Max(atk, main);
                if (d.slot == EquipSlot.Armor) hp = Mathf.Max(hp, main);
                if (d.slot == EquipSlot.Accessory) aspd = Mathf.Max(aspd, main);
            }

            Assert.LessOrEqual(atk, 0.25f + 1e-5f);
            Assert.LessOrEqual(hp, 0.25f + 1e-5f);
            Assert.LessOrEqual(aspd, 0.08f + 1e-5f);
        }

        /// <summary>Stats 4장: 판 밖 전부(영웅 레벨 + 각성 + 수련 + 장비, 유물은 RelicTests) 최대에서 공격력 ≤ +85% (유물 10 + 펫 5 = 100%)</summary>
        [Test]
        public void Budget_AllMetaSourcesAtMax_AttackAndHpWithinStatsTable()
        {
            var snap = new MetaSnapshot
            {
                heroLevel = 30,
                heroStars = 5,
                equipment = new List<EquippedItem>
                {
                    new EquippedItem { slot = 0, dataId = "w_l2", level = 40 },
                    new EquippedItem { slot = 1, dataId = "a_l1", level = 40 },
                    new EquippedItem { slot = 2, dataId = "x_l1", level = 40 },
                },
                training = new List<int> { 20, 20, 20, 20, 20, 10 },
            };

            List<StatModifier> mods = MetaStats.Build(snap, _meta, this);
            float atk = MetaStats.Sum(mods, StatType.Attack, ModifierKind.Percent);
            float hp = MetaStats.Sum(mods, StatType.MaxHp, ModifierKind.Percent);
            Assert.LessOrEqual(atk, 0.85f + 1e-5f, $"공격력 {atk}");
            Assert.LessOrEqual(hp, 0.85f + 1e-5f, $"체력 {hp}");
            Assert.LessOrEqual(MetaStats.Sum(mods, StatType.AttackSpeed, ModifierKind.Percent), 0.12f + 1e-5f, "공속 전체 상한 12%");
            Assert.LessOrEqual(MetaStats.Sum(mods, StatType.CritRate, ModifierKind.Flat), 0.08f + 1e-5f, "치확 전체 상한 8%");
            Assert.LessOrEqual(MetaStats.Sum(mods, StatType.CooldownReduction, ModifierKind.Flat), 0.08f + 1e-5f, "쿨감 전체 상한 8%");
            Assert.AreEqual(0f, MetaStats.Sum(mods, StatType.ExpGain, ModifierKind.Percent) + MetaStats.Sum(mods, StatType.ExpGain, ModifierKind.Flat), "판 밖은 경험치 획득 없음");
        }

        // ---------------------------------------------------------------- 칸 강화 (4장)

        [Test]
        public void Enhance_NeedsEquippedItem_StopsAtRarityCap_SpendsGold()
        {
            SaveData save = Unlocked();
            save.gold = 1_000_000;
            Assert.IsFalse(EquipmentInventory.TryEnhance(save, _table, "khan", EquipSlot.Weapon), "끼운 장비가 없으면 못 올린다");

            EquipmentItem common = EquipmentInventory.Add(save, _table, _table.Find("w_c")).Item;
            EquipmentInventory.Equip(save, _table, "khan", common.uid);
            Assert.AreEqual(10, EquipmentInventory.EnhanceMax(save, _table, "khan", EquipSlot.Weapon), "일반은 Lv10까지");
            Assert.AreEqual(10, EquipmentInventory.SlotLevel(save, EquipSlot.Weapon));
            Assert.AreEqual(1_000_000 - (100 + 100 * (1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 + 9)), save.gold);

            // 더 좋은 장비로 바꿔도 칸 강화는 남는다 → 이어서 올릴 수 있다
            EquipmentItem rare = EquipmentInventory.Add(save, _table, _table.Find("w_r1")).Item;
            EquipmentInventory.Equip(save, _table, "khan", rare.uid);
            Assert.IsTrue(EquipmentInventory.TryEnhance(save, _table, "khan", EquipSlot.Weapon));
            Assert.AreEqual(11, EquipmentInventory.SlotLevel(save, EquipSlot.Weapon));

            // 다른 영웅도 같은 칸 강화를 쓴다 (계정 공용). 일반을 끼우면 Lv10까지만 먹는다
            EquipmentInventory.Equip(save, _table, "guan", common.uid);
            Assert.AreEqual(10, EquipmentInventory.Snapshot(save, _table, "guan")[0].level);
            Assert.AreEqual(11, EquipmentInventory.Snapshot(save, _table, "khan")[0].level);
        }

        // ---------------------------------------------------------------- 합성·분해·교환 (5·9장)

        [Test]
        public void Synthesis_ThreeSameSlotSameRarity_ToChosenNextRarity()
        {
            SaveData save = Unlocked();
            save.gold = 600;
            var uids = new List<string>();
            for (int i = 0; i < 3; i++) uids.Add(EquipmentInventory.Add(save, _table, _table.Find("w_c")).Item.uid);
            string armor = EquipmentInventory.Add(save, _table, _table.Find("a_c")).Item.uid;

            Assert.IsFalse(EquipmentInventory.CanSynthesize(save, _table, new List<string> { uids[0], uids[1], armor }, out _, out _), "칸이 다르면 안 됨");
            Assert.IsFalse(EquipmentInventory.CanSynthesize(save, _table, new List<string> { uids[0], uids[0], uids[1] }, out _, out _), "같은 장비 두 번 안 됨");

            EquipmentInventory.SetLocked(save, uids[2], true);
            Assert.IsFalse(EquipmentInventory.CanSynthesize(save, _table, uids, out _, out _), "잠근 장비는 재료가 아님");
            EquipmentInventory.SetLocked(save, uids[2], false);
            EquipmentInventory.Equip(save, _table, "khan", uids[2]);
            Assert.IsFalse(EquipmentInventory.CanSynthesize(save, _table, uids, out _, out _), "낀 장비는 재료가 아님");
            EquipmentInventory.Unequip(save, "khan", EquipSlot.Weapon);

            Assert.IsNull(EquipmentInventory.TrySynthesize(save, _table, uids, "w_r1"), "두 등급 위는 고를 수 없다");
            EquipmentItem result = EquipmentInventory.TrySynthesize(save, _table, uids, "w_u2");
            Assert.IsNotNull(result);
            Assert.AreEqual("w_u2", result.dataId, "결과는 플레이어가 고른 것");
            Assert.AreEqual(100, save.gold, "합성 비용 500");
            Assert.AreEqual(2, save.equipment.items.Count, "재료 3개가 빠지고 결과 1개 + 갑옷");
        }

        [Test]
        public void Synthesis_RareAndLegendaryCannotBeMaterials()
        {
            SaveData save = Unlocked();
            save.gold = 100_000;
            save.equipment.autoDismantle = false;
            var uids = new List<string>();
            for (int i = 0; i < 3; i++) uids.Add(EquipmentInventory.Add(save, _table, _table.Find("w_r1")).Item.uid);
            Assert.IsFalse(EquipmentInventory.CanSynthesize(save, _table, uids, out _, out _), "희귀 → 전설 합성 금지");
        }

        [Test]
        public void Add_RareDuplicatesAutoDismantle_CommonDuplicatesKept_FullBagBecomesShards()
        {
            SaveData save = Unlocked();
            Assert.IsNotNull(EquipmentInventory.Add(save, _table, _table.Find("w_r1")).Item);
            EquipmentInventory.AddResult dup = EquipmentInventory.Add(save, _table, _table.Find("w_r1"));
            Assert.IsNull(dup.Item);
            Assert.AreEqual(10, dup.Shards, "희귀 중복 = 🔩 10");
            Assert.IsNotNull(EquipmentInventory.Add(save, _table, _table.Find("w_c")).Item);
            Assert.IsNotNull(EquipmentInventory.Add(save, _table, _table.Find("w_c")).Item, "일반 중복은 합성 재료로 보관");

            _table.inventoryCap = save.equipment.items.Count;
            EquipmentInventory.AddResult full = EquipmentInventory.Add(save, _table, _table.Find("a_c"));
            Assert.IsTrue(full.InventoryFull);
            Assert.AreEqual(1, full.Shards);
            Assert.AreEqual(11, save.ItemCount(Wallet.GearShard));
        }

        [Test]
        public void Dismantle_And_Exchange()
        {
            SaveData save = Unlocked();
            string uid = EquipmentInventory.Add(save, _table, _table.Find("w_u1")).Item.uid;
            EquipmentInventory.Equip(save, _table, "khan", uid);
            Assert.AreEqual(0, EquipmentInventory.TryDismantle(save, _table, uid), "낀 장비는 분해 안 됨");
            EquipmentInventory.Unequip(save, "khan", EquipSlot.Weapon);
            Assert.AreEqual(3, EquipmentInventory.TryDismantle(save, _table, uid));

            save.AddItem(Wallet.GearShard, 57);
            Assert.IsNull(EquipmentInventory.TryExchange(save, _table, "x_l1"), "전설은 🔩 300");
            Assert.IsNull(EquipmentInventory.TryExchange(save, _table, "x_c"), "일반은 교환소에 없음");
            EquipmentInventory.AddResult? got = EquipmentInventory.TryExchange(save, _table, "x_r2");
            Assert.IsTrue(got.HasValue && got.Value.Item != null);
            Assert.AreEqual(0, save.ItemCount(Wallet.GearShard));

            save.AddItem(Wallet.GearShard, 60);
            Assert.IsNotNull(EquipmentInventory.TryExchange(save, _table, "x_r2")?.Item, "교환은 고른 거라 중복이어도 분해하지 않는다");
        }

        // ---------------------------------------------------------------- 스냅샷·드랍·저장

        [Test]
        public void Snapshot_BuildsModifiersFromLatestData()
        {
            SaveData save = Unlocked();
            save.Hero("khan").level = 11;
            save.Hero("khan").stars = 3;
            string uid = EquipmentInventory.Add(save, _table, _table.Find("w_l2")).Item.uid;
            EquipmentInventory.Equip(save, _table, "khan", uid);
            save.equipment.slotLevels[0] = 40;

            MetaSnapshot snap = MetaStats.FromAccount(save, _meta, "khan");
            List<StatModifier> mods = MetaStats.Build(snap, _meta, this);
            Assert.AreEqual(0.10f + 0.06f + 0.25f, MetaStats.Sum(mods, StatType.Attack, ModifierKind.Percent), 1e-5f, "레벨 10% + ★3 6% + 전설 무기 25%");
            Assert.AreEqual(0.20f, MetaStats.Sum(mods, StatType.CritDamage, ModifierKind.Flat), 1e-5f);

            // 기록에 고정된 뒤 장비를 바꿔도 스냅샷은 그대로 (7-5)
            EquipmentInventory.Unequip(save, "khan", EquipSlot.Weapon);
            Assert.AreEqual(1, snap.equipment.Count);

            // 패치로 장비 수치가 바뀌면 옛 기록에도 적용된다 (수치는 최신 데이터)
            _table.weaponBase[3] = 0.20f;
            Assert.AreEqual(0.10f + 0.06f + 0.30f, MetaStats.Sum(MetaStats.Build(snap, _meta, this), StatType.Attack, ModifierKind.Percent), 1e-5f);

            // 없어진 장비는 빼고 싸운다
            snap.equipment[0].dataId = "deleted";
            Assert.AreEqual(0.16f, MetaStats.Sum(MetaStats.Build(snap, _meta, this), StatType.Attack, ModifierKind.Percent), 1e-5f);
        }

        [Test]
        public void DungeonDrop_ThirtyPercent_RarityTable_LockedBeforeFirstBoss()
        {
            var locked = new SaveData();
            Assert.IsNull(EquipmentInventory.RollDungeonDrop(locked, _table, 0f, 0f, 0), "1구간 보스 전에는 장비가 없다");

            SaveData save = Unlocked();
            Assert.IsNull(EquipmentInventory.RollDungeonDrop(save, _table, 0.30f, 0f, 0), "30% 밖");
            Assert.AreEqual(EquipRarity.Common, EquipmentInventory.RollDungeonDrop(save, _table, 0.29f, 0.69f, 0).rarity);
            Assert.AreEqual(EquipRarity.Uncommon, EquipmentInventory.RollDungeonDrop(save, _table, 0.29f, 0.94f, 0).rarity);
            Assert.AreEqual(EquipRarity.Rare, EquipmentInventory.RollDungeonDrop(save, _table, 0.29f, 0.96f, 0).rarity);
        }

        [Test]
        public void Load_SchemaV2Save_MigratesToV3WithEmptyMeta()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh_v3_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                var old = new SaveData { schemaVersion = 2, gold = 777 };
                old.records.Add(new RunRecord { id = "r1", heroId = "khan", skills = new List<RecordSkill> { new RecordSkill("atk_up", 2, false) } });
                string json = JsonUtility.ToJson(old, true);
                // v2 파일에는 v0.4 칸이 없다
                foreach (string key in new[] { "equipment", "heroProgress", "training", "gacha", "shop", "meta" }) json = json.Replace($"\"{key}\"", $"\"{key}_old\"");
                File.WriteAllText(store.MainPath, json);

                SaveData loaded = store.Load();
                Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.schemaVersion);
                Assert.AreEqual(777, loaded.gold);
                Assert.IsNotNull(loaded.equipment);
                Assert.AreEqual(3, loaded.equipment.slotLevels.Count);
                Assert.IsNotNull(loaded.records[0].meta);
                Assert.AreEqual(1, loaded.records[0].meta.heroLevel, "옛 기록 = 판 밖 성장 없음");
                Assert.AreEqual(0, loaded.records[0].meta.equipment.Count);
                Assert.AreEqual(1, loaded.Hero("khan").level);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void BossRewards_GivenOnceEvenForOldSaves()
        {
            Make("weapon_general_r", EquipSlot.Weapon, EquipRarity.Rare, StatType.CritDamage, 0.10f);
            var save = new SaveData { unlockedChapter = 2 }; // 장비가 생기기 전에 1구간 보스를 깬 저장
            save.defeatedBosses.Add("rift_warden");
            List<string> lines = EquipmentInventory.GrantBossRewards(save, _table);
            Assert.AreEqual(1, lines.Count);
            Assert.IsTrue(EquipmentInventory.Owns(save, "weapon_general_r"));
            Assert.AreEqual(0, EquipmentInventory.GrantBossRewards(save, _table).Count, "한 번만");
            Assert.AreEqual(0, EquipmentInventory.GrantBossRewards(new SaveData(), _table).Count, "보스를 안 깼으면 없음");
        }

        // ---------------------------------------------------------------- 상자 (Gacha 2장)

        private GachaTable Gacha()
        {
            var g = ScriptableObject.CreateInstance<GachaTable>();
            g.equipment = _table;
            return g;
        }

        private static System.Func<float> Always(float v) => () => v;

        [Test]
        public void Premium_FiftiethPullIsLegendary_PityResets()
        {
            SaveData save = Unlocked();
            GachaTable g = Gacha();
            List<GachaService.Pull> pulls = GachaService.Open(save, g, g.premium, 49, 0, Always(0.1f)); // 0.1 = 일반
            Assert.IsFalse(pulls.Exists(p => p.Data.rarity == EquipRarity.Legendary));
            Assert.AreEqual(49, save.gacha.premiumPity);
            Assert.AreEqual(1, GachaService.PullsUntilPity(save, g.premium), "전설 확정까지 1회");

            GachaService.Pull fiftieth = GachaService.Open(save, g, g.premium, 1, 0, Always(0.1f))[0];
            Assert.AreEqual(EquipRarity.Legendary, fiftieth.Data.rarity);
            Assert.IsTrue(fiftieth.FromPity);
            Assert.AreEqual(0, save.gacha.premiumPity, "전설이 나오면 0으로");

            // 운 좋게 나온 전설도 카운터를 0으로
            save.gacha.premiumPity = 20;
            Assert.AreEqual(EquipRarity.Legendary, GachaService.Open(save, g, g.premium, 1, 0, Always(0.99f))[0].Data.rarity);
            Assert.AreEqual(0, save.gacha.premiumPity);
        }

        [Test]
        public void Premium_TenPull_GuaranteesRareOrBetter_PityComesFirst()
        {
            SaveData save = Unlocked();
            GachaTable g = Gacha();
            List<GachaService.Pull> ten = GachaService.Open(save, g, g.premium, 10, 0, Always(0.1f));
            Assert.AreEqual(10, ten.Count);
            Assert.IsTrue(ten[9].FromTenGuarantee);
            Assert.AreEqual(EquipRarity.Rare, ten[9].Data.rarity, "보장 표에서 0.1 = 희귀");

            // 도중에 천장에 닿으면 그 장이 전설 → 10번째 보장은 필요 없다
            save.gacha.premiumPity = 45;
            ten = GachaService.Open(save, g, g.premium, 10, 0, Always(0.1f));
            Assert.IsTrue(ten[4].FromPity);
            Assert.AreEqual(EquipRarity.Legendary, ten[4].Data.rarity);
            Assert.IsFalse(ten[9].FromTenGuarantee);
            Assert.AreEqual(5, save.gacha.premiumPity, "전설 뒤 5장");
        }

        [Test]
        public void Normal_NoPity_NoLegendary_FreeOncePerDay()
        {
            SaveData save = Unlocked();
            GachaTable g = Gacha();
            Assert.AreEqual(EquipRarity.Rare, GachaService.Open(save, g, g.normal, 1, 0, Always(0.995f))[0].Data.rarity, "일반 상자엔 전설이 없다");
            Assert.AreEqual(0, save.gacha.premiumPity, "일반 상자는 천장 카운터를 안 쓴다");
            Assert.AreEqual(-1, GachaService.PullsUntilPity(save, g.normal));

            Assert.IsTrue(GachaService.TryPay(save, g, g.normal, 1, GachaService.Pay.FreeAd, "2026-10-26"));
            Assert.IsFalse(GachaService.TryPay(save, g, g.normal, 1, GachaService.Pay.FreeAd, "2026-10-26"), "하루 1번");
            Assert.IsTrue(GachaService.TryPay(save, g, g.normal, 1, GachaService.Pay.FreeAd, "2026-10-27"));
            Assert.IsFalse(GachaService.TryPay(save, g, g.premium, 1, GachaService.Pay.FreeAd, "2026-10-28"), "고급 상자는 무료로 주지 않는다");
        }

        [Test]
        public void Pay_GemsTicketsGold()
        {
            SaveData save = Unlocked();
            GachaTable g = Gacha();
            save.gemsFree = 2500;
            Assert.IsTrue(GachaService.TryPay(save, g, g.premium, 10, GachaService.Pay.Gems, "d"));
            Assert.AreEqual(250, save.gemsFree, "10연 2,250");
            Assert.IsTrue(GachaService.TryPay(save, g, g.premium, 1, GachaService.Pay.Gems, "d"));
            Assert.IsFalse(GachaService.TryPay(save, g, g.premium, 1, GachaService.Pay.Gems, "d"), "보석 부족");
            Assert.IsFalse(GachaService.TryPay(save, g, g.premium, 1, GachaService.Pay.Ticket, "d"), "소환권 없음");
            save.AddItem(Wallet.Ticket, 1);
            Assert.IsTrue(GachaService.TryPay(save, g, g.premium, 1, GachaService.Pay.Ticket, "d"));
            Assert.IsFalse(GachaService.TryPay(save, g, g.premium, 10, GachaService.Pay.Ticket, "d"), "소환권은 1회씩");
            save.gold = 5000;
            Assert.IsTrue(GachaService.TryPay(save, g, g.normal, 1, GachaService.Pay.Gold, "d"));
            Assert.AreEqual(0, save.gold);
        }

        [Test]
        public void RatesText_ComesFromTheSameTable()
        {
            GachaTable g = Gacha();
            string text = GachaService.RatesText(g, g.premium);
            StringAssert.Contains("전설 2%", text);
            StringAssert.Contains("장비마다 0.333%", text, "전설 6종: 2% ÷ 6");
            StringAssert.Contains("50번째", text);
            g.premium.rarity = new[] { 0.48f, 0.35f, 0.13f, 0.04f };
            StringAssert.Contains("전설 4%", GachaService.RatesText(g, g.premium), "표를 바꾸면 보이는 확률도 같이 바뀐다");
        }

        // ---------------------------------------------------------------- 영웅 성장 (Stats 4~7)

        [Test]
        public void HeroLevel_Costs200xN_MaxThirty()
        {
            var save = new SaveData { gold = 1_000_000 };
            int spent = 0;
            while (HeroGrowth.TryLevelUp(save, _meta, "khan")) { }
            for (int n = 1; n < 30; n++) spent += 200 * n;
            Assert.AreEqual(30, save.Hero("khan").level);
            Assert.AreEqual(87_000, spent, "영웅 하나 약 87,000");
            Assert.AreEqual(1_000_000 - spent, save.gold);
            Assert.AreEqual(0.29f, _meta.HeroLevelBonus(30), 1e-5f, "Lv1 = 0%, 레벨당 +1%");
            Assert.AreEqual(1, save.Hero("guan").level, "영웅마다 따로");
        }

        [Test]
        public void Awaken_SpendsHeroShards_10_20_40_80()
        {
            var save = new SaveData();
            Assert.IsFalse(HeroGrowth.TryAwaken(save, _meta, "khan"), "조각 없음");
            save.AddItem(Wallet.HeroShard("khan"), 150);
            for (int i = 0; i < 4; i++) Assert.IsTrue(HeroGrowth.TryAwaken(save, _meta, "khan"));
            Assert.AreEqual(5, save.Hero("khan").stars);
            Assert.AreEqual(0, HeroGrowth.Shards(save, "khan"), "10+20+40+80 = 150");
            Assert.IsFalse(HeroGrowth.TryAwaken(save, _meta, "khan"), "★5가 끝");
            Assert.AreEqual(0.15f, _meta.AwakenBonus(5), 1e-5f);
        }

        [Test]
        public void Training_LockedUntilSecondBoss_CappedByAccountLevel()
        {
            var save = new SaveData { gold = 1_000_000, accountLevel = 2, unlockedChapter = 2 };
            Assert.IsFalse(HeroGrowth.TryTrain(save, _meta, TrainingType.Weapon), "2구간 보스 전엔 잠김");
            save.unlockedChapter = 3;
            Assert.IsTrue(HeroGrowth.TryTrain(save, _meta, TrainingType.Weapon));
            Assert.IsTrue(HeroGrowth.TryTrain(save, _meta, TrainingType.Weapon));
            Assert.IsFalse(HeroGrowth.CanTrain(save, _meta, TrainingType.Weapon, out _, out string reason), "수련 레벨 ≤ 계정 레벨");
            StringAssert.Contains("계정 Lv 3", reason);
            Assert.AreEqual(1_000_000 - 500 - 500, save.gold, "Lv n → n+1 = 500 × n (Lv0→1은 n=1로 쳐서 500)");

            var snap = new MetaSnapshot { training = new List<int>(save.training) };
            Assert.AreEqual(0.015f, MetaStats.Sum(MetaStats.Build(snap, _meta, this), StatType.Attack, ModifierKind.Percent), 1e-5f, "무기 단련 Lv2 = +1.5%");
        }

        [Test]
        public void GoldShop_HeroShards_ThreePerDay()
        {
            var save = new SaveData { gold = 10_000 };
            for (int i = 0; i < 3; i++) Assert.IsTrue(HeroGrowth.TryBuyHeroShard(save, "guan", "2026-10-26"));
            Assert.IsFalse(HeroGrowth.TryBuyHeroShard(save, "khan", "2026-10-26"), "하루 3개 (영웅 합쳐서)");
            Assert.AreEqual(0, HeroGrowth.ShardsLeftToday(save, "2026-10-26"));
            Assert.AreEqual(3, HeroGrowth.ShardsLeftToday(save, "2026-10-27"));
            Assert.IsTrue(HeroGrowth.TryBuyHeroShard(save, "khan", "2026-10-27"));
            Assert.AreEqual(3, HeroGrowth.Shards(save, "guan"));
            Assert.AreEqual(10_000 - 4 * 500, save.gold);
        }

        [Test]
        public void Signature_StarUpgradesApplyAtThreeAndFive()
        {
            var khan = ScriptableObject.CreateInstance<HeroData>();
            khan.firstShotBonus = 0.5f;
            khan.star3FirstShotBonus = 0.75f;
            khan.star5FirstShotBonus = 1.0f;
            khan.star5FirstShotPierce = true;
            Assert.AreEqual(0.5f, HeroSignature.For(khan, 2).FirstShotBonus);
            Assert.AreEqual(0.75f, HeroSignature.For(khan, 3).FirstShotBonus);
            Assert.IsFalse(HeroSignature.For(khan, 4).FirstShotPierce);
            Assert.AreEqual(1.0f, HeroSignature.For(khan, 5).FirstShotBonus);
            Assert.IsTrue(HeroSignature.For(khan, 5).FirstShotPierce);

            var tesla = ScriptableObject.CreateInstance<HeroData>();
            tesla.chainBounces = 2;
            tesla.chainFalloff = 0.3f;
            tesla.star3ExtraBounces = 1;
            tesla.star5NoFalloff = true;
            Assert.AreEqual(3, HeroSignature.For(tesla, 3).ChainBounces);
            Assert.AreEqual(0.3f, HeroSignature.For(tesla, 4).ChainFalloff);
            Assert.AreEqual(0f, HeroSignature.For(tesla, 5).ChainFalloff);

            var napoleon = ScriptableObject.CreateInstance<HeroData>();
            napoleon.cannonEvery = 3;
            napoleon.cannonRadius = 1.5f;
            napoleon.star3CannonRadiusScale = 1.3f;
            napoleon.star5CannonEvery = 2;
            Assert.AreEqual(1.95f, HeroSignature.For(napoleon, 3).CannonRadius, 1e-5f);
            Assert.AreEqual(3, HeroSignature.For(napoleon, 4).CannonEvery);
            Assert.AreEqual(2, HeroSignature.For(napoleon, 5).CannonEvery);
            Assert.AreEqual(0, HeroSignature.For(khan, 5).CannonEvery, "포탄이 없는 영웅은 ★5여도 없음");
        }
    }
}
