using System.Linq;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Modes;
using HistoryHeroes.Skills;
using HistoryHeroes.Story;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class GodTrialBuilderTests
    {
        [Test]
        public void ExistingHeroOrderStaysAndSusanooAthenaFollowAnubisWithoutPlayableZeus()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Khan", "Guan", "Napoleon", "Tesla", "YiSunsin", "YiSeonggye", "Davinci", "Zhuge",
                "Jeanne", "Seondeok", "Boudica", "Thor", "Cleopatra", "Anubis", "Susanoo", "Athena", "Tomoe", "Wuzetian", "Curie", "Buddha", "Adam"
            }, Art3DCharacters.Heroes.Select(look => look.name).ToArray());
            Assert.AreEqual(8, Art3DCharacters.Heroes.Count(look => look.female));
            Assert.IsFalse(Art3DCharacters.Heroes.Any(look => look.name == "Zeus"));
        }

        [Test]
        public void EveryTrialHasAtMostThreeIntroLinesAndMatchingTints()
        {
            foreach (GodTrialDefinition trial in GodTrialRules.Trials)
            {
                string[] lines = GodTrialIntro.For(trial.Id);
                Assert.That(lines.Length, Is.InRange(1, 3), trial.Id);
                Assert.AreEqual(lines.Length, GodTrialIntro.TintsFor(trial.Id).Length);
                Assert.IsTrue(lines.All(line => !string.IsNullOrWhiteSpace(line)));
                StringAssert.Contains(trial.DisplayName, string.Join(" ", lines));
            }
            Assert.IsEmpty(GodTrialIntro.For("unknown_trial"));
        }

        [Test]
        public void SandCardShowsBothSlowAndRangeGrowth()
        {
            ActiveSkillData statue = ScriptableObject.CreateInstance<ActiveSkillData>();
            try
            {
                AnubisDefinition.ConfigureStatue(statue);
                string line = SkillCardView.UpgradeLine(statue, 1, 2);
                StringAssert.Contains("둔화 20% → 22.5%", line);
                StringAssert.Contains("범위", line);
                StringAssert.DoesNotContain("피해", line);
            }
            finally { Object.DestroyImmediate(statue); }
        }

        [Test]
        public void AnubisMaskReplacesHumanFace()
        {
            Art3DCharacters.Look look = Art3DCharacters.Heroes.Single(h => h.name == "Anubis");
            Assert.AreEqual("Male_Ranger", look.body);
            Assert.IsFalse(look.head);
            CollectionAssert.Contains(look.hide, "Male_Ranger_Head_Hood");
            CollectionAssert.Contains(look.props, "Anubis_Mask");
            Assert.IsEmpty(look.hair);
            CollectionAssert.DoesNotContain(look.props, "Enemy_Staff");
        }

        [Test]
        public void ZeusUsesWhiteHairAndExistingHumanBody()
        {
            Art3DCharacters.Look look = Art3DCharacters.Enemies.Single(e => e.ids.Contains("zeus_trial")).look;
            Assert.AreEqual("Male_Peasant", look.body);
            CollectionAssert.Contains(look.hair, "Hair_Long");
            CollectionAssert.Contains(look.hair, "Hair_Beard");
            Assert.That(look.hairColor.r, Is.GreaterThan(0.85f));
            Assert.That(look.metalColor.r, Is.GreaterThan(look.metalColor.b));
        }

        [Test]
        public void AnubisSummonsHaveTheirOwnMemoryLook()
        {
            Art3DCharacters.Look minion = Art3DCharacters.Enemies.Single(e => e.ids.Contains("minion")).look;
            Art3DCharacters.Look memory = Art3DCharacters.Enemies.Single(e => e.ids.Contains("memory_guardian")).look;
            Assert.AreEqual(minion.body, memory.body);
            Assert.AreNotEqual(minion.name, memory.name);
            Assert.That(memory.tint.r, Is.GreaterThan(minion.tint.r));
        }
    }
}
