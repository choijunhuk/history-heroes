using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>설치 자리는 고정하고 매 발마다 가까운 적을 다시 찾는다. 대표 수호상의 정지 공격과는 별개다.</summary>
    public sealed class PharosBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private GameObject stoneModel;
        [SerializeField] private float searchRange = 6f;
        [SerializeField] private Color lightColor = new Color(0.4f, 0.9f, 0.8f, 0.7f);

        private readonly List<EnemyBase> _hits = new List<EnemyBase>();

        protected override bool Activate()
        {
            if (player.Health.IsDead || CombatQuery.Nearest(player.transform.position, searchRange) == null) return false;
            StartCoroutine(Shine(player.transform.position, data.CountAt(level), data.DurationAt(level), data.RadiusAt(level), data.DamageAt(level)));
            return true;
        }

        private IEnumerator Shine(Vector2 center, int count, float duration, float radius, float coefficient)
        {
            var lamp = new GameObject("Pharos").transform;
            lamp.SetParent(EffectRoot, false);
            lamp.position = center;
            SkillArt.Attach(stoneModel, lamp, 0.7f);
            int pulses = Mathf.Max(1, count);
            float interval = Mathf.Max(0.01f, duration / pulses);
            for (int i = 0; i < pulses; i++)
            {
                while (IsPaused) yield return null;
                if (player == null || player.Health.IsDead) break;
                EnemyBase target = CombatQuery.Nearest(center, searchRange);
                if (target != null)
                {
                    Vector2 point = target.transform.position;
                    AttackFx.Line(center, point, 0.08f, lightColor, 0.2f);
                    AttackFx.Burst(point, radius, lightColor, 0.2f);
                    CombatQuery.EnemiesWithin(point, radius, _hits);
                    foreach (EnemyBase enemy in _hits)
                        if (enemy != null && !enemy.Health.IsDead) enemy.Health.TryTakeDamage(RollDamage(coefficient));
                }

                // 마지막 발 뒤에도 남은 한 간격을 보여 주어 소환 시간이 데이터와 같게 한다.
                yield return new WaitForSeconds(interval);
            }

            Destroy(lamp.gameObject);
        }
    }
}
