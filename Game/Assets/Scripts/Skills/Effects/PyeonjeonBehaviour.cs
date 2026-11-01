using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 이성계 전용 액티브 "편전" (확장 결정 12: 새 영웅): 5초마다 아주 빠른 작은 화살 3발이 화면에서 체력이 가장 많은 적에게 (각 60%).
    /// 전용 조합 "신궁" (진화: 편전 Lv5 + 치명타 피해): 편전에 맞은 적에게 표식 6초 — 다음 3단 화살 피해 +100%.
    /// </summary>
    public sealed class PyeonjeonBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float arrowSpeed = 26f;
        [SerializeField] private float interval = 0.08f;
        [SerializeField] private bool mark;
        [SerializeField] private float markSeconds = 6f;

        private readonly List<EnemyBase> _screen = new List<EnemyBase>();

        protected override bool Activate()
        {
            CombatQuery.EnemiesOnScreen(_screen);
            EnemyBase target = null;
            foreach (EnemyBase e in _screen)
            {
                if (e == null || e.Health.IsDead) continue;
                if (target == null || e.Health.Current > target.Health.Current) target = e;
            }

            if (target == null) return false;
            StartCoroutine(Fire(target));
            return true;
        }

        private IEnumerator Fire(EnemyBase target)
        {
            int count = data.CountAt(level);
            for (int i = 0; i < count; i++)
            {
                if (player == null || target == null || target.Health.IsDead) yield break;
                Vector2 origin = player.transform.position;
                Vector2 aim = ((Vector2)target.transform.position - origin).normalized;
                player.LaunchSkillProjectile(origin, aim, arrowSpeed, RollDamage(data.DamageAt(level)), 0,
                    mark ? OnHit : (System.Action<Health, Vector2>)null, owner: this);
                yield return new WaitForSeconds(interval);
            }
        }

        private void OnHit(Health target, Vector2 point)
        {
            if (player == null || target == null || target.IsDead) return;
            player.MarkForCharge(target, markSeconds);
            FxBursts.Puff((Vector3)point + Vector3.back * 0.6f, new Color(1f, 0.5f, 0.3f), 0.6f);
        }
    }
}
