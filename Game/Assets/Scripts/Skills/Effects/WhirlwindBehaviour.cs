using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 회오리: 6초마다 적이 가장 많은 곳에 회오리 2초 (레벨마다 +0.5초), 주변 적을 끌어모은다 (Skills.md 2장). 다른 광역 스킬과 연계용.
    /// 눈보라 (합체, freezeAtEnd): 2.5초 동안 끌어모으며 초당 60%, 끝날 때 1.5초 빙결 (4-4).
    /// 이 스킬의 "피해" 값 = 초당 피해 (회오리는 0).
    /// </summary>
    public sealed class WhirlwindBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float pullSpeed = 4f;
        [SerializeField] private Color color = new Color(0.75f, 0.9f, 1f, 0.35f);
        [SerializeField] private bool freezeAtEnd;
        [SerializeField] private float freezeDuration = 1.5f;

        private readonly List<EnemyBase> _inside = new List<EnemyBase>();
        private ComponentPool<FxSprite> _fx;

        protected override bool Activate()
        {
            if (!CombatQuery.DensestPoint(2f, out Vector2 center)) return false;
            StartCoroutine(Run(center));
            return true;
        }

        private IEnumerator Run(Vector2 center)
        {
            float radius = data.RadiusAt(level);
            float duration = data.DurationAt(level);
            float dps = data.DamageAt(level);

            _fx ??= FxSprite.CreatePool(EffectRoot);
            FxSprite visual = _fx.Get(center, Quaternion.identity);
            visual.Hold(PlaceholderSprites.Fan(300), center, Vector2.one * radius * 2f, color, -1);
            Transform go = visual.transform;

            float tick = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (!IsPaused)
                {
                    go.Rotate(0f, 0f, -540f * Time.deltaTime);
                    CombatQuery.EnemiesWithin(center, radius, _inside);
                    foreach (EnemyBase e in _inside) e.Status.Pull(center, pullSpeed, 0.15f);

                    tick -= Time.deltaTime;
                    if (dps > 0f && tick <= 0f)
                    {
                        tick += DamageZone.TickInterval;
                        foreach (EnemyBase e in _inside) e.Health.TryTakeDamage(RollDamage(dps * DamageZone.TickInterval));
                    }
                }

                yield return null;
            }

            if (freezeAtEnd)
            {
                CombatQuery.EnemiesWithin(center, radius, _inside);
                foreach (EnemyBase e in _inside) e.Status.ApplyFreeze(freezeDuration);
                AttackFx.Burst(center, radius, new Color(0.7f, 0.9f, 1f, 0.6f), 0.3f);
            }

            _fx.Release(visual);
        }
    }
}
