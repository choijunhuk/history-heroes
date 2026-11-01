using HistoryHeroes.Enemy;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    /// <summary>확장 1차 4장 엘리트 속성: 구간·층별 개수, 서로 다른 속성, 이름</summary>
    public sealed class EliteAffixTests
    {
        [Test]
        public void Count_FollowsChapterAndRiftRules()
        {
            var random = new System.Random(3);
            Assert.AreEqual(0, EliteAffixes.CountFor(1, 0, random), "1구간은 속성 없음");
            Assert.AreEqual(1, EliteAffixes.CountFor(2, 0, random));
            Assert.AreEqual(1, EliteAffixes.CountFor(3, 0, random));
            int two = 0;
            for (int i = 0; i < 400; i++)
            {
                int n = EliteAffixes.CountFor(5, 0, random);
                Assert.That(n, Is.InRange(1, 2), "4~5구간은 1~2개");
                if (n == 2) two++;
            }

            Assert.That(two / 400f, Is.InRange(0.3f, 0.5f), "2개는 약 40%");
            Assert.AreEqual(0, EliteAffixes.CountFor(1, 4, random), "균열 4층 = 0");
            Assert.AreEqual(1, EliteAffixes.CountFor(1, 5, random), "균열 5층부터 1");
            Assert.AreEqual(2, EliteAffixes.CountFor(1, 40, random), "균열은 최대 2");
        }

        [Test]
        public void Roll_GivesDistinctAffixes_AndNames()
        {
            var random = new System.Random(11);
            for (int i = 0; i < 200; i++)
            {
                EliteAffix a = EliteAffixes.Roll(random, 2);
                int bits = 0;
                foreach (EliteAffix one in EliteAffixes.All)
                {
                    if ((a & one) != 0) bits++;
                }

                Assert.AreEqual(2, bits, "서로 다른 속성 2개");
            }

            Assert.AreEqual(EliteAffix.None, EliteAffixes.Roll(random, 0));
            Assert.AreEqual("분노한 · 보호막", EliteAffixes.Names(EliteAffix.Enraged | EliteAffix.Shielded));
            foreach (EliteAffix one in EliteAffixes.All) Assert.IsNotEmpty(EliteAffixes.NameOf(one));
        }
    }
}
