using System.Collections;
using HistoryHeroes.Combat;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 거울 분신 (확장 1차 5장, 그림자 사냥꾼): 진짜만 발밑에 그림자가 있다.
    /// 진짜를 **기본 공격**으로 맞히면 "간파!" — 2초 동안 공격력 +25% (다시 맞히면 2초가 새로 시작).
    /// 전체 검토 5번: 예전에는 가짜를 맞히면 0.5초 기절이었는데, 기본 공격은 자동 조준이라 플레이어가 고를 수 없는 벌이었다 → 벌 대신 상으로.
    /// 가짜를 맞히면 처음 한 번만 알림으로 진짜를 노리라고 알려 준다. 다빈치 비행 기계 탄은 세지 않는다
    /// </summary>
    public sealed class MirrorStunGimmick : BossGimmick
    {
        public const float FocusSeconds = 2f;
        public const float FocusBonus = 0.25f;

        private readonly object _owner = new object();
        private EnemyData _cloneData;
        private bool _toldFake;
        private bool _toldReal;
        private float _focusUntil;
        private Player.PlayerController _buffed;
        private Coroutine _focus;

        public override string Hint => BossGimmicks.HintFor(GimmickKind.MirrorStun);

        /// <summary>지금 "간파!" 중인가 (QA · 테스트)</summary>
        public bool Focused => _buffed != null && Time.time < _focusUntil;

        protected override void OnBegin()
        {
            _cloneData = boss is ShadowHunterBoss hunter && hunter.ClonePrefab != null ? hunter.ClonePrefab.Data : null;
            _toldFake = false;
            _toldReal = false;
            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDamaged += OnAnyDamaged;
        }

        protected override void OnEnd()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            EndFocus();
        }

        private void OnAnyDamaged(Health h, DamageInfo info, float dealt)
        {
            if (!Running || info.Kind != DamageKind.BasicAttack || h.Team != Team.Enemy) return;
            if (info.SourceId == Player.PlayerController.HelperSourceId) return; // 다빈치 비행 기계는 영웅이 고른 탄이 아니다
            if (!h.TryGetComponent(out EnemyBase e) || hero == null || !hero.TryGetComponent(out Player.PlayerController player)) return;
            if (e == boss)
            {
                Focus(player);
                return;
            }

            if (_cloneData != null && e.Data == _cloneData && !_toldFake)
            {
                _toldFake = true;
                BossGimmicks.Notice?.Invoke("가짜예요! 그림자 있는 진짜를 맞히면 피해 +25%");
            }
        }

        private void Focus(Player.PlayerController player)
        {
            _focusUntil = Time.time + FocusSeconds;
            if (_buffed == null)
            {
                _buffed = player;
                player.Stats.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, FocusBonus, ModifierSource.Skill, _owner));
                FxBursts.Puff(player.transform.position + Vector3.back * 1.1f, new Color(1f, 0.85f, 0.35f), 1.1f);
                BossGimmicks.Notice?.Invoke(_toldReal ? "간파!" : "간파! 진짜를 맞혔다 — 2초 동안 피해 +25%");
                _toldReal = true;
            }

            if (_focus == null) _focus = StartCoroutine(FocusTimer());
        }

        private IEnumerator FocusTimer()
        {
            while (Time.time < _focusUntil) yield return null;
            _focus = null;
            EndFocus();
        }

        private void EndFocus()
        {
            if (_focus != null)
            {
                StopCoroutine(_focus);
                _focus = null;
            }

            if (_buffed != null) _buffed.Stats.RemoveAllFrom(_owner);
            _buffed = null;
        }
    }
}
