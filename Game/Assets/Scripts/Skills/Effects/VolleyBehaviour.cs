using System.Collections;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 칭기즈 칸 전용 — 만궁 연사: 5초마다 가장 가까운 적 쪽으로 부채꼴 화살 5발 (각 60%), 레벨마다 +1발 (Skills.md 3장).
    /// 천호의 화살비 (진화, bursts 2): 부채꼴 화살이 2번 연속 (0.3초 간격).
    /// </summary>
    public sealed class VolleyBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float fanDegrees = 50f;
        [SerializeField] private float arrowSpeed = 15f;
        [SerializeField] private float searchRange = 9f;
        [SerializeField, Min(1)] private int bursts = 1;
        [SerializeField] private float burstInterval = 0.3f;

        protected override bool Activate()
        {
            EnemyBase target = CombatQuery.Nearest(player.transform.position, searchRange);
            if (target == null) return false;
            StartCoroutine(Fire(target));
            return true;
        }

        private IEnumerator Fire(EnemyBase target)
        {
            for (int b = 0; b < bursts; b++)
            {
                Vector2 origin = player.transform.position;
                Vector2 aimAt = target != null && !target.Health.IsDead ? (Vector2)target.transform.position : origin + Vector2.up;
                Vector2 aim = (aimAt - origin).normalized;
                int count = data.CountAt(level);
                float step = count > 1 ? fanDegrees / (count - 1) : 0f;
                float start = -fanDegrees * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    Vector2 direction = Quaternion.Euler(0f, 0f, count > 1 ? start + step * i : 0f) * aim;
                    player.LaunchSkillProjectile(origin, direction, arrowSpeed, RollDamage(data.DamageAt(level)), owner: this);
                }

                if (b < bursts - 1) yield return new WaitForSeconds(burstInterval);
            }
        }
    }
}
