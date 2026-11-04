using System.Collections.Generic;
using System.Reflection;
using HistoryHeroes.Common;
using HistoryHeroes.Heroes;
using HistoryHeroes.Player;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class HeroIdentityOfferTests
    {
        private readonly List<Object> _created = new List<Object>();
        private bool _wasTutorial;

        [SetUp]
        public void SetUp()
        {
            _wasTutorial = GameSession.Tutorial;
            SetTutorial(false);
        }

        [TearDown]
        public void TearDown()
        {
            SetTutorial(_wasTutorial);
            for (int i = _created.Count - 1; i >= 0; i--) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private T Asset<T>() where T : ScriptableObject
        {
            T item = ScriptableObject.CreateInstance<T>();
            _created.Add(item);
            return item;
        }

        private ActiveSkillData Active(string id, string heroId = null, Rarity rarity = Rarity.Common)
        {
            ActiveSkillData skill = Asset<ActiveSkillData>();
            skill.id = id;
            skill.heroOnlyId = heroId;
            skill.rarity = rarity;
            return skill;
        }

        private SkillManager Manager(out ActiveSkillData exclusive, out List<SkillData> pool)
        {
            exclusive = Active("khan_own", "khan", Rarity.Rare);
            pool = new List<SkillData> { exclusive, Active("common_1"), Active("common_2"), Active("common_3"), Active("common_4") };
            HeroData hero = Asset<HeroData>();
            hero.id = "khan";
            hero.exclusiveActive = exclusive;
            RarityTable rarity = Asset<RarityTable>();
            rarity.common = AnimationCurve.Constant(0f, 100f, 100f);
            rarity.rare = AnimationCurve.Constant(0f, 100f, 0f);
            rarity.legendary = AnimationCurve.Constant(0f, 100f, 0f);
            GameObject go = new GameObject("HeroIdentityOfferTest");
            go.SetActive(false);
            _created.Add(go);
            PlayerController player = go.AddComponent<PlayerController>();
            SkillManager manager = go.AddComponent<SkillManager>();
            SetField(player, "hero", hero);
            SetField(manager, "player", player);
            SetField(manager, "skillPool", pool);
            SetField(manager, "rarityTable", rarity);
            return manager;
        }

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void SetTutorial(bool value)
            => typeof(GameSession).GetProperty("Tutorial", BindingFlags.Static | BindingFlags.Public).SetValue(null, value);

        [Test]
        public void EveryNewRunOffersExclusiveOnceWithoutLeakingAnotherHero()
        {
            for (int run = 0; run < 5; run++)
            {
                SkillManager manager = Manager(out ActiveSkillData exclusive, out List<SkillData> pool);
                pool.Add(Active("other_own", "guan", Rarity.Rare));
                List<SkillCard> first = manager.CreateOffer(1);
                Assert.AreEqual(1, first.FindAll(c => c.Skill == exclusive).Count);
                Assert.IsFalse(first.Exists(c => c.Skill != null && c.Skill.heroOnlyId == "guan"));
                Assert.IsFalse(manager.CreateOffer(1).Exists(c => c.Skill == exclusive), "둘째 묶음은 기존 등급 추첨을 따른다");
            }
        }

        [Test]
        public void TutorialAndForcedCardsKeepTheirThreeSlots()
        {
            SkillManager manager = Manager(out ActiveSkillData exclusive, out _);
            manager.TutorialCards = new[] { "common_1", "common_2", "common_3" };
            Assert.IsFalse(manager.CreateOffer(1).Exists(c => c.Skill == exclusive));

            SkillManager tutorial = Manager(out exclusive, out _);
            SetTutorial(true);
            Assert.IsFalse(tutorial.CreateOffer(1).Exists(c => c.Skill == exclusive));
        }

        [Test]
        public void BannedOrOccupiedExclusiveCannotBeForced()
        {
            SkillManager banned = Manager(out ActiveSkillData exclusive, out _);
            Assert.IsTrue(banned.Loadout.TryBan(new SkillCard { Type = CardType.NewSkill, Skill = exclusive }));
            Assert.IsFalse(banned.CreateOffer(1).Exists(c => c.Skill == exclusive));

            SkillManager full = Manager(out exclusive, out _);
            full.ConfigureSlots(1, 4);
            full.Loadout.AddNew(Active("occupied"));
            Assert.IsFalse(full.CreateOffer(1).Exists(c => c.Skill == exclusive));

            SkillManager maxed = Manager(out exclusive, out _);
            maxed.Loadout.AddNew(exclusive, exclusive.maxLevel);
            Assert.IsFalse(maxed.CreateOffer(1).Exists(c => c.Skill == exclusive));

            SkillManager owned = Manager(out exclusive, out _);
            owned.Loadout.AddNew(exclusive);
            Assert.IsFalse(owned.CreateOffer(1).Exists(c => c.Skill == exclusive), "이미 배운 전용기는 첫 카드 보장 대상이 아니다");
        }

        [Test]
        public void RestoredOfferCountDoesNotForceExclusiveAgain()
        {
            SkillManager manager = Manager(out ActiveSkillData exclusive, out _);
            manager.RestoreRunState(1, false);
            Assert.IsFalse(manager.CreateOffer(1).Exists(c => c.Skill == exclusive));
        }

        [Test]
        public void FirstOfferPreservesLegendaryWhenACommonCardCanBeReplaced()
        {
            SkillManager manager = Manager(out ActiveSkillData exclusive, out _);
            ActiveSkillData legendary = Active("legendary", rarity: Rarity.Legendary);
            ActiveSkillData common = Active("common");
            var offer = new List<SkillCard>
            {
                new SkillCard { Type = CardType.NewSkill, Skill = common },
                new SkillCard { Type = CardType.NewSkill, Skill = legendary }
            };
            var candidates = new List<SkillCard> { new SkillCard { Type = CardType.NewSkill, Skill = exclusive } };
            typeof(SkillManager).GetMethod("BoostExclusive", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { offer, candidates });
            Assert.IsTrue(offer.Exists(c => c.Skill == exclusive));
            Assert.IsTrue(offer.Exists(c => c.Skill == legendary));
        }

        [Test]
        public void AvailableFusionKeepsItsSlotWhenExclusiveIsAdded()
        {
            SkillManager manager = Manager(out ActiveSkillData exclusive, out List<SkillData> pool);
            ActiveSkillData material = Active("material");
            PassiveSkillData passive = Asset<PassiveSkillData>();
            passive.id = "passive";
            pool.Add(material);
            pool.Add(passive);
            manager.Loadout.AddNew(material, 5);
            manager.Loadout.AddNew(passive);
            FusionRecipe recipe = Asset<FusionRecipe>();
            recipe.id = "fusion";
            recipe.type = FusionType.Evolution;
            recipe.materialA = material;
            recipe.materialB = passive;
            recipe.result = Active("result");
            SetField(manager, "recipes", new List<FusionRecipe> { recipe });

            List<SkillCard> offer = manager.CreateOffer(1);
            Assert.IsTrue(offer.Exists(c => c.Type == CardType.Fusion && c.Recipe == recipe));
            Assert.IsTrue(offer.Exists(c => c.Skill == exclusive));
            Assert.AreEqual(3, offer.Count);
        }

        [Test]
        public void AllRosterHeroesExposeTheirActualExclusiveRecipeAndBothRequirements()
        {
            HeroRoster roster = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroRoster>("Assets/Data/Heroes/HeroRoster.asset");
            SkillDatabase database = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            Assert.IsNotNull(roster);
            Assert.IsNotNull(database);
            Assert.AreEqual(21, roster.heroes.Count);
            MethodInfo render = typeof(HeroPanel).GetMethod("SkillDetails", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(render);
            var activeIds = new HashSet<string>();
            var recipeIds = new HashSet<string>();

            foreach (HeroData hero in roster.heroes)
            {
                Assert.IsNotNull(hero.exclusiveActive, hero.id);
                Assert.IsTrue(activeIds.Add(hero.exclusiveActive.id), hero.id + " 전용기 중복");
                FusionRecipe recipe = database.recipes.Find(r => r != null && r.heroOnlyId == hero.id && r.materialA == hero.exclusiveActive);
                Assert.IsNotNull(recipe, hero.id + " 전용 조합 누락");
                Assert.IsNotNull(recipe.materialB, hero.id);
                Assert.IsNotNull(recipe.result, hero.id);
                Assert.IsTrue(recipeIds.Add(recipe.id), hero.id + " 전용 조합 중복");
                var rows = (List<ChoicePanel.Option>)render.Invoke(null, new object[] { hero, database });
                StringAssert.Contains(hero.exclusiveActive.displayName, rows[1].Text, hero.id);
                StringAssert.Contains(recipe.result.displayName, rows[2].Text, hero.id);
                StringAssert.Contains(recipe.type == FusionType.Evolution ? "전용 진화" : "전용 합체", rows[2].Text, hero.id);
                StringAssert.Contains($"{recipe.materialA.displayName} Lv{recipe.requiredLevelA}", rows[2].Text, hero.id);
                StringAssert.Contains($"{recipe.materialB.displayName} Lv{recipe.requiredLevelB}", rows[2].Text, hero.id);
            }
        }
    }
}
