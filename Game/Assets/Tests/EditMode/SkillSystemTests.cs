using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Economy;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class SkillSystemTests
    {
        private static T Make<T>(string id, int maxLevel = 5, string heroOnly = null) where T : SkillData
        {
            var s = ScriptableObject.CreateInstance<T>();
            s.id = id;
            s.maxLevel = maxLevel;
            s.heroOnlyId = heroOnly;
            return s;
        }

        [Test]
        public void MergeStartLevel_UsesExcessLevels()
        {
            var recipe = ScriptableObject.CreateInstance<FusionRecipe>();
            Assert.AreEqual(1, recipe.MergeStartLevel(3, 3));
            Assert.AreEqual(1, recipe.MergeStartLevel(4, 3));
            Assert.AreEqual(2, recipe.MergeStartLevel(4, 4));
            Assert.AreEqual(3, recipe.MergeStartLevel(5, 5));
        }

        [Test]
        public void Loadout_RespectsSlotLimits()
        {
            var loadout = new SkillLoadout(activeSlots: 1, passiveSlots: 1);
            loadout.AddNew(Make<ActiveSkillData>("a"));
            Assert.IsFalse(loadout.HasFreeSlot(SkillKind.Active));
            Assert.IsTrue(loadout.HasFreeSlot(SkillKind.Passive));
        }

        [Test]
        public void Candidates_SkipFullSlotsMaxLevelConsumedAndOtherHeroes()
        {
            var owned = Make<ActiveSkillData>("owned", maxLevel: 2);
            var newActive = Make<ActiveSkillData>("newActive");
            var consumed = Make<PassiveSkillData>("consumed");
            var otherHero = Make<PassiveSkillData>("other", heroOnly: "napoleon");
            var passive = Make<PassiveSkillData>("passive");

            var loadout = new SkillLoadout(activeSlots: 1, passiveSlots: 4);
            loadout.AddNew(owned, 1);
            loadout.MarkConsumed(consumed);

            var cards = SkillPicker.BuildCandidates(loadout, new SkillData[] { owned, newActive, consumed, otherHero, passive }, "khan");

            Assert.AreEqual(2, cards.Count, "owned 레벨업 + passive 신규만");
            Assert.IsTrue(cards.Exists(c => c.Skill == owned && c.Type == CardType.LevelUp && c.NextLevel == 2));
            Assert.IsTrue(cards.Exists(c => c.Skill == passive && c.Type == CardType.NewSkill));

            loadout.Find(owned).Level = 2;
            cards = SkillPicker.BuildCandidates(loadout, new SkillData[] { owned }, "khan");
            Assert.AreEqual(0, cards.Count, "최대 레벨이면 레벨업 카드 없음");
        }

        [Test]
        public void Candidates_IncludeFusionResultLevelUps()
        {
            var merged = Make<ActiveSkillData>("merged", maxLevel: 3);
            var loadout = new SkillLoadout();
            loadout.AddNew(merged, 1).IsFusionResult = true;

            var cards = SkillPicker.BuildCandidates(loadout, new SkillData[0], "khan");
            Assert.AreEqual(1, cards.Count);
            Assert.AreEqual(CardType.LevelUp, cards[0].Type);
        }

        [Test]
        public void PickSkills_NeverReturnsDuplicatesAndPadsWithFallback()
        {
            var a = Make<PassiveSkillData>("a");
            var b = Make<PassiveSkillData>("b");
            var candidates = SkillPicker.BuildCandidates(new SkillLoadout(), new SkillData[] { a, b }, "khan");
            var table = ScriptableObject.CreateInstance<RarityTable>();

            for (int i = 0; i < 50; i++)
            {
                var picked = SkillPicker.PickSkills(candidates, 3, 1, table);
                Assert.AreEqual(3, picked.Count);
                Assert.AreNotSame(picked[0].Skill, picked[1].Skill);
                Assert.AreEqual(CardType.Fallback, picked[2].Type);
            }
        }

        private static RarityTable Table(float common, float rare, float legendary)
        {
            var t = ScriptableObject.CreateInstance<RarityTable>();
            t.common = AnimationCurve.Constant(0f, 100f, common);
            t.rare = AnimationCurve.Constant(0f, 100f, rare);
            t.legendary = AnimationCurve.Constant(0f, 100f, legendary);
            return t;
        }

        private static List<SkillCard> CandidatesOf(params SkillData[] skills)
        {
            return SkillPicker.BuildCandidates(new SkillLoadout(), skills, "khan");
        }

        [Test]
        public void PickSkills_NeverUpgradesToLegendary()
        {
            var legend = Make<ActiveSkillData>("arrow_rain");
            legend.rarity = Rarity.Legendary;
            var picked = SkillPicker.PickSkills(CandidatesOf(legend), 3, 1, Table(common: 100, rare: 0, legendary: 0));
            Assert.IsTrue(picked.TrueForAll(c => c.Type == CardType.Fallback), "일반·희귀가 비어도 전설로 올라가지 않는다");
        }

        [Test]
        public void PickSkills_LegendaryRollFallsDownToCommon()
        {
            var common = Make<PassiveSkillData>("atk_up");
            var picked = SkillPicker.PickSkills(CandidatesOf(common), 1, 1, Table(common: 0, rare: 0, legendary: 100));
            Assert.AreSame(common, picked[0].Skill);
        }

        [Test]
        public void PickSkills_EmptyCommonGoesUpToRare()
        {
            var rare = Make<PassiveSkillData>("lifesteal");
            rare.rarity = Rarity.Rare;
            var picked = SkillPicker.PickSkills(CandidatesOf(rare), 1, 1, Table(common: 100, rare: 0, legendary: 0));
            Assert.AreSame(rare, picked[0].Skill);
        }

        [Test]
        public void PickSkills_RollsRarityByWeight()
        {
            var common = Make<PassiveSkillData>("c");
            var rare = Make<PassiveSkillData>("r");
            rare.rarity = Rarity.Rare;
            var table = Table(common: 0, rare: 100, legendary: 0);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreSame(rare, SkillPicker.PickSkills(CandidatesOf(common, rare), 1, 1, table)[0].Skill);
            }
        }

        private static FusionRecipe Recipe(FusionType type, ActiveSkillData a, int levelA, SkillData b, int levelB, string heroOnly = null)
        {
            var r = ScriptableObject.CreateInstance<FusionRecipe>();
            r.id = "recipe";
            r.type = type;
            r.materialA = a;
            r.requiredLevelA = levelA;
            r.materialB = b;
            r.requiredLevelB = levelB;
            r.result = Make<ActiveSkillData>("result");
            r.heroOnlyId = heroOnly;
            return r;
        }

        [Test]
        public void Fusions_EvolutionNeedsMaxLevelActiveAndPassive()
        {
            var blade = Make<ActiveSkillData>("spin_blade");
            var def = Make<PassiveSkillData>("def_up");
            var recipe = Recipe(FusionType.Evolution, blade, 5, def, 1);
            var loadout = new SkillLoadout();
            loadout.AddNew(blade, 4);
            loadout.AddNew(def, 1);

            Assert.AreEqual(0, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan").Count, "Lv4면 아직");
            loadout.Find(blade).Level = 5;
            Assert.AreEqual(1, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan").Count);
        }

        [Test]
        public void Fusions_MergeNeedsBothLevel3()
        {
            var lightning = Make<ActiveSkillData>("lightning");
            var flame = Make<ActiveSkillData>("flame_field");
            var recipe = Recipe(FusionType.Merge, lightning, 3, flame, 3);
            var loadout = new SkillLoadout();
            loadout.AddNew(lightning, 3);
            loadout.AddNew(flame, 2);

            Assert.AreEqual(0, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan").Count);
            loadout.Find(flame).Level = 3;
            Assert.AreEqual(1, FusionFinder.FindAvailableFusions(loadout, new[] { recipe }, "khan").Count);
        }

        [Test]
        public void Fusions_ExcludeHiddenCompletedChainedAndOtherHero()
        {
            var a = Make<ActiveSkillData>("a");
            var b = Make<ActiveSkillData>("b");
            var loadout = new SkillLoadout();
            loadout.AddNew(a, 3);
            loadout.AddNew(b, 3);

            var hidden = Recipe(FusionType.Merge, a, 3, b, 3);
            loadout.Hide(hidden);
            var completed = Recipe(FusionType.Merge, a, 3, b, 3);
            loadout.MarkCompleted(completed);
            var otherHero = Recipe(FusionType.Merge, a, 3, b, 3, heroOnly: "napoleon");
            Assert.AreEqual(0, FusionFinder.FindAvailableFusions(loadout, new[] { hidden, completed, otherHero }, "khan").Count);

            loadout.Find(a).IsFusionResult = true; // 조합 결과는 다시 재료가 되지 않는다
            var chained = Recipe(FusionType.Merge, a, 3, b, 3);
            Assert.AreEqual(0, FusionFinder.FindAvailableFusions(loadout, new[] { chained }, "khan").Count);
        }

        [Test]
        public void PlayerLevel_LevelsUpRepeatedlyFromOneBigGain()
        {
            var level = new PlayerLevel(baseRequirement: 1f);
            int events = 0;
            level.LeveledUp += _ => events++;

            // Lv1 필요 1, Lv2 필요 2^1.3 ≈ 2.46 → 3.5면 Lv3
            level.Add(3.5f);
            Assert.AreEqual(3, level.Level);
            Assert.AreEqual(2, events);
            Assert.Less(level.Experience, level.Required);
        }

        [Test]
        public void SaveCost_FirstLevelPlusPerLevel()
        {
            var merged = Make<ActiveSkillData>("thunder_fire", maxLevel: 3);
            merged.saveCostFirstLevel = 15;
            merged.saveCostPerLevel = 3;
            Assert.AreEqual(15, merged.SaveCostAt(1));
            Assert.AreEqual(21, merged.SaveCostAt(3));
        }

        [Test]
        public void PlayerLevel_AppliesExpGainBonus()
        {
            var level = new PlayerLevel(baseRequirement: 10f);
            level.Add(5f, expGainBonus: 0.5f);
            Assert.AreEqual(7.5f, level.Experience, 0.001f);
        }

        // ---------------- 리롤: 방금 본 카드 빼기 (조사 A1) ----------------

        [Test]
        public void Reroll_ExcludesShownCards_KeepsOthers()
        {
            var a = Make<PassiveSkillData>("a");
            var b = Make<PassiveSkillData>("b");
            var c = Make<PassiveSkillData>("c");
            var d = Make<PassiveSkillData>("d");
            var candidates = new List<SkillCard>
            {
                new SkillCard { Type = CardType.NewSkill, Skill = a }, new SkillCard { Type = CardType.NewSkill, Skill = b },
                new SkillCard { Type = CardType.NewSkill, Skill = c }, new SkillCard { Type = CardType.NewSkill, Skill = d },
            };
            var shown = new List<SkillCard> { new SkillCard { Type = CardType.NewSkill, Skill = a }, new SkillCard { Type = CardType.LevelUp, Skill = b }, SkillCard.Fallback() };

            SkillPicker.ExcludeShown(candidates, shown);

            Assert.AreEqual(2, candidates.Count);
            Assert.IsFalse(candidates.Exists(x => x.Skill == a || x.Skill == b));
        }

        [Test]
        public void Reroll_FewCandidates_StillFillsThreeCards()
        {
            var a = Make<PassiveSkillData>("a");
            var candidates = new List<SkillCard> { new SkillCard { Type = CardType.NewSkill, Skill = a } };
            SkillPicker.ExcludeShown(candidates, new List<SkillCard> { new SkillCard { Type = CardType.NewSkill, Skill = a } });

            List<SkillCard> picked = SkillPicker.PickSkills(candidates, 3, 1, null);

            Assert.AreEqual(3, picked.Count, "후보가 없어도 기본 카드로 채운다");
            Assert.IsTrue(picked.TrueForAll(x => x.Type == CardType.Fallback));
        }

        [Test]
        public void Reroll_FusionCardsAreNotExcluded()
        {
            var a = Make<PassiveSkillData>("a");
            var candidates = new List<SkillCard> { new SkillCard { Type = CardType.NewSkill, Skill = a } };
            var fusion = new SkillCard { Type = CardType.Fusion, Skill = a };
            SkillPicker.ExcludeShown(candidates, new List<SkillCard> { fusion });
            Assert.AreEqual(1, candidates.Count, "조합 카드는 고정이므로 빼기 대상이 아니다");
        }

        [Test]
        public void DamageLog_SharesBySource()
        {
            DamageLog.Reset();
            DamageLog.Record(new DamageInfo { Kind = DamageKind.BasicAttack }, 60f);
            DamageLog.Record(new DamageInfo { Kind = DamageKind.Active, SourceId = "lightning" }, 30f);
            DamageLog.Record(new DamageInfo { Kind = DamageKind.Active, SourceId = "lightning" }, 10f);
            DamageLog.Record(new DamageInfo { Kind = DamageKind.Active, SourceId = "flame" }, 0f); // 0은 안 센다

            List<KeyValuePair<string, float>> shares = DamageLog.Shares();
            Assert.AreEqual(2, shares.Count);
            Assert.AreEqual(DamageLog.BasicSource, shares[0].Key, "큰 것부터");
            Assert.AreEqual(0.6f, shares[0].Value, 1e-4f);
            Assert.AreEqual(0.4f, DamageLog.ShareOf("lightning"), 1e-4f);
            Assert.AreEqual(0f, DamageLog.ShareOf("flame"));
            Assert.AreEqual("기본 공격 60% · 기타 40%", RunText.TopDamage(null, null), "DB가 없으면 스킬 이름 대신 기타");
            DamageLog.Reset();
            Assert.AreEqual("피해 없음", RunText.TopDamage(null, null));
        }

        // ---------------- 조합 힌트 (조사 A3·A4) ----------------

        private static FusionRecipe Recipe(string id, FusionType type, ActiveSkillData a, SkillData b, ActiveSkillData result, int needA, int needB)
        {
            var r = ScriptableObject.CreateInstance<FusionRecipe>();
            r.id = id;
            r.type = type;
            r.materialA = a;
            r.materialB = b;
            r.result = result;
            r.requiredLevelA = needA;
            r.requiredLevelB = needB;
            return r;
        }

        [Test]
        public void Hints_NearestKnownRecipe_WithProgressAndOthers()
        {
            var flame = Make<ActiveSkillData>("flame");
            var lightning = Make<ActiveSkillData>("lightning");
            var atk = Make<PassiveSkillData>("atk");
            var trail = Make<ActiveSkillData>("trail");
            var storm = Make<ActiveSkillData>("storm");
            var secret = Make<ActiveSkillData>("secret");
            flame.displayName = "불꽃 장판";
            lightning.displayName = "낙뢰";
            atk.displayName = "공격력";
            trail.displayName = "불꽃 발자국";
            storm.displayName = "뇌화 폭풍";

            var evo = Recipe("evo", FusionType.Evolution, flame, atk, trail, 5, 1);
            var merge = Recipe("merge", FusionType.Merge, flame, lightning, storm, 3, 3);
            var unknown = Recipe("unknown", FusionType.Merge, flame, lightning, secret, 3, 3);
            var recipes = new[] { evo, merge, unknown };
            var known = new HashSet<FusionRecipe> { evo, merge };

            var loadout = new SkillLoadout();
            loadout.AddNew(flame, 2);
            loadout.AddNew(lightning, 3);

            Assert.IsTrue(FusionHints.TryNearest(flame, 3, loadout, recipes, "khan", known.Contains, out FusionHints.Progress p, out int others));
            Assert.AreSame(merge, p.Recipe, "낙뢰 3/3 + 불꽃 3/3인 합체가 진화(불꽃 3/5, 공격력 없음)보다 가깝다");
            Assert.AreEqual(1, others, "모르는 조합은 세지 않는다");
            StringAssert.Contains("뇌화 폭풍", FusionHints.CardLine(p, others));
            StringAssert.Contains("외 1", FusionHints.CardLine(p, others));

            Assert.IsFalse(FusionHints.TryNearest(atk, 1, loadout, new[] { unknown }, "khan", known.Contains, out _, out _), "모르는 조합만 있으면 힌트 없음");

            loadout.MarkCompleted(merge);
            Assert.IsTrue(FusionHints.TryNearest(flame, 3, loadout, recipes, "khan", known.Contains, out p, out others));
            Assert.AreSame(evo, p.Recipe, "이미 만든 조합은 빼고 찾는다");
            Assert.AreEqual(0, others);
        }

        [Test]
        public void Hints_BlockedBy_ListsOnlyKnownRecipesLosingAMaterial()
        {
            var flame = Make<ActiveSkillData>("flame");
            var lightning = Make<ActiveSkillData>("lightning");
            var ice = Make<ActiveSkillData>("ice");
            var atk = Make<PassiveSkillData>("atk");
            var trail = Make<ActiveSkillData>("trail");
            var storm = Make<ActiveSkillData>("storm");
            var god = Make<ActiveSkillData>("god");
            var frost = Make<ActiveSkillData>("frost");
            flame.displayName = "불꽃 장판";
            trail.displayName = "불꽃 발자국";

            var evoFlame = Recipe("evo_flame", FusionType.Evolution, flame, atk, trail, 5, 1);
            var stormR = Recipe("storm", FusionType.Merge, flame, lightning, storm, 3, 3);
            var evoLightning = Recipe("evo_lightning", FusionType.Evolution, lightning, atk, god, 5, 1);
            var iceR = Recipe("ice", FusionType.Merge, ice, lightning, frost, 3, 3);
            var recipes = new[] { evoFlame, stormR, evoLightning, iceR };
            var loadout = new SkillLoadout();

            // 합체(불꽃+낙뢰)를 고르면 불꽃·낙뢰 둘 다 사라진다 → 불꽃 진화, 낙뢰 진화가 막힘. 얼음 합체는 모르는 조합이라 말 안 함
            var known = new HashSet<FusionRecipe> { evoFlame, stormR, evoLightning };
            var blocked = FusionHints.BlockedBy(stormR, loadout, recipes, "khan", known.Contains);
            Assert.AreEqual(2, blocked.Count);
            Assert.IsTrue(blocked.Exists(x => x.blocked == evoFlame && x.lost == flame));
            Assert.IsTrue(blocked.Exists(x => x.blocked == evoLightning && x.lost == lightning));
            StringAssert.Contains("불꽃 발자국", FusionHints.BlockedLine(blocked));

            // 진화(불꽃)는 액티브만 사라진다 → 패시브(공격력)를 쓰는 낙뢰 진화는 막히지 않는다
            blocked = FusionHints.BlockedBy(evoFlame, loadout, recipes, "khan", known.Contains);
            Assert.AreEqual(1, blocked.Count);
            Assert.AreSame(stormR, blocked[0].blocked);

            Assert.AreEqual(string.Empty, FusionHints.BlockedLine(new List<(SkillData, FusionRecipe)>()));
        }

        [Test]
        public void Hints_SkipRecipesWhoseMissingMaterialHasNoFreeSlot()
        {
            var blade = Make<ActiveSkillData>("blade");
            var def = Make<PassiveSkillData>("def");
            var whirl = Make<ActiveSkillData>("whirl");
            var lightning = Make<ActiveSkillData>("lightning");
            var storm = Make<ActiveSkillData>("storm");
            var evo = Recipe("evo", FusionType.Evolution, blade, def, whirl, 5, 1);
            var merge = Recipe("merge", FusionType.Merge, blade, lightning, storm, 3, 3);
            var known = new HashSet<FusionRecipe> { evo, merge };

            // 패시브 칸이 다 찼는데 방어력이 없다 → 강철 회오리는 이번 판에 못 만든다 (Codex v0.3b 5번)
            var loadout = new SkillLoadout(activeSlots: 4, passiveSlots: 2);
            loadout.AddNew(blade, 3);
            loadout.AddNew(Make<PassiveSkillData>("p1"));
            loadout.AddNew(Make<PassiveSkillData>("p2"));

            Assert.IsFalse(FusionHints.Obtainable(evo, loadout));
            Assert.IsTrue(FusionHints.TryNearest(blade, 4, loadout, new[] { evo, merge }, "khan", known.Contains, out FusionHints.Progress p, out int others));
            Assert.AreSame(merge, p.Recipe, "못 만드는 진화 대신 만들 수 있는 합체를 보여준다");
            Assert.AreEqual(0, others);
            Assert.IsFalse(FusionHints.TryNearest(blade, 4, loadout, new[] { evo }, "khan", known.Contains, out _, out _));

            // 새 액티브 카드 + 짝도 새 액티브인데 액티브 칸이 1개뿐 → 둘 다 들어갈 수 없다
            var tight = new SkillLoadout(activeSlots: 4, passiveSlots: 4);
            tight.AddNew(Make<ActiveSkillData>("a1"));
            tight.AddNew(Make<ActiveSkillData>("a2"));
            tight.AddNew(Make<ActiveSkillData>("a3"));
            Assert.IsFalse(FusionHints.Obtainable(merge, tight), "칼날·낙뢰 둘 다 없고 빈 칸은 1개");
        }

        [Test]
        public void Hints_CountForCodex()
        {
            var flame = Make<ActiveSkillData>("flame");
            var atk = Make<PassiveSkillData>("atk");
            var lightning = Make<ActiveSkillData>("lightning");
            var a = Recipe("a", FusionType.Evolution, flame, atk, Make<ActiveSkillData>("r1"), 5, 1);
            var b = Recipe("b", FusionType.Merge, flame, lightning, Make<ActiveSkillData>("r2"), 3, 3);
            var known = new HashSet<FusionRecipe> { a };

            Assert.AreEqual((2, 1), FusionHints.CountFor(flame, new[] { a, b }, known.Contains));
            Assert.AreEqual((1, 0), FusionHints.CountFor(lightning, new[] { a, b }, known.Contains));
            Assert.AreEqual((0, 0), FusionHints.CountFor(Make<PassiveSkillData>("none"), new[] { a, b }, known.Contains));
        }

        // ---------------- 손맛 (조사 A9) ----------------

        [Test]
        public void GamePause_PauseAlwaysBeatsSlow_AndResumeReturnsToSlow()
        {
            object card = new object();
            object hit = new object();
            try
            {
                HistoryHeroes.Common.GamePause.ResetAll();
                HistoryHeroes.Common.GamePause.RequestSlow(hit, 0.3f, 10f);
                Assert.AreEqual(0.3f, Time.timeScale, 1e-4f, "느리게 요청 중 가장 작은 값");

                HistoryHeroes.Common.GamePause.Pause(card);
                Assert.AreEqual(0f, Time.timeScale, "멈춤(레벨업 창)이 언제나 이긴다");
                HistoryHeroes.Common.GamePause.RequestSlow(new object(), 0.5f, 10f);
                Assert.AreEqual(0f, Time.timeScale, "멈춘 동안 새 느리게 요청이 와도 0");

                HistoryHeroes.Common.GamePause.Resume(card);
                Assert.AreEqual(0.3f, Time.timeScale, 1e-4f, "창을 닫으면 남은 느리게로 돌아간다");
                Assert.IsFalse(HistoryHeroes.Common.GamePause.IsPaused, "느리게는 멈춤이 아니다 (스킬 판정은 계속)");
            }
            finally
            {
                HistoryHeroes.Common.GamePause.ResetAll();
            }

            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void DamageNumbers_FormatAndShakeSetting()
        {
            Assert.AreEqual("1", DamageNumbers.Format(0.2f), "0이 아니라 1부터");
            Assert.AreEqual("1234", DamageNumbers.Format(1234.4f));
            Assert.AreEqual("1.2만", DamageNumbers.Format(12345f));
            Assert.AreEqual("12만", DamageNumbers.Format(123456f));

            Assert.AreEqual(0f, HistoryHeroes.Common.CameraShake.SettingMultiplier(0));
            Assert.AreEqual(0.5f, HistoryHeroes.Common.CameraShake.SettingMultiplier(1));
            Assert.AreEqual(1f, HistoryHeroes.Common.CameraShake.SettingMultiplier(2));
        }
    }
}
