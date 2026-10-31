using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Heroes;
using HistoryHeroes.Player;
using HistoryHeroes.Stats;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HistoryHeroes.Tests
{
    public sealed class CombatGenerationTests
    {
        private const int TestLayer = 29;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _created = new List<Object>();
        private PlayerController _player;
        private HeroData _hero;
        private Transform _shots;
        private Transform _previousFxRoot;
        private UnityEngine.Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            _randomState = UnityEngine.Random.state;
            _previousFxRoot = (Transform)typeof(AttackFx).GetField("_root", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _hero = ScriptableObject.CreateInstance<HeroData>();
            _created.Add(_hero);
            AnubisDefinition.ConfigureHero(_hero);
            _player = MakeObject("GenerationPlayer").AddComponent<PlayerController>();
            Set(_player, "hero", _hero);
            Set(_player, "_sig", HeroSignature.For(_hero, 1));
            Health health = _player.GetComponent<Health>();
            health.Init(Team.Player, 100f);
            Set(_player, "<Health>k__BackingField", health);
            _player.Stats.SetBase(StatType.Attack, 10f);
            _player.Stats.SetBase(StatType.Range, 3f);
            _player.Stats.SetBase(StatType.CritRate, 0f);
            Set(_player, "enemyMask", (LayerMask)(1 << TestLayer));
            Set(_player, "_enemyFilter", new ContactFilter2D { useLayerMask = true, layerMask = 1 << TestLayer, useTriggers = true });
            // EditMode에서는 Awake 대신 실제 이동 이벤트와 같은 초기화 경로만 연결한다.
            _player.Stop.Moved += () => Invoke(_player, "ResetStopSignatures", true);
            Projectile template = MakeObject("GenerationProjectile").AddComponent<Projectile>();
            template.gameObject.SetActive(false);
            _shots = MakeObject("GenerationShots").transform;
            Set(_player, "_projectiles", new ComponentPool<Projectile>(template, _shots));
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            Transform fxRoot = (Transform)typeof(AttackFx).GetField("_root", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (fxRoot != null && fxRoot != _previousFxRoot)
            {
                Object.DestroyImmediate(fxRoot.gameObject);
                typeof(AttackFx).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            }
            UnityEngine.Random.state = _randomState;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AnubisFireMoveLateHit_DoesNotBuildMark(bool stopAgain)
        {
            Health target = MakeTarget();
            Projectile shot = Fire(target);
            Move(stopAgain);
            Invoke(shot, "Step", 0.5f);
            Assert.Less(target.Current, target.Max, "이동해도 이미 날아간 탄의 기본 피해는 유지한다");
            Assert.AreEqual(0, _player.MarkCount, "이동 전 발사한 탄으로 표식을 쌓지 않는다");
        }

        [Test]
        public void AnubisFireMoveRestopLateHit_DoesNotConsumeReadyMark()
        {
            Health target = MakeTarget();
            var mark = (AnubisMarkState)Get(_player, "_mark");
            for (int i = 0; i < 3; i++) mark.RecordHit(target, target.Life, Time.time, true, 3, 6f);
            Projectile shot = Fire(target);
            Move(true);
            Invoke(shot, "Step", 0.5f);
            Assert.AreEqual(3, _player.MarkCount, "이전 탄은 준비된 둔화도 발동·소모하지 않는다");
            Assert.IsTrue(_player.MarkReady);
        }

        [Test]
        public void AnubisStationaryHit_StillBuildsMark()
        {
            Health target = MakeTarget();
            Invoke(Fire(target), "Step", 0.5f);
            Assert.AreEqual(1, _player.MarkCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AnubisLateHit_AfterDeathOrDisableDoesNotBuildMark(bool disable)
        {
            Health target = MakeTarget();
            Projectile shot = Fire(target);
            if (disable) _player.enabled = false;
            else _player.Health.Kill();
            Invoke(shot, "Step", 0.5f);
            Assert.AreEqual(0, _player.MarkCount);
        }

        [Test]
        public void MultiSwing_MoveAndRestopBetweenSwings_CancelsRemainingDamage()
        {
            Health target = MakeTarget();
            IEnumerator swings = BeginSwings(target);
            Assert.IsTrue(swings.MoveNext());
            Assert.AreEqual(990f, target.Current, 0.001f);
            Move(true);
            Assert.IsFalse(swings.MoveNext(), "다시 멈춰도 취소한 연속 베기를 재개하지 않는다");
            Assert.AreEqual(990f, target.Current, 0.001f);
        }

        [Test]
        public void MultiSwing_WithoutMovement_DealsAllThreeSwings()
        {
            Health target = MakeTarget();
            IEnumerator swings = BeginSwings(target);
            while (swings.MoveNext()) { }
            Assert.AreEqual(970f, target.Current, 0.001f);
        }

        private IEnumerator BeginSwings(Health target)
        {
            _hero.attackKind = BasicAttackKind.MeleeArc;
            return (IEnumerator)Invoke(_player, "Swings", target, 3, 1f, 120f, 0, false, false);
        }

        private Projectile Fire(Health target)
        {
            Invoke(_player, "Fire", target);
            Physics2D.SyncTransforms();
            return _shots.GetChild(_shots.childCount - 1).GetComponent<Projectile>();
        }

        private void Move(bool stopAgain)
        {
            _player.Stop.Tick(Vector2.right, 0.01f);
            if (stopAgain) _player.Stop.Tick(Vector2.zero, StopDetector.StopThreshold);
            Assert.AreEqual(stopAgain, _player.IsStopped);
        }

        private Health MakeTarget()
        {
            GameObject go = MakeObject("GenerationTarget");
            go.layer = TestLayer;
            go.transform.position = new Vector2(2f, 0f);
            go.AddComponent<CircleCollider2D>().radius = 0.25f;
            Health health = go.AddComponent<Health>();
            health.Init(Team.Enemy, 1000f);
            Physics2D.SyncTransforms();
            return health;
        }

        private GameObject MakeObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, PrivateInstance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
    }
}
