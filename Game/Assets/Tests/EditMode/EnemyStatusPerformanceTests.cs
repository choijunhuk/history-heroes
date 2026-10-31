using System.Collections;
using System.Reflection;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class EnemyStatusPerformanceTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _enemyObject;
        private EnemyData _enemyData;

        [SetUp]
        public void SetUp()
        {
            GamePause.ResetAll();
            Assert.IsFalse(GamePause.IsPaused);
        }

        [TearDown]
        public void TearDown()
        {
            if (_enemyObject != null) Object.DestroyImmediate(_enemyObject);
            if (_enemyData != null) Object.DestroyImmediate(_enemyData);
            GamePause.ResetAll();
        }

        private EnemyStatus CreateStatus(bool withFlash = false)
        {
            _enemyObject = new GameObject("EnemyStatusTest");
            _enemyObject.AddComponent<SpriteRenderer>();
            Health health = _enemyObject.AddComponent<Health>();
            health.Init(Team.Enemy, 100f);
            _enemyObject.AddComponent<Rigidbody2D>();
            if (withFlash) _enemyObject.AddComponent<HitFlash>();
            EnemyStatus status = _enemyObject.AddComponent<EnemyStatus>();
            BomberEnemy enemy = _enemyObject.AddComponent<BomberEnemy>();
            if (enemy.Health == null) typeof(EnemyBase).GetMethod("Awake", PrivateInstance).Invoke(enemy, null);
            typeof(EnemyStatus).GetMethod("Awake", PrivateInstance).Invoke(status, null);
            _enemyData = ScriptableObject.CreateInstance<EnemyData>();
            typeof(EnemyBase).GetField("data", PrivateInstance).SetValue(enemy, _enemyData);
            return status;
        }

        private static T Field<T>(EnemyStatus status, string name)
        {
            FieldInfo field = typeof(EnemyStatus).GetField(name, PrivateInstance);
            Assert.IsNotNull(field, name);
            return (T)field.GetValue(status);
        }

        [Test]
        public void ExpiredPoisonSourcesAreRemovedWithoutSkippingAdjacentEntries()
        {
            EnemyStatus status = CreateStatus();
            object surviving = new object();
            status.TouchPoison(new object(), 2f, -1f);
            status.TouchPoison(new object(), 3f, -1f);
            status.TouchPoison(surviving, 4f, 10f);
            status.TouchPoison(new object(), 5f, -1f);

            typeof(EnemyStatus).GetMethod("Update", PrivateInstance).Invoke(status, null);

            Assert.AreEqual(1, Field<IList>(status, "_poison").Count);
            Assert.AreEqual(1, status.PoisonLayers);
            status.TouchPoison(surviving, 6f, 10f);
            Assert.AreEqual(1, Field<IList>(status, "_poison").Count, "남은 출처는 새 겹으로 중복 등록하지 않는다");
        }

        [Test]
        public void DeathThenImmediateReuseClearsQueuedPushAndPoisonTick()
        {
            EnemyStatus status = CreateStatus();
            BomberEnemy enemy = _enemyObject.GetComponent<BomberEnemy>();
            enemy.Spawn(_enemyObject.transform, 1f, 1f, false);
            status.Pull(Vector2.right, 2f, 10f);
            status.Push(Vector2.up);
            status.TouchPoison(new object(), 4f, 10f);
            typeof(EnemyStatus).GetField("_poisonTimer", PrivateInstance).SetValue(status, 0.3f);
            Assert.AreEqual(Vector2.up, Field<Vector2>(status, "_push"));

            typeof(EnemyBase).GetField("_lastHitKind", PrivateInstance).SetValue(enemy, DamageKind.Enemy);
            enemy.Health.Kill();
            Assert.IsFalse(_enemyObject.activeSelf, "사망한 적은 풀에서 잠시 비활성화된다");
            _enemyObject.SetActive(true);
            enemy.Spawn(_enemyObject.transform, 1f, 1f, false);

            Assert.IsFalse(enemy.Health.IsDead);
            Assert.AreEqual(Vector2.zero, Field<Vector2>(status, "_push"));
            Assert.AreEqual(0f, Field<float>(status, "_poisonTimer"));
            Assert.AreEqual(0, status.PoisonLayers);
            Assert.IsFalse(status.IsPulled);
        }

        [Test]
        public void HitFlashIsResolvedOnceWhenStatusAwakes()
        {
            EnemyStatus status = CreateStatus(withFlash: true);
            Assert.AreSame(_enemyObject.GetComponent<HitFlash>(), Field<HitFlash>(status, "_hitFlash"));
        }
    }
}
