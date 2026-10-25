using HistoryHeroes.Stats;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    /// <summary>Docs/Design/Stats.md 2장(피해·능력치 계산)과 1장(상한) 규칙 확인.</summary>
    public class StatSheetTests
    {
        private static readonly object SkillA = new object();
        private static readonly object SkillB = new object();

        private static StatSheet NewSheet()
        {
            var sheet = new StatSheet();
            sheet.SetBase(StatType.Attack, 100f);
            sheet.SetBase(StatType.AttackSpeed, 1.0f);
            sheet.SetBase(StatType.DamageReduction, 0f);
            return sheet;
        }

        [Test]
        public void NoModifiers_ReturnsBase()
        {
            Assert.AreEqual(100f, NewSheet().Get(StatType.Attack), 0.0001f);
        }

        [Test]
        public void FlatThenPercent_PercentsAreSummedOnce()
        {
            var sheet = NewSheet();
            sheet.Add(new StatModifier(StatType.Attack, ModifierKind.Flat, 20f, ModifierSource.Equipment, SkillA));
            sheet.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, 0.15f, ModifierSource.Skill, SkillA));
            sheet.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, 0.10f, ModifierSource.Skill, SkillB));

            // (100 + 20) × (1 + 0.15 + 0.10) = 150
            Assert.AreEqual(150f, sheet.Get(StatType.Attack), 0.0001f);
        }

        [Test]
        public void OrderOfModifiers_DoesNotChangeResult()
        {
            var a = NewSheet();
            a.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, 0.3f, ModifierSource.Skill, SkillA));
            a.Add(new StatModifier(StatType.Attack, ModifierKind.Flat, 50f, ModifierSource.Equipment, SkillB));

            var b = NewSheet();
            b.Add(new StatModifier(StatType.Attack, ModifierKind.Flat, 50f, ModifierSource.Equipment, SkillB));
            b.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, 0.3f, ModifierSource.Skill, SkillA));

            Assert.AreEqual(a.Get(StatType.Attack), b.Get(StatType.Attack), 0.0001f);
        }

        [Test]
        public void BasicAttackOnlyModifier_IsIgnoredForActives()
        {
            var sheet = NewSheet();
            // 공격속도 패시브는 기본 공격에만 (Skills.md 1장)
            sheet.Add(new StatModifier(StatType.AttackSpeed, ModifierKind.Percent, 0.12f,
                ModifierSource.Skill, SkillA, StatTarget.BasicAttack));

            Assert.AreEqual(1.12f, sheet.Get(StatType.AttackSpeed, StatTarget.BasicAttack), 0.0001f);
            Assert.AreEqual(1.00f, sheet.Get(StatType.AttackSpeed, StatTarget.Active), 0.0001f);
        }

        [Test]
        public void DamageReduction_IsCappedAt60Percent()
        {
            var sheet = NewSheet();
            for (int i = 0; i < 20; i++)
            {
                sheet.Add(new StatModifier(StatType.DamageReduction, ModifierKind.Flat, 0.06f, ModifierSource.Skill, SkillA));
            }

            Assert.AreEqual(0.6f, sheet.Get(StatType.DamageReduction), 0.0001f);
        }

        [Test]
        public void AttackSpeed_IsCappedAt3()
        {
            var sheet = NewSheet();
            sheet.Add(new StatModifier(StatType.AttackSpeed, ModifierKind.Percent, 5f, ModifierSource.Skill, SkillA));

            Assert.AreEqual(3.0f, sheet.Get(StatType.AttackSpeed), 0.0001f);
        }

        [Test]
        public void RemoveAllFrom_RemovesOnlyThatOwner()
        {
            var sheet = NewSheet();
            sheet.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, 0.2f, ModifierSource.Skill, SkillA));
            sheet.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, 0.1f, ModifierSource.Skill, SkillB));

            int removed = sheet.RemoveAllFrom(SkillA);

            Assert.AreEqual(1, removed);
            Assert.AreEqual(110f, sheet.Get(StatType.Attack), 0.0001f);
        }

        [Test]
        public void RemoveAllBySource_ClearsRunSkillsOnly()
        {
            var sheet = NewSheet();
            sheet.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, 0.2f, ModifierSource.Skill, SkillA));
            sheet.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, 0.1f, ModifierSource.Equipment, SkillB));

            sheet.RemoveAllBySource(ModifierSource.Skill);

            Assert.AreEqual(110f, sheet.Get(StatType.Attack), 0.0001f);
        }

        [Test]
        public void Changed_FiresOnAddAndRemove()
        {
            var sheet = NewSheet();
            int count = 0;
            sheet.Changed += () => count++;

            sheet.Add(new StatModifier(StatType.Attack, ModifierKind.Flat, 1f, ModifierSource.Skill, SkillA));
            sheet.RemoveAllFrom(SkillA);
            sheet.RemoveAllFrom(SkillA); // 지울 게 없으면 알리지 않는다

            Assert.AreEqual(2, count);
        }
    }
}
