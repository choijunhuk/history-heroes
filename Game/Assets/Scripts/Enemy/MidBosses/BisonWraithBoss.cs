using System.Collections;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 1구간 중간 보스 🐃 들소 망령 (Modes 2-8): 큰 돌진 3연속 → 마지막에 벽에 부딪혀 2초 기절 (가장 큰 빈틈).
    /// 2단계: 돌진 뒤 발밑 충격파 (원 예고).
    /// </summary>
    public sealed class BisonWraithBoss : PatternBoss
    {
        [SerializeField] private float dashTelegraph = 0.75f;
        [SerializeField] private float dashSpeed = 11f;
        [SerializeField] private float dashTime = 1.2f;
        [SerializeField] private float dashDamage = 1.3f;
        [SerializeField] private float finalStun = 2f;
        [SerializeField] private float shockRadius = 2.2f;

        protected override IEnumerator Cycle()
        {
            for (int i = 0; i < 3; i++)
            {
                bool last = i == 2;
                // 앞의 두 번은 짧게 멈칫, 마지막은 벽에 박혀 기절
                yield return Dash(dashTelegraph * Tele, dashSpeed, dashTime, dashDamage, "mid_bison", last ? finalStun : 0.35f, last ? 1.2f : 0.3f);
            }

            if (phase >= 2)
            {
                yield return CircleStrike(Pos, shockRadius, 0.9f * Tele, 1.1f, "mid_bison_shock");
                yield return Open(1.2f);
            }
        }
    }
}
