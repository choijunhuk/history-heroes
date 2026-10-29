using System.Linq;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Missions;
using HistoryHeroes.Modes;
using HistoryHeroes.Story;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class GodFinalPresentationTests
    {
        [Test]
        public void NewGodLooksReuseTheRegisteredPropsAndCorrectBodies()
        {
            Art3DCharacters.Look susanoo = Art3DCharacters.Heroes.Single(look => look.name == "Susanoo");
            Assert.AreEqual("Male_Ranger", susanoo.body);
            CollectionAssert.Contains(susanoo.hide, "Male_Ranger_Head_Hood");
            CollectionAssert.Contains(susanoo.hair, "Hair_Long");
            CollectionAssert.Contains(susanoo.props, "Susanoo_Sword");
            Assert.AreEqual("Sword_Regular_A", susanoo.attack);
            Assert.AreEqual("hand_r", Art3DProps.Bones["Susanoo_Sword"]);

            Art3DCharacters.Look athena = Art3DCharacters.Heroes.Single(look => look.name == "Athena");
            Assert.IsTrue(athena.female);
            Assert.AreEqual("Female_Ranger", athena.body);
            CollectionAssert.Contains(athena.hide, "Female_Ranger_Head_Hood");
            CollectionAssert.Contains(athena.hair, "Hair_Buns");
            CollectionAssert.Contains(athena.props, "Athena_Spear");
            CollectionAssert.Contains(athena.props, "Athena_Helmet");
            CollectionAssert.DoesNotContain(athena.props, "Enemy_TowerShield");
            Assert.AreEqual("hand_r", Art3DProps.Bones["Athena_Spear"]);
            Assert.AreEqual("Head", Art3DProps.Bones["Athena_Helmet"]);
            Assert.That(athena.metalColor.r, Is.GreaterThan(athena.metalColor.b));
            Assert.AreEqual(5, Art3DCharacters.Heroes.Count(look => look.female));
        }

        [Test]
        public void TrialIntroductionsAndSevenVoiceMomentsArePresent()
        {
            foreach (string trialId in new[] { GodTrialRules.SusanooTrialId, GodTrialRules.AthenaTrialId })
            {
                string[] lines = GodTrialIntro.For(trialId);
                Assert.That(lines.Length, Is.InRange(1, 3));
                Assert.AreEqual(lines.Length, GodTrialIntro.TintsFor(trialId).Length);
                Assert.IsTrue(lines.All(line => !string.IsNullOrWhiteSpace(line)));
            }

            foreach (string heroId in new[] { "susanoo", "athena" })
                foreach (LineMoment moment in System.Enum.GetValues(typeof(LineMoment)))
                    Assert.IsNotEmpty(StoryLines.For(heroId, moment), $"{heroId}: {moment}");
        }

        [Test]
        public void MissionOrderKeepsThePreviousHeroesAndAddsTwo()
        {
            var table = ScriptableObject.CreateInstance<MissionTable>();
            try
            {
                CollectionAssert.AreEqual(new[]
                {
                    "khan", "guan", "napoleon", "tesla", "yisunsin", "yiseonggye", "davinci", "zhuge",
                    "jeanne", "seondeok", "boudica", "thor", "cleopatra", "anubis", "susanoo", "athena",
                }, table.heroOrder);
                Assert.AreEqual(16, table.heroOrder.Distinct().Count());
            }
            finally { Object.DestroyImmediate(table); }
        }
    }
}
