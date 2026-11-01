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
    public sealed class RecipeTargetTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private SaveStore _previousStore;

        [SetUp]
        public void SetUp()
        {
            _previousStore = SaveSystem.Store;
            SaveSystem.Use(new SaveStore(Path.Combine(Path.GetTempPath(), "hh_recipe_target_" + Guid.NewGuid().ToString("N"))));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object obj in _objects) UnityEngine.Object.DestroyImmediate(obj);
            _objects.Clear();
            SaveSystem.Use(_previousStore);
        }

        private T Asset<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _objects.Add(value);
            return value;
        }

        private FusionRecipe Recipe(string id)
        {
            FusionRecipe recipe = Asset<FusionRecipe>();
            recipe.id = id;
            return recipe;
        }

        private SkillManager Manager(List<SkillData> pool, List<FusionRecipe> recipes, RarityTable rarity = null)
        {
            var go = new GameObject("RecipeTargetTest");
            go.SetActive(false);
            _objects.Add(go);
            var player = go.AddComponent<PlayerController>();
            var manager = go.AddComponent<SkillManager>();
            Set(manager, "player", player);
            Set(manager, "skillPool", pool);
            Set(manager, "recipes", recipes);
            Set(manager, "rarityTable", rarity);
            return manager;
        }

        private static void Set(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(instance, value);
        }

        [Test]
        public void Targets_OnlyDiscoveredRecipes_AtMostTwo_AndCanRemove()
        {
            var save = new SaveData();
            FusionRecipe a = Recipe("a"), b = Recipe("b"), c = Recipe("c"), hidden = Recipe("hidden");
            save.discoveredRecipes.AddRange(new[] { "a", "b", "c" });

            Assert.IsFalse(RecipeBook.TrySetTarget(save, hidden, true));
            Assert.IsTrue(RecipeBook.TrySetTarget(save, a, true));
            Assert.IsTrue(RecipeBook.TrySetTarget(save, b, true));
            Assert.IsFalse(RecipeBook.TrySetTarget(save, a, true), "중복 선택은 변경 없음");
            Assert.IsFalse(RecipeBook.TrySetTarget(save, c, true), "세 번째 목표는 거부");
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, save.targetRecipes);
            Assert.IsTrue(RecipeBook.IsTargeted(save, a));
            Assert.IsTrue(RecipeBook.TrySetTarget(save, a, false));
            Assert.IsFalse(RecipeBook.IsTargeted(save, a));
            Assert.IsTrue(RecipeBook.TrySetTarget(save, c, true));
            CollectionAssert.AreEquivalent(new[] { "b", "c" }, save.targetRecipes);
        }

        [Test]
        public void Targets_InvalidOrUnchangedRequests_DoNotChangeSave()
        {
            var save = new SaveData();
            save.discoveredRecipes.Add("a");
            FusionRecipe invalid = Recipe("");
            FusionRecipe a = Recipe("a");
            Assert.IsFalse(RecipeBook.TrySetTarget(save, invalid, true));
            Assert.IsFalse(RecipeBook.TrySetTarget(save, a, false));
            Assert.IsFalse(RecipeBook.TrySetTarget(null, a, true));
            Assert.IsFalse(RecipeBook.IsTargeted(null, a));
            Assert.IsEmpty(save.targetRecipes);
            save.targetRecipes.Add("hidden");
            Assert.IsFalse(RecipeBook.IsTargeted(save, Recipe("hidden")), "오염된 저장 값도 미발견 결과를 공개하지 않는다");
        }

        [Test]
        public void TargetSaveFailure_RollsBackSelection()
        {
            string blocker = Path.GetTempFileName();
            try
            {
                SaveSystem.Use(new SaveStore(Path.Combine(blocker, "sub")));
                FusionRecipe recipe = Recipe("a");
                SaveSystem.Data.discoveredRecipes.Add(recipe.id);
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("저장 실패"));
                Assert.AreEqual(SaveSystem.CommitResult.SaveFailed, RecipeBook.TrySetTarget(recipe, true));
                Assert.IsFalse(RecipeBook.IsTargeted(recipe));
            }
            finally
            {
                File.Delete(blocker);
            }
        }

        [Test]
        public void CardHint_ShowsTargetAndExistingProgressTogether()
        {
            var material = Asset<ActiveSkillData>();
            material.id = "blade";
            material.displayName = "회전 칼날";
            var passive = Asset<PassiveSkillData>();
            passive.id = "defense";
            passive.displayName = "방어력";
            var result = Asset<ActiveSkillData>();
            result.id = "whirl";
            result.displayName = "강철 회오리";
            FusionRecipe recipe = Recipe("whirl_recipe");
            recipe.type = FusionType.Evolution;
            recipe.materialA = material;
            recipe.materialB = passive;
            recipe.result = result;
            recipe.requiredLevelA = 5;
            recipe.requiredLevelB = 1;
            SaveSystem.Data.discoveredRecipes.Add(recipe.id);
            Assert.IsTrue(RecipeBook.TrySetTarget(SaveSystem.Data, recipe, true));

            SkillManager manager = Manager(new List<SkillData> { material, passive }, new List<FusionRecipe> { recipe });
            manager.Loadout.AddNew(material, 2);
            string line = manager.CardHint(new SkillCard { Type = CardType.LevelUp, Skill = material, NextLevel = 3 });
            StringAssert.Contains("★ 목표: 강철 회오리", line);
            StringAssert.Contains("◆ 강철 회오리", line);
            StringAssert.Contains("3/5", line);
            string newSkillLine = manager.CardHint(new SkillCard { Type = CardType.NewSkill, Skill = passive, NextLevel = 1 });
            StringAssert.Contains("★ 목표: 강철 회오리", newSkillLine);
        }

        [Test]
        public void TargetSelection_DoesNotChangeFixedSeedOfferOrCardRandomState()
        {
            var pool = new List<SkillData>();
            for (int i = 0; i < 5; i++)
            {
                var skill = Asset<ActiveSkillData>();
                skill.id = "skill_" + i;
                skill.rarity = Rarity.Common;
                pool.Add(skill);
            }
            var passive = Asset<PassiveSkillData>();
            passive.id = "passive";
            var result = Asset<ActiveSkillData>();
            result.id = "result";
            FusionRecipe recipe = Recipe("recipe");
            recipe.materialA = (ActiveSkillData)pool[0];
            recipe.materialB = passive;
            recipe.result = result;
            SaveSystem.Data.discoveredRecipes.Add(recipe.id);
            RarityTable rarity = Asset<RarityTable>();
            var recipes = new List<FusionRecipe> { recipe };

            List<SkillCard> candidatesBefore = SkillPicker.BuildCandidates(new SkillLoadout(), pool, string.Empty);
            RunRandom.Begin(341);
            List<SkillCard> before = Manager(pool, recipes, rarity).CreateOffer(1);
            string stateBefore = RunRandom.Capture()[2];
            Assert.IsTrue(RecipeBook.TrySetTarget(SaveSystem.Data, recipe, true));
            List<SkillCard> candidatesAfter = SkillPicker.BuildCandidates(new SkillLoadout(), pool, string.Empty);
            RunRandom.Begin(341);
            List<SkillCard> after = Manager(pool, recipes, rarity).CreateOffer(1);
            string stateAfter = RunRandom.Capture()[2];

            Assert.AreEqual(candidatesBefore.Count, candidatesAfter.Count);
            for (int i = 0; i < candidatesBefore.Count; i++)
            {
                Assert.AreEqual(candidatesBefore[i].Type, candidatesAfter[i].Type);
                Assert.AreEqual(candidatesBefore[i].Skill, candidatesAfter[i].Skill);
            }
            Assert.AreEqual(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].Type, after[i].Type);
                Assert.AreEqual(before[i].Skill, after[i].Skill);
            }
            Assert.AreEqual(stateBefore, stateAfter, "목표 선택은 카드 난수를 소비하지 않는다");
        }
    }
}
