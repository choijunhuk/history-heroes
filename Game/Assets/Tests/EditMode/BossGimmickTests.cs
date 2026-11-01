using HistoryHeroes.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>확장 1차 5장 기믹 보스: 보스마다 맞는 기믹만 (검토 4번 표), 균열은 층 씨앗으로 같은 기믹, 싸움터 귀퉁이</summary>
    public sealed class BossGimmickTests
    {
        private static T Make<T>() where T : EnemyBase
        {
            var go = new GameObject(typeof(T).Name);
            go.SetActive(false); // Awake 없이 종류만 본다
            return go.AddComponent<T>();
        }

        [Test]
        public void EachBossGetsItsOwnGimmick()
        {
            var king = Make<OblivionKingBoss>();
            var twins = Make<ShadowTwinsBoss>();
            var hunter = Make<ShadowHunterBoss>();
            var executor = Make<OblivionExecutorBoss>();
            var colossus = Make<SteelColossusBoss>();
            var warden = Make<RiftWardenBoss>();
            try
            {
                Assert.AreEqual(GimmickKind.PillarShield, BossGimmicks.For(king));
                Assert.AreEqual(GimmickKind.ShadowMarks, BossGimmicks.For(twins));
                Assert.AreEqual(GimmickKind.MirrorStun, BossGimmicks.For(hunter));
                Assert.AreEqual(GimmickKind.HalfFire, BossGimmicks.For(executor));
                Assert.AreEqual(GimmickKind.None, BossGimmicks.For(colossus), "보스맵 강철 거상은 기믹 없음 (균열에서만 불길)");

                CollectionAssert.AreEqual(new[] { GimmickKind.PillarShield }, BossGimmicks.RiftOptions(warden));
                CollectionAssert.AreEqual(new[] { GimmickKind.HalfFire }, BossGimmicks.RiftOptions(colossus));
                CollectionAssert.AreEqual(new[] { GimmickKind.ShadowMarks }, BossGimmicks.RiftOptions(twins));
                CollectionAssert.AreEquivalent(new[] { GimmickKind.PillarShield, GimmickKind.CrumblingFloor }, BossGimmicks.RiftOptions(king));
                Assert.IsEmpty(BossGimmicks.RiftOptions(hunter), "중간 보스는 균열 보스 층에 안 나온다");

                foreach (GimmickKind k in new[] { GimmickKind.PillarShield, GimmickKind.CrumblingFloor, GimmickKind.ShadowMarks, GimmickKind.MirrorStun, GimmickKind.HalfFire })
                    Assert.IsNotEmpty(BossGimmicks.HintFor(k), $"{k}: 처음 나올 때 알리는 한 줄");
            }
            finally
            {
                foreach (EnemyBase e in new EnemyBase[] { king, twins, hunter, executor, colossus, warden }) Object.DestroyImmediate(e.gameObject);
            }
        }

        [Test]
        public void RiftPick_SameSeedAndFloor_SameGimmick()
        {
            var king = Make<OblivionKingBoss>();
            try
            {
                bool sawPillar = false, sawFloor = false;
                for (int floor = 10; floor <= 400; floor += 10)
                {
                    GimmickKind a = BossGimmicks.PickRift(king, 1234, floor);
                    Assert.AreEqual(a, BossGimmicks.PickRift(king, 1234, floor), "같은 씨앗·층 = 같은 기믹");
                    sawPillar |= a == GimmickKind.PillarShield;
                    sawFloor |= a == GimmickKind.CrumblingFloor;
                }

                Assert.IsTrue(sawPillar && sawFloor, "망각의 왕은 두 기믹이 섞여 나온다");
            }
            finally
            {
                Object.DestroyImmediate(king.gameObject);
            }
        }

        [Test]
        public void ArenaCorners_StayInside()
        {
            BossArena round = BossArena.Circle(Vector2.zero, 3.9f);
            BossArena room = BossArena.Room(new Vector2(0f, 0.2f), new Vector2(3.6f, 6.3f));
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(round.Contains(round.Corner(i)), "보스맵 원 안");
                Assert.IsTrue(room.Contains(room.Corner(i)), "던전 방 안");
            }

            Assert.AreEqual(0.75f, PillarShieldGimmick.MaxShield, 1e-5f, "보호막 -75%");
            Assert.AreEqual(0.25f, PillarShieldGimmick.PerPillar, 1e-5f, "기둥 하나에 25%씩");
        }
    }
}
