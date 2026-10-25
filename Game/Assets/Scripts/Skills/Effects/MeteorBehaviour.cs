using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 유성 낙하 (전설): 10초마다 적이 가장 많은 곳에 유성 (400%, 범위 큼). 0.8초 전에 떨어질 자리를 보여준다.
    /// 레벨마다 피해 +60% (Lv1 피해의 60% = +240%p, 화살비와 같은 해석) (Skills.md 2장).
    /// </summary>
    public sealed class MeteorBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private float telegraph = 0.8f;
        [SerializeField] private Color markerColor = new Color(1f, 0.3f, 0.15f, 0.35f);
        [SerializeField] private Color blastColor = new Color(1f, 0.6f, 0.25f, 0.7f);

        private readonly List<EnemyBase> _hits = new List<EnemyBase>();

        protected override bool Activate()
        {
            if (!CombatQuery.DensestPoint(2.2f, out Vector2 center)) return false;
            StartCoroutine(Fall(center));
            return true;
        }

        private IEnumerator Fall(Vector2 center)
        {
            float radius = data.RadiusAt(level);
            var marker = new GameObject("MeteorMarker");
            marker.transform.SetParent(EffectRoot, false);
            marker.transform.position = center;
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Circle;
            sr.color = markerColor;
            sr.sortingOrder = -1;

            for (float t = 0f; t < telegraph; t += Time.deltaTime)
            {
                float k = t / telegraph;
                marker.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.3f, 1f, k);
                yield return null;
            }

            Destroy(marker);
            AttackFx.Burst(center, radius, blastColor, 0.35f);
            CombatQuery.EnemiesWithin(center, radius, _hits);
            foreach (EnemyBase e in _hits) e.Health.TryTakeDamage(RollDamage(data.DamageAt(level)));
        }
    }
}
