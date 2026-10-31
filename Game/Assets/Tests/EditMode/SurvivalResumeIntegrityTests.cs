using System;
using System.Reflection;
using HistoryHeroes.Common;
using HistoryHeroes.Records;
using HistoryHeroes.Survival;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class SurvivalResumeIntegrityTests
    {
        private static void Call(string method, RunSnapshot snapshot)
        {
            MethodInfo target = typeof(SurvivalRun).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(target, method + " 연결이 필요해요");
            target.Invoke(null, new object[] { snapshot });
        }

        [Test]
        public void SurvivalSnapshot_RestoresNextCardsAfterRestart()
        {
            RunRandom.Begin(74821);
            for (int i = 0; i < 7; i++) RunRandom.CardRange(0, 1000);
            var snapshot = new RunSnapshot { valid = true, mode = (int)BattleMode.Survival };
            Call("CaptureRandomState", snapshot);
            int expected = RunRandom.CardRange(0, 1000000);
            RunSnapshot loaded = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(snapshot));

            RunRandom.Begin(222);
            for (int i = 0; i < 5; i++) RunRandom.CardRange(0, 1000);
            Call("RestoreRandomState", loaded);

            Assert.AreEqual(74821, loaded.seed);
            Assert.AreEqual(4, loaded.rng.Count);
            Assert.AreEqual(74821, RunRandom.Seed);
            Assert.AreEqual(expected, RunRandom.CardRange(0, 1000000), "강제 종료 뒤에도 다음 카드는 같다");
        }
    }
}
