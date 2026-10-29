using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>모래 수호상은 느리게 할 뿐이다. 진화한 모래의 문에서만 적의 첫 진입에 파동이 난다.</summary>
    public sealed class SandStatueBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private GameObject stoneModel;
        [SerializeField] private float searchRange = 8f;
        [SerializeField] private Color tint = new Color(0.78f, 0.62f, 0.28f, 0.45f);

        private readonly List<EnemyBase> _near = new List<EnemyBase>();
        private readonly List<EnemyBase> _waveHits = new List<EnemyBase>();
        private readonly Dictionary<EnemyBase, int> _entered = new Dictionary<EnemyBase, int>();
        private readonly Dictionary<EnemyBase, int> _waveDamaged = new Dictionary<EnemyBase, int>();
        private Coroutine _guard;
        private GameObject _statue;

        protected override bool Activate()
        {
            if (player.Health.IsDead) return false;
            EnemyBase target = CombatQuery.Nearest(player.transform.position, searchRange);
            if (target == null) return false;

            RemoveStatue();
            Vector2 center = target.transform.position;
            if (_statue == null)
            {
                _statue = new GameObject("SandStatue");
                _statue.transform.SetParent(EffectRoot, false);
                SkillArt.Attach(stoneModel != null ? stoneModel : player.Hero.helperModel, _statue.transform, 0.65f);
            }
            _statue.transform.position = center;
            _statue.SetActive(true);
            AttackFx.Burst(center, data.RadiusAt(level), tint, 0.3f);
            _entered.Clear();
            _waveDamaged.Clear();
            _guard = StartCoroutine(Guard(center, data.RadiusAt(level), data.DurationAt(level), data.SlowAt(level), data.DamageAt(level)));
            return true;
        }

        private IEnumerator Guard(Vector2 center, float radius, float duration, float slow, float waveDamage)
        {
            float until = Time.time + duration;
            var pulse = new WaitForSeconds(0.1f);
            while (Time.time < until && player != null && !player.Health.IsDead)
            {
                if (!IsPaused)
                {
                    CombatQuery.EnemiesWithin(center, radius, _near);
                    foreach (EnemyBase enemy in _near)
                    {
                        if (enemy == null || enemy.Health.IsDead) continue;
                        enemy.Status.ApplySlow(slow * (enemy is IPhasedBoss ? data.slowBossScale : 1f), 0.2f, this);
                        if (waveDamage > 0f && (!_entered.TryGetValue(enemy, out int life) || life != enemy.Health.Life))
                        {
                            _entered[enemy] = enemy.Health.Life;
                            Wave(enemy.transform.position, waveDamage);
                        }
                    }
                }

                yield return pulse;
            }

            _guard = null;
            if (_statue != null) _statue.SetActive(false);
        }

        private void Wave(Vector2 point, float coefficient)
        {
            if (data.waveRadius <= 0f) return;
            AttackFx.Burst(point, data.waveRadius, tint, 0.18f);
            CombatQuery.EnemiesWithin(point, data.waveRadius, _waveHits);
            foreach (EnemyBase enemy in _waveHits)
            {
                if (enemy == null || enemy.Health.IsDead) continue;
                if (_waveDamaged.TryGetValue(enemy, out int life) && life == enemy.Health.Life) continue;
                _waveDamaged[enemy] = enemy.Health.Life;
                enemy.Health.TryTakeDamage(RollDamage(coefficient));
            }
        }

        private void RemoveStatue()
        {
            if (_guard != null) StopCoroutine(_guard);
            _guard = null;
            if (_statue != null) _statue.SetActive(false);
        }

        public override void Dispose()
        {
            RemoveStatue();
            base.Dispose();
        }
    }
}
