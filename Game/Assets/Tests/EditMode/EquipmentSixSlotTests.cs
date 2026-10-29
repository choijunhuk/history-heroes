using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Equipment;
using HistoryHeroes.Records;
using HistoryHeroes.Stats;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>실제 장비 42종 정의로 6칸의 예산·저장 이전·상자 노출을 확인한다.</summary>
    public sealed class EquipmentSixSlotTests
    {
        private EquipmentTable _table;
        private GachaTable _gacha;

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<EquipmentTable>();
            // 기존 에셋에 남은 3칸 값도 빌더가 새 표로 덮어써야 한다.
            _table.enhanceCostPerLevel = 100;
            _table.mainShares = new[] { 1f, 1f, 1f };
            SceneBuilder.ConfigureEquipment(_table, id => ScriptableObject.CreateInstance<EquipmentData>());
            _gacha = ScriptableObject.CreateInstance<GachaTable>();
            _gacha.equipment = _table;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (EquipmentData data in _table.items) UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(_gacha);
            UnityEngine.Object.DestroyImmediate(_table);
        }

        private static SaveData Unlocked()
        {
            var save = new SaveData { unlockedChapter = 2 };
            save.defeatedBosses.Add("rift_warden");
            return save;
        }

        private static SaveData LoadWritten(SaveData data)
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh_gear6_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                data.integrity = string.Empty;
                File.WriteAllText(store.MainPath, SaveStore.Sign(JsonUtility.ToJson(data, true)));
                return store.Load();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestCase(0)]
        [TestCase(12)]
        [TestCase(40)]
        public void OldThreeSlotSave_KeepsEveryItemAndEquippedUidAndLevel(int accessoryLevel)
        {
            var old = Unlocked();
            old.schemaVersion = 7;
            old.equipment.items.Add(new EquipmentItem { uid = "e_weapon", dataId = "weapon_general_r", locked = true });
            old.equipment.items.Add(new EquipmentItem { uid = "e_armor", dataId = "armor_general_r" });
            old.equipment.items.Add(new EquipmentItem { uid = "e_accessory", dataId = "acc_signet_r" });
            old.equipment.items.Add(new EquipmentItem { uid = "e_spare", dataId = "weapon_recruit_c" });
            old.equipment.slotLevels = new List<int> { 25, 18, accessoryLevel };
            old.equipment.loadouts.Add(new HeroLoadout
            {
                heroId = "khan", uids = new List<string> { "e_weapon", "e_armor", "e_accessory" },
            });
            old.equipment.loadouts.Add(new HeroLoadout
            {
                heroId = "guan", uids = new List<string> { "e_spare", "", "e_accessory" },
            });
            old.records.Add(new RunRecord
            {
                id = "r_gear", heroId = "khan", meta = new MetaSnapshot
                {
                    equipment = new List<EquippedItem> { new EquippedItem { slot = 2, dataId = "acc_signet_r", level = accessoryLevel } },
                },
            });

            SaveData loaded = LoadWritten(old);

            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.schemaVersion);
            Assert.AreEqual(4, loaded.equipment.items.Count, "가방의 남는 장비도 보존");
            Assert.IsTrue(EquipmentInventory.Find(loaded, "e_weapon").locked);
            CollectionAssert.AreEqual(new[] { 25, 18, accessoryLevel, 1, 1, 1 }, loaded.equipment.slotLevels);
            CollectionAssert.AreEqual(new[] { "e_weapon", "e_armor", "e_accessory", "", "", "" }, loaded.equipment.loadouts[0].uids);
            CollectionAssert.AreEqual(new[] { "e_spare", "", "e_accessory", "", "", "" }, loaded.equipment.loadouts[1].uids);
            Assert.AreEqual("acc_signet_r", EquipmentInventory.Equipped(loaded, "khan", EquipSlot.Necklace).dataId);
            Assert.AreEqual("acc_signet_r", loaded.records[0].meta.equipment[0].dataId, "옛 기록 장비 ID도 보존");
            Assert.AreEqual(2, loaded.records[0].meta.equipment[0].slot);
            Assert.AreEqual(accessoryLevel, loaded.records[0].meta.equipment[0].level);
        }

        [Test]
        public void CurrentSave_RepairsShortNullAndLongArraysToSixWithoutLosingBagItems()
        {
            var broken = Unlocked();
            broken.equipment.items.Add(new EquipmentItem { uid = "e_extra", dataId = "boots_recruit_c" });
            broken.equipment.slotLevels = new List<int> { 9 };
            broken.equipment.loadouts.Add(new HeroLoadout { heroId = "khan", uids = new List<string> { "e_extra" } });
            broken.equipment.loadouts.Add(new HeroLoadout
            {
                heroId = "guan", uids = new List<string> { "", null, "", "", "", "", "e_extra" },
            });
            SaveData loaded = LoadWritten(broken);
            CollectionAssert.AreEqual(new[] { 9, 1, 1, 1, 1, 1 }, loaded.equipment.slotLevels);
            CollectionAssert.AreEqual(new[] { "e_extra", "", "", "", "", "" }, loaded.equipment.loadouts[0].uids);
            CollectionAssert.AreEqual(new[] { "", "", "", "", "", "" }, loaded.equipment.loadouts[1].uids);
            Assert.AreEqual(1, loaded.equipment.items.Count, "잘린 장착 참조의 물건도 가방에 남는다");

            broken.equipment.slotLevels = new List<int> { -9, 88, 12, 1, 2, 3, 999 };
            loaded = LoadWritten(broken);
            CollectionAssert.AreEqual(new[] { 0, 40, 12, 1, 2, 3 }, loaded.equipment.slotLevels);
            Assert.AreEqual(1, loaded.equipment.items.Count);

            broken.equipment.slotLevels = null;
            broken.equipment.loadouts[0].uids = null;
            loaded = LoadWritten(broken);
            Assert.AreEqual(6, loaded.equipment.slotLevels.Count);
            Assert.AreEqual(6, loaded.equipment.loadouts[0].uids.Count);
            Assert.AreEqual(1, loaded.equipment.items.Count);
        }

        [Test]
        public void RealDefinitions_HaveSevenPerSlotAndEveryRarity()
        {
            Assert.AreEqual(6, EquipmentInventory.SlotCount);
            Assert.AreEqual(42, _table.items.Count);
            Assert.AreEqual(50, _table.enhanceCostPerLevel, "기존 에셋 비용도 갱신");
            CollectionAssert.AreEqual(new[] { 0.6f, 0.5f, 1f, 0.4f, 0.3f, 0.2f }, _table.mainShares);
            for (int s = 0; s < EquipmentInventory.SlotCount; s++)
            {
                Assert.AreEqual(1, _table.Of((EquipSlot)s, EquipRarity.Common).Count);
                foreach (EquipRarity rarity in new[] { EquipRarity.Uncommon, EquipRarity.Rare, EquipRarity.Legendary })
                    Assert.AreEqual(2, _table.Of((EquipSlot)s, rarity).Count, $"{(EquipSlot)s} {rarity}");
            }
        }

        [Test]
        public void Equip_RejectsAllOtherSlotTypesAndKeepsExistingEquipment()
        {
            SaveData save = Unlocked();
            var uids = new string[EquipmentInventory.SlotCount];
            for (int s = 0; s < uids.Length; s++)
            {
                EquipmentData data = _table.Of((EquipSlot)s, EquipRarity.Common)[0];
                uids[s] = EquipmentInventory.Add(save, _table, data).Item.uid;
                Assert.IsTrue(EquipmentInventory.Equip(save, _table, "khan", (EquipSlot)s, uids[s]));
            }

            for (int s = 0; s < uids.Length; s++)
            {
                for (int other = 0; other < uids.Length; other++)
                {
                    if (s == other) continue;
                    Assert.IsFalse(EquipmentInventory.Equip(save, _table, "khan", (EquipSlot)s, uids[other]));
                    Assert.AreEqual(uids[s], EquipmentInventory.Equipped(save, "khan", (EquipSlot)s).uid);
                }
            }
        }

        [Test]
        public void AllLegendaryAtForty_MatchesOldPrimaryAndSecondaryBudgets()
        {
            var expectedMain = new[] { 0.15f, 0.125f, 0.08f, 0.10f, 0.075f, 0.05f };
            float attack = 0f, hp = 0f, speed = 0f;
            for (int s = 0; s < EquipmentInventory.SlotCount; s++)
            {
                EquipmentData data = _table.Of((EquipSlot)s, EquipRarity.Legendary)[0];
                float main = _table.MainPercent(data, 40);
                Assert.AreEqual(expectedMain[s], main, 1e-6f, (EquipSlot)s + " 최대 주 능력치");
                switch (EquipmentTable.MainStat(data.slot))
                {
                    case StatType.Attack: attack += main; break;
                    case StatType.MaxHp: hp += main; break;
                    case StatType.AttackSpeed: speed += main; break;
                }
            }
            Assert.AreEqual(0.25f, attack, 1e-6f);
            Assert.AreEqual(0.25f, hp, 1e-6f);
            Assert.AreEqual(0.08f, speed, 1e-6f);

            var expectedSecondary = new Dictionary<StatType, float>
            {
                [StatType.CritRate] = 0.05f, [StatType.CritDamage] = 0.20f,
                [StatType.CooldownReduction] = 0.04f, [StatType.MoveSpeed] = 0.05f,
                [StatType.DamageReduction] = 0.06f,
            };
            foreach (KeyValuePair<StatType, float> pair in expectedSecondary)
            {
                float max = 0f;
                for (int s = 0; s < EquipmentInventory.SlotCount; s++)
                {
                    float slotMax = 0f;
                    foreach (EquipmentData data in _table.items)
                        if ((int)data.slot == s && data.hasSecondary && data.secondaryStat == pair.Key)
                            slotMax = Mathf.Max(slotMax, data.secondaryValue);
                    max += slotMax;
                }
                Assert.AreEqual(pair.Value, max, 1e-6f, pair.Key + " 장비 몫");
            }
        }

        [Test]
        public void SixSlotEnhancement_UsesExactly234000Gold()
        {
            SaveData save = Unlocked();
            save.gold = 300_000;
            for (int s = 0; s < EquipmentInventory.SlotCount; s++)
            {
                EquipSlot slot = (EquipSlot)s;
                EquipmentData data = _table.Of(slot, EquipRarity.Legendary)[0];
                string uid = EquipmentInventory.Add(save, _table, data).Item.uid;
                Assert.IsTrue(EquipmentInventory.Equip(save, _table, "khan", slot, uid));
                Assert.AreEqual(1, EquipmentInventory.SlotLevel(save, slot));
                Assert.AreEqual(39, EquipmentInventory.EnhanceMax(save, _table, "khan", slot));
                Assert.AreEqual(40, EquipmentInventory.SlotLevel(save, slot));
            }
            Assert.AreEqual(66_000, save.gold);
        }

        [Test]
        public void BothBoxes_ReachEverySlotAndShowItsIndividualRate()
        {
            CollectionAssert.AreEqual(new[] { 0.70f, 0.27f, 0.03f, 0f }, _gacha.normal.rarity);
            CollectionAssert.AreEqual(new[] { 0.50f, 0.35f, 0.13f, 0.02f }, _gacha.premium.rarity);
            Assert.AreEqual(0, _gacha.normal.pity);
            Assert.AreEqual(50, _gacha.premium.pity);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0.867f, 0.133f }, _gacha.tenPullGuarantee);
            foreach (BoxRates box in new[] { _gacha.normal, _gacha.premium })
            {
                Assert.IsTrue(GachaService.CanOpen(_gacha, box));
                string rates = GachaService.RatesText(_gacha, box);
                float previous = 0f;
                for (int rarityIndex = 0; rarityIndex < box.rarity.Length; rarityIndex++)
                {
                    float chance = box.rarity[rarityIndex];
                    if (chance <= 0f) continue;
                    EquipRarity rarity = (EquipRarity)rarityIndex;
                    var pool = new List<EquipmentData>();
                    foreach (EquipmentData data in _table.items)
                        if (data.rarity == rarity) pool.Add(data);
                    for (int s = 0; s < EquipmentInventory.SlotCount; s++)
                    {
                        EquipmentData target = _table.Of((EquipSlot)s, rarity)[0];
                        int index = pool.IndexOf(target);
                        Assert.GreaterOrEqual(index, 0);
                        Match shown = Regex.Match(rates, Regex.Escape(target.displayName) + @": ([0-9.,]+)%");
                        Assert.IsTrue(shown.Success, $"확률표에 {target.displayName} 표시");
                        float shownPercent = float.Parse(shown.Groups[1].Value, CultureInfo.CurrentCulture);
                        Assert.AreEqual(chance * 100f / pool.Count, shownPercent, 1e-5f, $"{target.id} 개별 확률");
                        SaveData save = Unlocked();
                        int call = 0;
                        float Pick() => ++call == 1 ? previous + chance * 0.5f : (index + 0.5f) / pool.Count;
                        List<GachaService.Pull> draws = GachaService.Open(save, _gacha, box, 1, 1L, Pick);
                        Assert.AreEqual(target.id, draws[0].Data.id, $"{box.id} {rarity} {(EquipSlot)s}");
                        Assert.AreEqual(2, call);
                    }
                    previous += chance;
                }
                Assert.That(rates, Does.Contain(box.pity > 0 ? "천장" : "보장 없음"));
                if (box.pity > 0)
                {
                    Assert.That(rates, Does.Contain("10연 보장 7.225%"));
                    Assert.That(rates, Does.Contain("10연 보장 1.108333%, 천장 8.333333%"));
                }
            }
        }
    }
}
