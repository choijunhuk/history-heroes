using System.Collections.Generic;
using System.Reflection;
using HistoryHeroes.Combat;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class ProjectileWaveTests
    {
        private const int TestLayer = 29;
        private static readonly MethodInfo StepMethod = typeof(Projectile).GetMethod("Step", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject value in _objects) if (value != null) Object.DestroyImmediate(value);
            _objects.Clear();
        }

        private Health Enemy(string name, float x)
        {
            var go = new GameObject(name) { layer = TestLayer };
            _objects.Add(go);
            go.transform.position = new Vector3(x, 0f, 0f);
            go.AddComponent<CircleCollider2D>().radius = 0.25f;
            Health health = go.AddComponent<Health>();
            health.Init(Team.Enemy, 100f);
            return health;
        }

        private Projectile Shoot(bool oncePerTarget, int pierce)
        {
            var go = new GameObject("TestWave");
            _objects.Add(go);
            Projectile projectile = go.AddComponent<Projectile>();
            var damage = new DamageInfo { Amount = 10f, Kind = DamageKind.Active, AttackerTeam = Team.Player };
            projectile.Launch(Vector2.right, 10f, damage, 1 << TestLayer, 0, pierce, 0,
                shot => shot.gameObject.SetActive(false), maxDistance: 3f, hitOncePerTarget: oncePerTarget);
            Physics2D.SyncTransforms();
            return projectile;
        }

        private static void Step(Projectile projectile)
        {
            Assert.IsNotNull(StepMethod);
            StepMethod.Invoke(projectile, new object[] { 0.15f });
        }

        [Test]
        public void PiercingWaveDamagesEachOverlappingColliderOnlyOnce()
        {
            Health first = Enemy("First", 1f);
            Health second = Enemy("Second", 1.25f);
            Projectile wave = Shoot(true, int.MaxValue);
            for (int i = 0; i < 8 && wave.gameObject.activeSelf; i++) Step(wave);
            Assert.AreEqual(90f, first.Current, 0.0001f);
            Assert.AreEqual(90f, second.Current, 0.0001f);
            Assert.IsFalse(wave.gameObject.activeSelf, "파동은 정한 직선 길이에서 끝난다");
        }

        [Test]
        public void OrdinaryNonPiercingProjectileStillStopsAtFirstTarget()
        {
            Health first = Enemy("First", 1f);
            Health second = Enemy("Second", 1.25f);
            Projectile shot = Shoot(false, 0);
            Step(shot);
            Assert.AreEqual(90f, first.Current, 0.0001f);
            Assert.AreEqual(100f, second.Current, 0.0001f);
            Assert.IsFalse(shot.gameObject.activeSelf);
        }
    }
}
