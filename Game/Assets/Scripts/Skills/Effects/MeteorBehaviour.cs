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
        private ComponentPool<FxSprite> _fx;

        protected override bool Activate()
        {
            if (!CombatQuery.DensestPoint(2.2f, out Vector2 center)) return false;
            StartCoroutine(Fall(center));
            return true;
        }

        private IEnumerator Fall(Vector2 center)
        {
            float radius = data.RadiusAt(level);
            _fx ??= FxSprite.CreatePool(EffectRoot);
            FxSprite marker = _fx.Get(center, Quaternion.identity);
            marker.Hold(PlaceholderSprites.Circle, center, Vector2.one * radius * 0.6f, markerColor, -1);

            // 그래픽 평가 5번: 하늘에서 비스듬히 떨어지는 빛나는 바위 (블렌더 유성 + 불꼬리)
            GameObject rock = null;
            SkillArt art = SkillArt.Get;
            var from = new Vector3(center.x + 2.2f, center.y + 3.2f, -10f);
            var to = new Vector3(center.x, center.y, -0.4f);
            if (art != null && art.meteor != null)
            {
                rock = Instantiate(art.meteor, from, Quaternion.identity, EffectRoot);
                rock.transform.localScale = Vector3.one * Mathf.Clamp(radius * 0.7f, 0.9f, 1.8f);
                SkillArt.Trail(rock, new Color(1f, 0.55f, 0.2f), 0.6f, 0.25f);
            }

            for (float t = 0f; t < telegraph; t += Time.deltaTime)
            {
                float k = t / telegraph;
                marker.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.3f, 1f, k);
                if (rock != null)
                {
                    rock.transform.position = Vector3.Lerp(from, to, k * k); // 점점 빨라진다
                    rock.transform.Rotate(220f * Time.deltaTime, 140f * Time.deltaTime, 0f, Space.Self);
                }

                yield return null;
            }

            if (rock != null) Destroy(rock);
            Common.CameraShake.Add(0.35f);
            _fx.Release(marker);
            AttackFx.Burst(center, radius, blastColor, 0.35f);
            CombatQuery.EnemiesWithin(center, radius, _hits);
            foreach (EnemyBase e in _hits) e.Health.TryTakeDamage(RollDamage(data.DamageAt(level)));
        }
    }
}
