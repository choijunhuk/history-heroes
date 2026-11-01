using System.Collections;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>발동 때 정한 출발점과 방향으로만 검기를 내보내 길 밖을 건드리지 않는다.</summary>
    public sealed class WindSlashBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Projectile wavePrefab;

        protected override bool Activate()
        {
            if (player.Health.IsDead || wavePrefab == null) return false;
            Vector2 origin = player.transform.position;
            EnemyBase target = CombatQuery.Nearest(origin, data.waveLength);
            if (target == null) return false;
            Vector2 aim = (Vector2)target.transform.position - origin;
            if (aim.sqrMagnitude < 0.0001f) aim = Vector2.up;
            aim.Normalize();
            StartCoroutine(Waves(origin, aim, data.CountAt(level)));
            return true;
        }

        private IEnumerator Waves(Vector2 origin, Vector2 aim, int count)
        {
            for (int i = 0; i < count; i++)
            {
                while (IsPaused) yield return null;
                if (player == null || player.Health.IsDead) yield break;
                player.LaunchSkillProjectile(wavePrefab, origin, aim, data.waveSpeed, RollDamage(data.DamageAt(level)),
                    int.MaxValue, (hit, _) => Push(hit, aim), this, data.waveLength, hitOncePerTarget: true);
                if (i < count - 1) yield return new WaitForSeconds(data.waveInterval);
            }
        }

        private void Push(Health hit, Vector2 aim)
        {
            if (hit == null || hit.IsDead || !hit.TryGetComponent(out EnemyBase enemy) || !enemy.CanBeKnockedBack) return;
            enemy.Push(aim * data.pushDistance);
        }

        public override void Dispose()
        {
            StopAllCoroutines();
            base.Dispose();
        }
    }
}
