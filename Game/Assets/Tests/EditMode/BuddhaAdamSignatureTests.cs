using System.IO;
using System.Reflection;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Heroes;
using HistoryHeroes.Player;
using HistoryHeroes.Stats;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class BuddhaAdamSignatureTests
    {
        [Test]
        public void BuddhaCountsOnlyActualStationaryAttackStartsAndKeepsReuseAcrossRoom()
        {
            var state = new BuddhaBreathState();
            Assert.IsFalse(state.RecordAttack(false, 3, 0f, 8f));
            Assert.IsFalse(state.RecordAttack(true, 3, 0f, 8f));
            Assert.IsFalse(state.RecordAttack(true, 3, 0.5f, 8f));
            Assert.AreEqual(2, state.Count);
            state.ResetPreparation();
            Assert.AreEqual(0, state.Count);
            Assert.IsFalse(state.RecordAttack(true, 3, 1f, 8f));
            Assert.IsFalse(state.RecordAttack(true, 3, 2f, 8f));
            Assert.IsTrue(state.RecordAttack(true, 3, 3f, 8f));
            Assert.AreEqual(8f, state.CooldownRemaining(3f));
            state.ResetPreparation();
            Assert.IsFalse(state.RecordAttack(true, 3, 10.9f, 8f));
            Assert.AreEqual(0, state.Count);
            Assert.IsFalse(state.RecordAttack(true, 3, 11f, 8f));
            Assert.AreEqual(1, state.Count);
        }

        [TestCase(1, 0.05f, 2f)]
        [TestCase(3, 0.05f, 2.5f)]
        [TestCase(5, 0.06f, 2.5f)]
        public void BuddhaStarValues(int stars, float fraction, float duration)
        {
            var hero = ScriptableObject.CreateInstance<HeroData>();
            try
            {
                hero.breathAttacks = 3;
                hero.breathShieldFraction = 0.05f;
                hero.breathShieldDuration = 2f;
                hero.breathCooldown = 8f;
                hero.star3BreathShieldDuration = 2.5f;
                hero.star5BreathShieldFraction = 0.06f;
                HeroSignature signature = HeroSignature.For(hero, stars);
                Assert.AreEqual(fraction, signature.BreathShieldFraction, 0.0001f);
                Assert.AreEqual(duration, signature.BreathShieldDuration, 0.0001f);
                Assert.AreEqual(8f, signature.BreathCooldown);
            }
            finally { Object.DestroyImmediate(hero); }
        }

        [Test]
        public void AdamNeedsUnbrokenActualMovementAndOnlyFirstStationaryAttackGrantsDodge()
        {
            var state = new AdamFootstepState();
            state.RecordMovement(0.7f, true, 0f, 0.8f);
            state.RecordMovement(0.1f, false, 0.7f, 0.8f);
            Assert.IsFalse(state.IsReady);
            state.RecordMovement(0.79f, true, 1f, 0.8f);
            Assert.IsFalse(state.TryBeginAttack(true, 1.79f, 6f, 1.5f));
            state.RecordMovement(0.01f, true, 1.8f, 0.8f);
            Assert.IsTrue(state.IsReady);
            state.RecordMovement(1f, false, 2f, 0.8f);
            Assert.IsTrue(state.IsReady, "표적을 찾기 전까지 준비를 보관한다");
            Assert.IsTrue(state.TryBeginAttack(true, 2f, 6f, 1.5f));
            Assert.IsFalse(state.TryBeginAttack(true, 2.1f, 6f, 1.5f));
            Assert.AreEqual(6f, state.CooldownRemaining(2f));
            Assert.IsTrue(state.BonusActive(2.5f));
            state.OnMoved();
            Assert.IsFalse(state.BonusActive(2.5f));
            state.RecordMovement(1f, true, 3f, 0.8f);
            Assert.IsFalse(state.IsReady, "재사용 중에는 새 준비를 쌓지 않는다");
            state.ResetForRoom();
            Assert.AreEqual(5f, state.CooldownRemaining(3f));
            state.RecordMovement(0.8f, true, 8f, 0.8f);
            Assert.IsTrue(state.TryBeginAttack(true, 8f, 6f, 2f));
        }

        [Test]
        public void AdamMovementExcludesBlockedInputAndDevicePush()
        {
            Assert.IsFalse(AdamFootstepState.IsJoystickMovement(Vector2.right, Vector2.zero, Vector2.zero));
            Assert.IsFalse(AdamFootstepState.IsJoystickMovement(Vector2.zero, Vector2.right, Vector2.zero));
            Assert.IsFalse(AdamFootstepState.IsJoystickMovement(Vector2.right, Vector2.right, Vector2.right));
            Assert.IsFalse(AdamFootstepState.IsJoystickMovement(Vector2.right, Vector2.left, Vector2.zero));
            Assert.IsTrue(AdamFootstepState.IsJoystickMovement(Vector2.right, new Vector2(0.1f, 0f), Vector2.zero));
            Assert.IsTrue(AdamFootstepState.IsJoystickMovement(Vector2.right, new Vector2(0.3f, 0f), new Vector2(0.2f, 0f)));
        }

        [Test]
        public void DevicePushReportsItsDisplacementToThePlayer()
        {
            // 켜진 오브젝트여야 콜라이더에 강체가 연결된다 (실제 게임과 같게). 에디트 모드라 PlayerController.Awake는 돌지 않는다
            var go = new GameObject("PushProbe");
            try
            {
                Rigidbody2D body = go.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
                PlayerController player = go.AddComponent<PlayerController>();
                Physics2D.SyncTransforms();
                RoomDevices.Push(collider, new Vector2(0.4f, 0.1f));
                FieldInfo field = typeof(PlayerController).GetField("_externalDisplacement", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field);
                Assert.AreEqual(new Vector2(0.4f, 0.1f), (Vector2)field.GetValue(player));
                Assert.AreEqual(new Vector2(0.4f, 0.1f), body.position);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ExistingTrapAndBossPushPathsReportExternalMovement()
        {
            string trap = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Dungeon/TrapZone.cs"));
            string boss = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Enemy/PatternBoss.cs"));
            StringAssert.Contains("RoomDevices.Push(_hits[i]", trap, "컨베이어는 공통 강제 이동 경로를 쓴다");
            StringAssert.Contains("RoomDevices.Push(c,", trap, "함정의 밀어내기도 같은 경로를 쓴다");
            StringAssert.Contains("pushedPlayer.NoteExternalDisplacement(delta)", boss, "보스의 밀기는 플레이어에 외부 이동량을 알린다");
        }

        [Test]
        public void AdamDodgeStarsAndGlobalCap()
        {
            var hero = ScriptableObject.CreateInstance<HeroData>();
            try
            {
                hero.footstepMoveSeconds = 0.8f;
                hero.footstepDodgeBonus = 0.10f;
                hero.footstepDodgeDuration = 1.5f;
                hero.footstepCooldown = 6f;
                hero.star3FootstepDodgeDuration = 2f;
                hero.star5FootstepDodgeBonus = 0.12f;
                Assert.AreEqual(1.5f, HeroSignature.For(hero, 1).FootstepDodgeDuration);
                Assert.AreEqual(2f, HeroSignature.For(hero, 3).FootstepDodgeDuration);
                Assert.AreEqual(0.12f, HeroSignature.For(hero, 5).FootstepDodgeBonus);
                Assert.AreEqual(0.25f, StatCaps.Clamp(StatType.Dodge, 0.18f + HeroSignature.For(hero, 5).FootstepDodgeBonus));
            }
            finally { Object.DestroyImmediate(hero); }
        }

        [Test]
        public void AdamDodgeAddsToOtherSourcesAndRemovingItPreservesThePassive()
        {
            var stats = new StatSheet();
            var passive = new object();
            var footstep = new object();
            stats.Add(new StatModifier(StatType.Dodge, ModifierKind.Flat, 0.08f, ModifierSource.Skill, passive));
            stats.Add(new StatModifier(StatType.Dodge, ModifierKind.Flat, 0.10f, ModifierSource.Hero, footstep));
            Assert.AreEqual(0.18f, stats.Get(StatType.Dodge), 0.0001f);
            stats.RemoveAllFrom(passive);
            stats.Add(new StatModifier(StatType.Dodge, ModifierKind.Flat, 0.18f, ModifierSource.Skill, passive));
            stats.RemoveAllFrom(footstep);
            stats.Add(new StatModifier(StatType.Dodge, ModifierKind.Flat, 0.12f, ModifierSource.Hero, footstep));
            Assert.AreEqual(0.25f, stats.Get(StatType.Dodge), 0.0001f);
            stats.RemoveAllFrom(footstep);
            Assert.AreEqual(0.18f, stats.Get(StatType.Dodge), 0.0001f);
        }
    }
}
