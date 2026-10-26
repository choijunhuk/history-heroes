using System.Collections.Generic;
using HistoryHeroes.Records;
using HistoryHeroes.Runes;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>v0.4 룬 (Runes 2~7장)</summary>
    public sealed class RuneTests
    {
        private RuneTable _table;
        private RuneData _fire, _lightning, _projectile, _melee, _bond;

        private RuneData Rune(string id, SkillTag tag, RuneSource source, bool bond = false)
        {
            var r = ScriptableObject.CreateInstance<RuneData>();
            r.id = id;
            r.displayName = id + "의 룬";
            r.tag = tag;
            r.source = source;
            r.bond = bond;
            _table.runes.Add(r);
            return r;
        }

        private static T Skill<T>(string id, Rarity rarity, SkillTag tags) where T : SkillData
        {
            var s = ScriptableObject.CreateInstance<T>();
            s.id = id;
            s.rarity = rarity;
            s.tags = tags;
            s.maxLevel = 5;
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<RuneTable>();
            _fire = Rune("불", SkillTag.Fire, RuneSource.BossChoice);
            _lightning = Rune("번개", SkillTag.Lightning, RuneSource.BossChoice);
            _projectile = Rune("투사체", SkillTag.Projectile, RuneSource.BossChoice);
            _melee = Rune("근접", SkillTag.Melee, RuneSource.Shop);
            _bond = Rune("인연", SkillTag.None, RuneSource.Survival, true);
        }

        private sealed class Fixed : IWeightModifier
        {
            private readonly SkillTag _tag;
            private readonly float _m;
            public Fixed(SkillTag tag, float m) { _tag = tag; _m = m; }
            public float GetMultiplier(SkillData c, SkillLoadout l) => (c.tags & _tag) != 0 ? _m : 1f;
        }

        [Test]
        public void TakeWeighted_OnlyInsideTheRolledRarity()
        {
            var a = Skill<ActiveSkillData>("a", Rarity.Rare, SkillTag.Fire);
            var b = Skill<ActiveSkillData>("b", Rarity.Rare, SkillTag.Lightning);
            var legendary = Skill<ActiveSkillData>("legend", Rarity.Legendary, SkillTag.Fire);
            List<SkillCard> Pool() => new List<SkillCard>
            {
                new SkillCard { Type = CardType.NewSkill, Skill = a },
                new SkillCard { Type = CardType.NewSkill, Skill = b },
                new SkillCard { Type = CardType.NewSkill, Skill = legendary },
            };

            var mod = new Fixed(SkillTag.Fire, 2f);
            // 희귀 안에서 a 가중치 2, b 1 → 0~2/3은 a, 그 위는 b
            Assert.AreSame(a, SkillPicker.TakeWeighted(Pool(), SkillPicker.FallbackOrder(Rarity.Rare), mod, null, 0.6f).Skill);
            SkillCard bCard = SkillPicker.TakeWeighted(Pool(), SkillPicker.FallbackOrder(Rarity.Rare), mod, null, 0.7f);
            Assert.AreSame(b, bCard.Skill);
            Assert.IsFalse(bCard.RuneBoosted);
            Assert.IsTrue(SkillPicker.TakeWeighted(Pool(), SkillPicker.FallbackOrder(Rarity.Rare), mod, null, 0.1f).RuneBoosted, "룬 덕분에 나온 카드 표시");
            // 희귀가 뽑혔으면 불 태그 전설은 절대 안 나온다 (등급 추첨은 룬과 무관)
            for (float r = 0f; r < 1f; r += 0.05f)
            {
                Assert.AreNotEqual(Rarity.Legendary, SkillPicker.TakeWeighted(Pool(), SkillPicker.FallbackOrder(Rarity.Rare), mod, null, r).Skill.rarity);
            }
        }

        [Test]
        public void Slots_AndBossChoice_AndShop()
        {
            var save = new SaveData { gold = 50_000, unlockedChapter = 2 };
            Assert.AreEqual(0, RuneService.SlotCount(save, _table), "2구간 보스 전엔 칸 없음");
            Assert.IsNull(RuneService.PendingChoice(save, _table));
            Assert.IsFalse(RuneService.TryBuy(save, _table, _melee.id), "칸이 없으면 상점도 없음");

            save.unlockedChapter = 3;
            Assert.AreEqual(1, RuneService.SlotCount(save, _table));
            Assert.AreEqual("ch2", RuneService.PendingChoice(save, _table));
            Assert.AreEqual(3, RuneService.ChoiceOptions(save, _table).Count, "불·번개·투사체");
            Assert.IsFalse(RuneService.CanBuy(save, _table, _lightning), "고르기 전엔 보스 룬을 못 산다");
            Assert.IsFalse(RuneService.TakeChoice(save, _table, _melee.id), "상점 룬은 고르기 대상이 아님");
            Assert.IsTrue(RuneService.TakeChoice(save, _table, _fire.id));
            Assert.IsNull(RuneService.PendingChoice(save, _table));

            Assert.IsTrue(RuneService.TryBuy(save, _table, _lightning.id), "못 고른 룬은 상점에서 8,000");
            Assert.IsTrue(RuneService.TryBuy(save, _table, _melee.id));
            Assert.IsFalse(RuneService.TryBuy(save, _table, _melee.id), "이미 가짐");
            Assert.IsFalse(RuneService.CanBuy(save, _table, _bond), "인연의 룬은 서바이벌 첫 클리어");
            Assert.AreEqual(50_000 - 16_000, save.gold);

            save.unlockedChapter = 5;
            Assert.AreEqual(2, RuneService.SlotCount(save, _table));
            Assert.AreEqual("ch4", RuneService.PendingChoice(save, _table), "4구간 보스: 남은 것 중 고르기");
            Assert.AreEqual(2, RuneService.ChoiceOptions(save, _table).Count, "보상으로 안 고른 번개(이미 삼)·투사체");

            // 미리 산 번개를 보상으로 고르면 산 값을 돌려받는다 (확정 보상이 사라지지 않게, Codex v0.4 5번)
            int gold = save.gold;
            Assert.IsTrue(RuneService.TakeChoice(save, _table, _lightning.id));
            Assert.AreEqual(gold + _table.shopPrice, save.gold);
            Assert.IsNull(RuneService.PendingChoice(save, _table));
            Assert.AreEqual(1, RuneService.Level(save, _lightning.id), "룬은 그대로 하나");
        }

        [Test]
        public void BossChoice_StillThereWhenEveryBossRuneWasBought()
        {
            var save = new SaveData { gold = 100_000, unlockedChapter = 3 };
            Assert.IsTrue(RuneService.TakeChoice(save, _table, _fire.id));
            Assert.IsTrue(RuneService.TryBuy(save, _table, _lightning.id));
            Assert.IsTrue(RuneService.TryBuy(save, _table, _projectile.id));
            save.unlockedChapter = 5;
            Assert.AreEqual("ch4", RuneService.PendingChoice(save, _table), "다 사 두었어도 4구간 보상은 남는다");
            Assert.IsTrue(RuneService.TakeChoice(save, _table, _projectile.id));
            Assert.AreEqual(100_000 - 16_000 + 8_000, save.gold);
        }

        [Test]
        public void Upgrade_AndEquip_PerHero()
        {
            var save = new SaveData { gold = 13_000, unlockedChapter = 5 };
            RuneService.TakeChoice(save, _table, _fire.id);
            RuneService.TakeChoice(save, _table, _projectile.id);
            Assert.IsTrue(RuneService.TryUpgrade(save, _table, _fire.id));
            Assert.IsTrue(RuneService.TryUpgrade(save, _table, _fire.id));
            Assert.IsFalse(RuneService.TryUpgrade(save, _table, _fire.id), "Lv3이 끝");
            Assert.AreEqual(0, save.gold, "3,000 + 10,000");
            Assert.AreEqual(2.5f, _table.MultiplierAt(RuneService.Level(save, _fire.id)));

            Assert.IsTrue(RuneService.Equip(save, _table, "khan", 0, _fire.id));
            Assert.IsTrue(RuneService.Equip(save, _table, "khan", 1, _fire.id));
            Assert.AreEqual(string.Empty, RuneService.Equipped(save, "khan", 0), "같은 룬은 한 칸에만 (옮겨 감)");
            Assert.IsTrue(RuneService.Equip(save, _table, "guan", 0, _fire.id), "다른 영웅은 같이 껴도 됨");
            Assert.IsFalse(RuneService.Equip(save, _table, "khan", 0, _lightning.id), "없는 룬");
            Assert.IsFalse(RuneService.Equip(save, _table, "khan", 2, _projectile.id), "칸은 2개");
        }

        [Test]
        public void Modifier_StacksUpToThree_AndBondNeedsDiscoveredRecipeAndPartner()
        {
            var save = new SaveData { unlockedChapter = 5 };
            save.runes.owned.Add(new RuneLevel { id = _fire.id, level = 3 });
            save.runes.owned.Add(new RuneLevel { id = _lightning.id, level = 3 });
            RuneService.Equip(save, _table, "khan", 0, _fire.id);
            RuneService.Equip(save, _table, "khan", 1, _lightning.id);
            RuneWeightModifier mod = RuneService.ModifierFor(save, _table, "khan", null);
            var both = Skill<ActiveSkillData>("thunder_fire", Rarity.Rare, SkillTag.Fire | SkillTag.Lightning);
            Assert.AreEqual(3f, mod.GetMultiplier(both, new SkillLoadout()), 1e-5f, "2.5 × 2.5 → 최대 3.0");
            Assert.AreEqual(2.5f, mod.GetMultiplier(Skill<ActiveSkillData>("f", Rarity.Common, SkillTag.Fire), new SkillLoadout()), 1e-5f);
            Assert.IsNull(RuneService.ModifierFor(new SaveData { unlockedChapter = 5 }, _table, "khan", null), "룬이 없으면 영향 없음");

            // 인연의 룬
            var blade = Skill<ActiveSkillData>("blade", Rarity.Common, SkillTag.Melee);
            var def = Skill<PassiveSkillData>("def", Rarity.Common, SkillTag.Defense);
            var recipe = ScriptableObject.CreateInstance<FusionRecipe>();
            recipe.id = "test_bond_recipe";
            recipe.materialA = blade;
            recipe.materialB = def;
            var bondSave = new SaveData { unlockedChapter = 3 };
            bondSave.runes.owned.Add(new RuneLevel { id = _bond.id, level = 1 });
            RuneService.Equip(bondSave, _table, "khan", 0, _bond.id);

            var loadout = new SkillLoadout();
            loadout.AddNew(blade, 2);
            var oldSave = SaveSystem.Store;
            SaveSystem.Use(new SaveStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hh_rune_" + System.Guid.NewGuid().ToString("N"))));
            try
            {
                RuneWeightModifier bondMod = RuneService.ModifierFor(bondSave, _table, "khan", new[] { recipe });
                Assert.AreEqual(1f, bondMod.GetMultiplier(def, loadout), "발견하지 않은 레시피는 안 돕는다");
                RecipeBook.Discover(recipe);
                Assert.AreEqual(1.5f, bondMod.GetMultiplier(def, loadout), 1e-5f, "회전 칼날을 가지면 방어력이 잘 나온다");
                Assert.AreEqual(1f, bondMod.GetMultiplier(def, new SkillLoadout()), "짝 재료가 없으면 안 돕는다");
            }
            finally
            {
                SaveSystem.Use(oldSave);
            }
        }

        [Test]
        public void ShareText_ShowsRealChangeInsideRarity()
        {
            var pool = new List<SkillData>
            {
                Skill<ActiveSkillData>("f", Rarity.Rare, SkillTag.Fire),
                Skill<ActiveSkillData>("x", Rarity.Rare, SkillTag.Lightning),
                Skill<PassiveSkillData>("y", Rarity.Rare, SkillTag.Survival),
                Skill<PassiveSkillData>("z", Rarity.Rare, SkillTag.Projectile),
            };
            string text = RuneService.ShareText(_table, _fire, 2, pool, "khan");
            StringAssert.Contains("희귀 카드 중 불 스킬 25% → 40%", text, "Runes 2장 예시: 4개 중 1개, ×2.0");
            StringAssert.Contains("전설이 나올 확률은 바뀌지 않아요", text);
        }
    }
}
