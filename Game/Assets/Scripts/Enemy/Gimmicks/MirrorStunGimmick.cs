using System.Collections;
using HistoryHeroes.Combat;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 거울 분신 (확장 1차 5장, 그림자 사냥꾼): 진짜만 발밑에 그림자가 있다. 가짜(분신)를 **기본 공격**으로 맞히면 0.5초 기절 (나).
    /// 공정하게: 스킬(자동 광역)로 맞힌 건 기절 없음, 기절은 2.5초에 한 번까지. 처음 한 번은 알림으로 알려 준다
    /// </summary>
    public sealed class MirrorStunGimmick : BossGimmick
    {
        public const float Stun = 0.5f;
        public const float Cooldown = 2.5f;

        private readonly object _owner = new object();
        private EnemyData _cloneData;
        private float _readyAt;
        private bool _told;
        private Player.PlayerController _stunned;

        public override string Hint => BossGimmicks.HintFor(GimmickKind.MirrorStun);

        protected override void OnBegin()
        {
            _cloneData = boss is ShadowHunterBoss hunter && hunter.ClonePrefab != null ? hunter.ClonePrefab.Data : null;
            _readyAt = 0f;
            _told = false;
            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDamaged += OnAnyDamaged;
        }

        protected override void OnEnd()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            Unstun();
        }

        private void OnAnyDamaged(Health h, DamageInfo info, float dealt)
        {
            if (!Running || _cloneData == null || info.Kind != DamageKind.BasicAttack || h.Team != Team.Enemy) return;
            if (!h.TryGetComponent(out EnemyBase e) || e == boss || e.Data != _cloneData) return;
            if (Time.time < _readyAt || hero == null || !hero.TryGetComponent(out Player.PlayerController player)) return;
            _readyAt = Time.time + Cooldown;
            StartCoroutine(StunFor(player));
        }

        private IEnumerator StunFor(Player.PlayerController player)
        {
            Unstun();
            _stunned = player;
            player.Root(Stun); // 코드 검토 3번: 이동 속도 하한과 상관없이 정말 멈춘다
            FxBursts.Puff(player.transform.position + Vector3.back * 1.1f, new Color(1f, 0.9f, 0.4f), 1f);
            BossGimmicks.Notice?.Invoke(_told ? "가짜! 기절" : "가짜였다! 그림자 있는 진짜를 노려요 (0.5초 기절)");
            _told = true;
            yield return new WaitForSeconds(Stun);
            Unstun();
        }

        private void Unstun() => _stunned = null; // 묶임은 시간이 지나면 스스로 풀린다 (Root)
    }
}
