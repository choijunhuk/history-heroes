using HistoryHeroes.Combat;
using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Player;
using HistoryHeroes.Records;
using HistoryHeroes.Stats;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>새 영웅 이순신 (확장 결정 12): 철갑 숫자 · 별 강화 · 명단 · 전용 액티브 · 학익진 조합</summary>
    public sealed class YiSunsinTests
    {
        private static HeroData Load() => UnityEditor.AssetDatabase.LoadAssetAtPath<HeroRoster>("Assets/Data/Heroes/HeroRoster.asset")?.Find("yisunsin");

        [Test]
        public void Ironclad_StarsRaiseGuardAndCap()
        {
            var yi = ScriptableObject.CreateInstance<HeroData>();
            yi.stopGuard = 0.25f;
            yi.stopGuardCap = 2f;
            yi.star3StopGuard = 0.3f;
            yi.star5StopGuardCap = 3f;
            try
            {
                Assert.AreEqual(0.25f, HeroSignature.For(yi, 1).StopGuard, 1e-5f);
                Assert.AreEqual(2f, HeroSignature.For(yi, 1).StopGuardCap, 1e-5f);
                Assert.AreEqual(0.3f, HeroSignature.For(yi, 3).StopGuard, 1e-5f, "★3 멈춤 받는 피해 -30%");
                Assert.AreEqual(2f, HeroSignature.For(yi, 4).StopGuardCap, 1e-5f);
                Assert.AreEqual(3f, HeroSignature.For(yi, 5).StopGuardCap, 1e-5f, "★5 쌓기 상한 300%");
                StringAssert.Contains("★3 멈춤 받는 피해 -30%", HeroSignature.StarText(yi));

                var khan = ScriptableObject.CreateInstance<HeroData>();
                Assert.AreEqual(0f, HeroSignature.For(khan, 5).StopGuard, "다른 영웅은 철갑 없음");
                Object.DestroyImmediate(khan);
            }
            finally
            {
                Object.DestroyImmediate(yi);
            }
        }

        [Test]
        public void YiSeonggye_ChargeShot_StarsAndRoster()
        {
            var ysg = ScriptableObject.CreateInstance<HeroData>();
            ysg.chargeShot = true;
            ysg.chargeTimes = new Vector3(0.4f, 0.9f, 1.5f);
            ysg.chargeFullMultiplier = 2.5f;
            ysg.star3ChargeFull = 3f;
            ysg.star5ChargeFullTime = 1.2f;
            try
            {
                Assert.AreEqual(2.5f, HeroSignature.For(ysg, 1).ChargeFull, 1e-5f);
                Assert.AreEqual(1.5f, HeroSignature.For(ysg, 1).ChargeFullTime, 1e-5f);
                Assert.AreEqual(3f, HeroSignature.For(ysg, 3).ChargeFull, 1e-5f, "★3 3단 ×3");
                Assert.AreEqual(1.2f, HeroSignature.For(ysg, 5).ChargeFullTime, 1e-5f, "★5 3단까지 1.2초");
            }
            finally
            {
                Object.DestroyImmediate(ysg);
            }

            HeroData hero = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroRoster>("Assets/Data/Heroes/HeroRoster.asset")?.Find("yiseonggye");
            Assert.IsNotNull(hero, "명단에 이성계");
            Assert.IsTrue(hero.chargeShot);
            Assert.AreEqual("ysg_pyeonjeon", hero.exclusiveActive.id);
            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            Assert.AreEqual("yiseonggye", db.FindRecipe("ysg_shingung").heroOnlyId);
        }

        [Test]
        public void Roster_HasYiWithExclusiveAndFusion()
        {
            HeroData yi = Load();
            Assert.IsNotNull(yi, "명단에 이순신 (장면 빌더)");
            Assert.AreEqual(HeroClass.Gunner, yi.heroClass);
            Assert.AreEqual(1250f, yi.maxHp);
            Assert.AreEqual(0.25f, yi.stopGuard, 1e-5f);
            Assert.IsNotNull(yi.projectilePrefab, "총통 포탄");
            Assert.IsNotNull(yi.exclusiveActive, "전용 액티브");
            Assert.AreEqual("yi_turtle", yi.exclusiveActive.id);
            Assert.IsNotNull(yi.exclusiveActive.behaviourPrefab, "거북선 돌격 동작");

            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            FusionRecipe recipe = db.FindRecipe("yi_hakikjin");
            Assert.IsNotNull(recipe, "학익진 조합");
            Assert.AreEqual(FusionType.Evolution, recipe.type);
            Assert.AreEqual("yisunsin", recipe.heroOnlyId, "이순신만");
            Assert.IsNotNull(((ActiveSkillData)recipe.result).behaviourPrefab, "학익진 동작");
            CollectionAssert.IsEmpty(db.FindProblems(), "스킬 데이터 문제 없음");
        }


        // ---------------------------------------------------------------- 새 영웅 코드 검토 (2026-10-28)

        [Test]
        public void GemsOnlyHeroes_StayLockedUntilBought()
        {
            var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroRoster>("Assets/Data/Heroes/HeroRoster.asset");
            foreach (string id in new[] { "davinci", "zhuge" })
            {
                HeroData h = roster.Find(id);
                Assert.AreEqual(id, h.id);
                Assert.IsTrue(h.gemsOnly, id + " 보석으로만");
                var save = new SaveData();
                Assert.IsFalse(HeroUnlock.IsUnlocked(save, h), id + " 새 계정에서 잠김");
                save.unlockedChapter = 9;
                Assert.IsFalse(HeroUnlock.IsUnlocked(save, h), id + " 구간을 다 깨도 잠김");
                Wallet.AddFreeGems(save, 1999);
                Assert.IsFalse(HeroUnlock.TryBuy(save, h), "보석이 모자라면 못 산다");
                Wallet.AddFreeGems(save, 1);
                Assert.IsTrue(HeroUnlock.TryBuy(save, h));
                Assert.AreEqual(0, Wallet.Gems(save), "정확히 2,000 빠진다");
                Assert.IsTrue(HeroUnlock.IsUnlocked(save, h));
                StringAssert.Contains("보석 2,000개로 열려요", HeroUnlock.LockText(h));
            }

            Assert.IsTrue(HeroUnlock.IsUnlocked(new SaveData(), roster.Default), "칭기즈 칸은 처음부터");
            StringAssert.Contains("(또는 보석", HeroUnlock.LockText(roster.Find("guan")));
        }

        [Test]
        public void ShotSpread_FanWithMultishotKeepsCenterCovered()
        {
            // 제갈량 기본: 3갈래 ±18°
            PlayerController.ShotSpread(3, 18f, 10f, 3, out float start, out float step);
            Assert.AreEqual(-18f, start, 1e-4f);
            Assert.AreEqual(18f, step, 1e-4f);

            // 멀티샷 1~3: 부채 넓이는 그대로 (±18°), 거리 4.5 단일 적(반경 0.45 + 탄 0.2)에 적어도 한 발은 맞는다
            for (int extra = 1; extra <= 3; extra++)
            {
                int count = 3 + extra;
                PlayerController.ShotSpread(3, 18f, 10f, count, out start, out step);
                Assert.AreEqual(-18f, start, 1e-4f, "부채 끝은 그대로");
                Assert.AreEqual(18f, start + step * (count - 1), 1e-4f);
                float nearest = 180f;
                for (int i = 0; i < count; i++) nearest = Mathf.Min(nearest, Mathf.Abs(start + step * i));
                Assert.Less(4.5f * Mathf.Sin(nearest * Mathf.Deg2Rad), 0.65f, $"멀티샷 {extra}: 가운데 적을 놓치지 않는다");
            }

            // 보통 영웅은 그대로 (한 발 간격 normalSpread)
            PlayerController.ShotSpread(1, 20f, 10f, 3, out start, out step);
            Assert.AreEqual(10f, step, 1e-4f);
            Assert.AreEqual(-10f, start, 1e-4f);
        }

        [Test]
        public void Ironclad_BlockedShareRespectsOtherReductionAndCap()
        {
            var iron = new object();
            var other = new object();
            var stats = new StatSheet();
            stats.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, 0.22f, ModifierSource.Equipment, other));
            stats.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, 0.25f, ModifierSource.Hero, iron));
            Assert.AreEqual(0.47f, stats.Get(StatType.DamageReduction), 1e-5f);
            Assert.AreEqual(0.22f, stats.GetWithout(StatType.DamageReduction, iron), 1e-5f);

            // 상한 60%: 다른 감소 50% + 철갑 25% → 철갑 몫은 10%뿐
            stats.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, 0.28f, ModifierSource.Equipment, other));
            Assert.AreEqual(0.6f, stats.Get(StatType.DamageReduction), 1e-5f);
            Assert.AreEqual(0.5f, stats.GetWithout(StatType.DamageReduction, iron), 1e-5f);
        }

        [Test]
        public void Health_LifeCountsSpawnsAndRemembersRawDamage()
        {
            var go = new GameObject("H");
            try
            {
                var h = go.AddComponent<Health>();
                h.Init(Team.Enemy, 100f);
                int first = h.Life;
                h.DamageScale = 0.5f;
                Assert.IsTrue(h.TryTakeDamage(new DamageInfo { Amount = 40f, AttackerTeam = Team.Player }, out float dealt));
                Assert.AreEqual(20f, dealt, 1e-4f);
                Assert.AreEqual(20f, h.LastBeforeReduction, 1e-4f, "감소 전 값 = 피해 × DamageScale");
                h.Init(Team.Enemy, 100f);
                Assert.AreEqual(first + 1, h.Life, "풀에서 다시 나오면 새 삶");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
