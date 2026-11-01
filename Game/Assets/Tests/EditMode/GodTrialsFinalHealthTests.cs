using System;
using System.IO;
using System.Reflection;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Missions;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HistoryHeroes.Tests
{
    public sealed class GodTrialsFinalHealthTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly PropertyInfo PracticeProperty = typeof(GameSession).GetProperty(nameof(GameSession.Practice));

        [Test]
        public void SecondFormHitShowsDamageOnSecondWithoutDoubleCounting()
        {
            GameObject bossObject = null;
            GameObject formObject = null;
            Action<Health, DamageInfo, float> onAnyDamaged = null;
            Action<Health, DamageInfo, float> onAnyVisualDamaged = null;
            float previousTimeScale = Time.timeScale;
            float previousDevSpeed = GamePause.DevSpeed;
            bool previousPractice = GameSession.Practice;
            Func<Health, DamageInfo, DamageInfo> previousEnemyIncoming = Health.EnemyIncoming;
            try
            {
                Health.EnemyIncoming = null;
                SetPractice(true);
                BuildTrial(out bossObject, out formObject, out ShadowTwinsBoss boss, out ShadowTrialForm form,
                    out Health bossHealth, out Health formHealth, out HitFlash bossFlash, out HitFlash formFlash);
                SetField(boss, "_openSecond", true);
                Func<DamageInfo, DamageInfo> mainModifier = hit => { hit.Amount *= 2f; return hit; };
                bossHealth.IncomingModifier = mainModifier;
                int mainDamageEvents = 0;
                int globalDamageEvents = 0;
                int visualDamageEvents = 0;
                Health stateTarget = null;
                Health visibleTarget = null;
                bossHealth.Damaged += (_, __) => mainDamageEvents++;
                onAnyDamaged = (target, _, __) => { globalDamageEvents++; stateTarget = target; };
                onAnyVisualDamaged = (target, _, __) => { visualDamageEvents++; visibleTarget = target; };
                Health.AnyDamaged += onAnyDamaged;
                Health.AnyVisualDamaged += onAnyVisualDamaged;
                DamageLog.Reset();

                Assert.IsTrue(formHealth.TryTakeDamage(BasicHit(30f), out float dealt));

                Assert.AreEqual(30f, dealt);
                Assert.AreEqual(170f, bossHealth.Current);
                Assert.AreEqual(100f, formHealth.Current, "형상은 표시만 맡고 체력은 본체가 한 번 깎는다");
                Assert.AreEqual(30f, DamageLog.BasicAttack, 0.001f);
                Assert.AreEqual(1, mainDamageEvents, "본체 단계·시련 상태 알림은 유지한다");
                Assert.AreEqual(1, globalDamageEvents);
                Assert.AreEqual(1, visualDamageEvents);
                Assert.AreSame(bossHealth, stateTarget, "처치·임무 구독자는 공유 체력 몸체를 계속 받는다");
                Assert.AreSame(formHealth, visibleTarget);
                Assert.AreSame(mainModifier, bossHealth.IncomingModifier, "본체 표식은 경로 우회 뒤 복구한다");
                Assert.IsNull(bossHealth.DamageDisplayTarget, "피해 표시 경로는 한 번의 타격 뒤 복구한다");
                Assert.IsFalse(bossFlash.IsFlashing);
                Assert.IsTrue(formFlash.IsFlashing);
            }
            finally
            {
                if (onAnyDamaged != null) Health.AnyDamaged -= onAnyDamaged;
                if (onAnyVisualDamaged != null) Health.AnyVisualDamaged -= onAnyVisualDamaged;
                if (formObject != null) Object.DestroyImmediate(formObject);
                if (bossObject != null) Object.DestroyImmediate(bossObject);
                DamageLog.Reset();
                GamePause.ResetAll();
                GamePause.DevSpeed = previousDevSpeed;
                Time.timeScale = previousTimeScale;
                SetPractice(previousPractice);
                Health.EnemyIncoming = previousEnemyIncoming;
            }
        }

        [Test]
        public void ClosedSecondFormRejectsHitAndFinalHitReportsOnSecond()
        {
            GameObject bossObject = null;
            GameObject formObject = null;
            Action<Health, DamageInfo, float> onAnyDamaged = null;
            Action<Health, DamageInfo, float> onAnyVisualDamaged = null;
            float previousTimeScale = Time.timeScale;
            float previousDevSpeed = GamePause.DevSpeed;
            bool previousPractice = GameSession.Practice;
            const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo storeField = typeof(SaveSystem).GetField("_store", PrivateStatic);
            FieldInfo dataField = typeof(SaveSystem).GetField("_data", PrivateStatic);
            FieldInfo dirtyField = typeof(SaveSystem).GetField("_dirty", PrivateStatic);
            object previousStore = storeField.GetValue(null);
            object previousData = dataField.GetValue(null);
            object previousDirty = dirtyField.GetValue(null);
            MissionTable previousTable = MissionEvents.Table;
            float previousTrauma = CameraShake.Trauma;
            Func<Health, DamageInfo, DamageInfo> previousEnemyIncoming = Health.EnemyIncoming;
            try
            {
                Health.EnemyIncoming = null;
                SaveSystem.Use(new SaveStore(Path.Combine(Path.GetTempPath(), "hh_godfix_twin_" + Guid.NewGuid().ToString("N"))));
                MissionEvents.Table = null;
                SetPractice(false);
                BuildTrial(out bossObject, out formObject, out ShadowTwinsBoss boss, out ShadowTrialForm form,
                    out Health bossHealth, out Health formHealth, out HitFlash bossFlash, out HitFlash formFlash);
                int globalDamageEvents = 0;
                int visualDamageEvents = 0;
                Health stateTarget = null;
                Health visibleTarget = null;
                onAnyDamaged = (target, _, __) => { globalDamageEvents++; stateTarget = target; };
                onAnyVisualDamaged = (target, _, __) => { visualDamageEvents++; visibleTarget = target; };
                Health.AnyDamaged += onAnyDamaged;
                Health.AnyVisualDamaged += onAnyVisualDamaged;
                DamageLog.Reset();

                Assert.IsFalse(formHealth.TryTakeDamage(BasicHit(10f), out float blocked));
                Assert.AreEqual(0f, blocked);
                Assert.AreEqual(0, globalDamageEvents);
                Assert.AreEqual(0, visualDamageEvents);
                Assert.AreEqual(0f, DamageLog.Total);

                Assert.IsTrue(bossHealth.TryTakeDamage(BasicHit(100f), out _));
                Assert.IsTrue(bossFlash.IsFlashing);
                Assert.IsFalse(formFlash.IsFlashing);
                Assert.IsTrue(formHealth.TryTakeDamage(BasicHit(100f), out float finalDealt));
                Assert.AreEqual(100f, finalDealt);
                Assert.IsTrue(bossHealth.IsDead);
                Assert.AreEqual(200f, DamageLog.BasicAttack, 0.001f);
                Assert.AreEqual(1L, SaveSystem.Data.missions.counters.basicKills, "두 형상을 쓰러뜨려도 보스 처치 임무는 한 번만 센다");
                Assert.AreEqual(2, globalDamageEvents, "각 형상의 한 번씩만 알린다");
                Assert.AreEqual(2, visualDamageEvents);
                Assert.AreSame(bossHealth, stateTarget);
                Assert.AreSame(formHealth, visibleTarget, "마지막 타격도 실제 맞은 몸체에서 표시한다");
                Assert.IsNull(bossHealth.DamageDisplayTarget, "마지막 타격도 표시 대상이 남지 않는다");
            }
            finally
            {
                if (onAnyDamaged != null) Health.AnyDamaged -= onAnyDamaged;
                if (onAnyVisualDamaged != null) Health.AnyVisualDamaged -= onAnyVisualDamaged;
                if (formObject != null) Object.DestroyImmediate(formObject);
                if (bossObject != null) Object.DestroyImmediate(bossObject);
                DamageLog.Reset();
                GamePause.ResetAll();
                GamePause.DevSpeed = previousDevSpeed;
                Time.timeScale = previousTimeScale;
                SetPractice(previousPractice);
                MissionEvents.Table = previousTable;
                // Use는 메모리 데이터와 저장 대기 여부도 지우므로 원래 상태를 그대로 돌려놓는다.
                storeField.SetValue(null, previousStore);
                dataField.SetValue(null, previousData);
                dirtyField.SetValue(null, previousDirty);
                RestoreCameraShake(previousTrauma);
                Health.EnemyIncoming = previousEnemyIncoming;
            }
        }

        [Test]
        public void ReinitializedHealthForgetsItsDamageDisplayTarget()
        {
            var mainObject = new GameObject("PooledMainHealth");
            var formObject = new GameObject("PooledFormHealth");
            Action<Health, DamageInfo, float> onAnyVisualDamaged = null;
            Func<Health, DamageInfo, DamageInfo> previousEnemyIncoming = Health.EnemyIncoming;
            try
            {
                Health.EnemyIncoming = null;
                Health main = mainObject.AddComponent<Health>();
                Health form = formObject.AddComponent<Health>();
                main.Init(Team.Enemy, 100f);
                form.Init(Team.Enemy, 100f);
                main.DamageDisplayTarget = form;
                main.Init(Team.Enemy, 100f);
                Health visibleTarget = null;
                onAnyVisualDamaged = (target, _, __) => visibleTarget = target;
                Health.AnyVisualDamaged += onAnyVisualDamaged;

                Assert.IsTrue(main.TryTakeDamage(BasicHit(10f)));
                Assert.AreSame(main, visibleTarget);
            }
            finally
            {
                if (onAnyVisualDamaged != null) Health.AnyVisualDamaged -= onAnyVisualDamaged;
                Object.DestroyImmediate(formObject);
                Object.DestroyImmediate(mainObject);
                DamageLog.Reset();
                Health.EnemyIncoming = previousEnemyIncoming;
            }
        }

        private static DamageInfo BasicHit(float amount) => new DamageInfo
        {
            Amount = amount, AttackerTeam = Team.Player, Kind = DamageKind.BasicAttack
        };

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private static void SetPractice(bool value)
        {
            Assert.IsNotNull(PracticeProperty);
            MethodInfo setter = PracticeProperty.GetSetMethod(true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { value });
        }

        private static void RestoreCameraShake(float trauma)
        {
            FieldInfo instanceField = typeof(CameraShake).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(instanceField);
            CameraShake shake = (CameraShake)instanceField.GetValue(null);
            if (shake == null) return;
            FieldInfo traumaField = typeof(CameraShake).GetField("_trauma", PrivateInstance);
            Assert.IsNotNull(traumaField);
            traumaField.SetValue(shake, trauma);
        }

        private static void BuildTrial(out GameObject bossObject, out GameObject formObject,
            out ShadowTwinsBoss boss, out ShadowTrialForm form, out Health bossHealth, out Health formHealth,
            out HitFlash bossFlash, out HitFlash formFlash)
        {
            bossObject = new GameObject("TrialBoss");
            bossObject.AddComponent<SpriteRenderer>();
            bossObject.AddComponent<Rigidbody2D>();
            bossObject.AddComponent<CircleCollider2D>();
            bossHealth = bossObject.AddComponent<Health>();
            bossHealth.Init(Team.Enemy, 200f);
            bossFlash = bossObject.AddComponent<HitFlash>();
            boss = bossObject.AddComponent<ShadowTwinsBoss>();
            EnsureEnemyAwake(boss, typeof(ShadowTwinsBoss));
            EnsureFlashSubscribed(bossFlash);

            formObject = new GameObject("SecondForm");
            formObject.transform.position = new Vector3(2.5f, 0f, 0f);
            formObject.AddComponent<SpriteRenderer>();
            formObject.AddComponent<Rigidbody2D>();
            formObject.AddComponent<CircleCollider2D>();
            formHealth = formObject.AddComponent<Health>();
            formHealth.Init(Team.Enemy, 100f);
            formFlash = formObject.AddComponent<HitFlash>();
            form = formObject.AddComponent<ShadowTrialForm>();
            EnsureEnemyAwake(form, typeof(EnemyBase));
            EnsureFlashSubscribed(formFlash);

            SetField(boss, "twoFormTrial", true);
            SetField(boss, "trialForm", form);
            SetField(boss, "_twinPos", (Vector2)formObject.transform.position);
            var trialState = (ShadowTwinTrialState)typeof(ShadowTwinsBoss).GetField("_trialState", PrivateInstance).GetValue(boss);
            trialState.Reset(200f);
            bossHealth.Guard = (Func<DamageInfo, DamageInfo>)Delegate.CreateDelegate(typeof(Func<DamageInfo, DamageInfo>),
                boss, typeof(ShadowTwinsBoss).GetMethod("GuardTrialDamage", PrivateInstance));
            bossHealth.FinalDamage = (Func<float, float>)Delegate.CreateDelegate(typeof(Func<float, float>),
                boss, typeof(ShadowTwinsBoss).GetMethod("TrialDamage", PrivateInstance));
            formHealth.DamageRedirect = (Health.DamageReceiver)Delegate.CreateDelegate(typeof(Health.DamageReceiver),
                boss, typeof(ShadowTwinsBoss).GetMethod("RedirectTwinDamage", PrivateInstance));
        }

        private static void EnsureEnemyAwake(EnemyBase enemy, Type ownerType)
        {
            FieldInfo field = typeof(EnemyBase).GetField("health", PrivateInstance);
            Assert.IsNotNull(field);
            if (field.GetValue(enemy) != null) return;
            MethodInfo awake = ownerType.GetMethod("Awake", PrivateInstance);
            Assert.IsNotNull(awake);
            awake.Invoke(enemy, null);
        }

        private static void EnsureFlashSubscribed(HitFlash flash)
        {
            Type type = typeof(HitFlash);
            FieldInfo field = type.GetField("_health", PrivateInstance);
            Assert.IsNotNull(field);
            if (field.GetValue(flash) == null) type.GetMethod("Awake", PrivateInstance).Invoke(flash, null);
            // EditMode에서 AddComponent가 OnEnable을 자동 호출했는지와 상관없이 한 번만 듣는다.
            type.GetMethod("OnDisable", PrivateInstance).Invoke(flash, null);
            type.GetMethod("OnEnable", PrivateInstance).Invoke(flash, null);
        }

        [Test]
        public void ProxyReturnsRealDamageWithoutSpendingItsOwnLife()
        {
            var bossObject = new GameObject("TrialBossHealth");
            var formObject = new GameObject("TrialFormHealth");
            try
            {
                Health boss = bossObject.AddComponent<Health>();
                boss.Init(Team.Enemy, 200f);
                Health form = formObject.AddComponent<Health>();
                form.Init(Team.Enemy, 100f);
                form.DamageRedirect = boss.TryTakeDamage;
                DamageLog.Reset();

                var hit = new DamageInfo { Amount = 60f, AttackerTeam = Team.Player, Kind = DamageKind.BasicAttack };
                Assert.IsTrue(form.TryTakeDamage(hit, out float dealt));
                Assert.AreEqual(60f, dealt);
                Assert.AreEqual(140f, boss.Current);
                Assert.AreEqual(100f, form.Current);
                Assert.AreEqual(60f, DamageLog.BasicAttack);
            }
            finally
            {
                Object.DestroyImmediate(formObject);
                Object.DestroyImmediate(bossObject);
            }
        }

        [Test]
        public void FinalCapKeepsOtherTrialFormAliveAfterIncomingBonus()
        {
            var go = new GameObject("TrialHealthTest");
            try
            {
                Health health = go.AddComponent<Health>();
                health.Init(Team.Enemy, 200f);
                health.Guard = hit =>
                {
                    hit.Amount = Mathf.Min(hit.Amount, 100f);
                    return hit;
                };
                health.IncomingModifier = hit =>
                {
                    hit.Amount *= 2f;
                    return hit;
                };
                health.FinalDamage = incoming => Mathf.Min(incoming, 100f);

                var hit = new DamageInfo { Amount = 200f, AttackerTeam = Team.Player, Kind = DamageKind.BasicAttack };
                Assert.IsTrue(health.TryTakeDamage(hit, out float dealt));
                Assert.AreEqual(100f, dealt);
                Assert.AreEqual(100f, health.Current);
                Assert.IsFalse(health.IsDead);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TwoFormTrial_LastHitClearsFloatResidue_SoTheBossCanDie()
        {
            // 형상별로 깎은 합과 본체 체력이 float 오차로 다르면 0.004가 남아 못 이겼다 (Codex 리뷰 🔴)
            var state = new HistoryHeroes.Enemy.ShadowTwinTrialState();
            state.Reset(112800f);
            float body = 112800f;
            var rng = new System.Random(7);
            bool second = false;
            for (int guard = 0; guard < 100000 && body > 0f; guard++)
            {
                if (!state.FirstAlive) second = true;
                else if (!state.SecondAlive) second = false;
                else second = rng.Next(2) == 1;
                float incoming = (float)(rng.NextDouble() * 777.77 + 0.1);
                bool otherDown = second ? !state.FirstAlive : !state.SecondAlive;
                float damage = HistoryHeroes.Enemy.ShadowTwinTrialState.FinalDamage(incoming, state.Remaining(second), otherDown, body);
                float dealt = Mathf.Min(body, damage);
                body -= dealt;
                state.Apply(second, dealt);
            }

            Assert.AreEqual(0f, body, "마지막 한 방이 본체를 정확히 0으로");
            Assert.IsTrue(state.Complete);
        }
    }
}
