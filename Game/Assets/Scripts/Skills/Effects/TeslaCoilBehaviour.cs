using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 테슬라 전용 — 테슬라 코일: 8초마다 발밑에 코일 설치 5초 (레벨마다 +1초), 0.5초마다 반지름 3 안의 가장 가까운 적에게 번개 50%.
    /// 워든클리프 탑 (합체, tower): 탑 8초, 1초마다 화면 안 적 최대 5명에게 번개 60% (Skills.md 4-4).
    /// 코일을 깔아 두고 그 주변에서 싸우는 "자리 잡기" 플레이 (Heroes.md 6장).
    /// </summary>
    public sealed class TeslaCoilBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float zapInterval = 0.5f;
        [Tooltip("탑: 화면 안 적 여러 명에게 (0이면 가장 가까운 1명, 반지름 안)")]
        [SerializeField] private int screenTargets;
        [SerializeField] private Vector2 coilSize = new Vector2(0.5f, 0.9f);
        [SerializeField] private Color coilColor = new Color(0.45f, 0.75f, 1f);
        [SerializeField] private Color zapColor = new Color(0.6f, 0.9f, 1f, 0.95f);

        private readonly List<EnemyBase> _targets = new List<EnemyBase>();
        private ComponentPool<FxSprite> _fx;

        protected override bool Activate()
        {
            StartCoroutine(Run(player.transform.position));
            return true;
        }

        private IEnumerator Run(Vector2 position)
        {
            _fx ??= FxSprite.CreatePool(EffectRoot);
            FxSprite coil = _fx.Get(position, Quaternion.identity);
            coil.Hold(PlaceholderSprites.Square, position, coilSize, coilColor, 1);

            float duration = data.DurationAt(level);
            float radius = data.RadiusAt(level);
            Vector2 top = position + Vector2.up * coilSize.y * 0.5f;

            for (float t = 0f; t < duration; t += zapInterval)
            {
                if (screenTargets > 0)
                {
                    CombatQuery.RandomEnemiesOnScreen(screenTargets, _targets);
                }
                else
                {
                    _targets.Clear();
                    EnemyBase nearest = CombatQuery.Nearest(position, radius);
                    if (nearest != null) _targets.Add(nearest);
                }

                foreach (EnemyBase e in _targets)
                {
                    AttackFx.Line(top, e.transform.position, 0.1f, zapColor);
                    e.Health.TryTakeDamage(RollDamage(data.DamageAt(level)));
                }

                yield return new WaitForSeconds(zapInterval);
            }

            _fx.Release(coil);
        }
    }
}
