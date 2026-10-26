using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 2구간 중간 보스 🔥 횃불 기사 (Modes 2-8): 불길 휘두르기 (앞쪽 부채꼴) + 화염 분사구 켜기 (바닥 원 여러 개).
    /// 2단계: 휘두르기 2연속, 분사구가 더 많다.
    /// </summary>
    public sealed class TorchKnightBoss : PatternBoss
    {
        [SerializeField] private float swingRadius = 2.8f;
        [SerializeField] private float swingDegrees = 130f;
        [SerializeField] private float swingTelegraph = 0.8f;
        [SerializeField] private float swingDamage = 1.4f;
        [SerializeField] private float ventRadius = 1.1f;
        [SerializeField] private float ventTelegraph = 1.1f;
        [SerializeField] private float ventDamage = 0.9f;

        private int _step;

        protected override IEnumerator Cycle()
        {
            _step++;
            if (_step % 2 == 1)
            {
                int swings = phase >= 2 ? 2 : 1;
                for (int i = 0; i < swings; i++) yield return ArcStrike(swingRadius, swingDegrees, swingTelegraph * Tele * (i > 0 ? 0.7f : 1f), swingDamage, "mid_torch");
                yield return Open(1.4f);
                yield break;
            }

            // 화염 분사구: 플레이어 주변 바닥에 불길 (첫 개는 플레이어 자리)
            int vents = phase >= 2 ? 5 : 3;
            var points = new List<Vector2> { PlayerPos };
            for (int i = 1; i < vents; i++)
            {
                float a = i * Mathf.PI * 2f / (vents - 1) + bossTime;
                points.Add(Inside(PlayerPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f));
            }

            yield return CircleStrikes(points, ventRadius, ventTelegraph * Tele, ventDamage, "mid_torch_vent", new Color(1f, 0.45f, 0.15f, 0.5f));
            yield return Open(1.1f);
        }
    }
}
