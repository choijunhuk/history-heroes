using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 5구간 중간 보스 ⚖️ 망각의 집행자 (Modes 2-8): 바닥 균열 (큰 원, 1.2초 전 금) + 1~4구간 중간 보스 패턴을 하나씩 돌아가며
    /// (들소 돌진 3연속 · 횃불 휘두르기 · 증기 분출 · 그림자 분신).
    /// </summary>
    public sealed class OblivionExecutorBoss : PatternBoss
    {
        [SerializeField] private EnemyBase clonePrefab;
        [SerializeField] private float crackRadius = 1.5f;

        private int _step;

        protected override IEnumerator Cycle()
        {
            _step++;
            if (_step % 2 == 1)
            {
                // 망각 균열: 플레이어 자리 + 양옆
                Vector2 p = PlayerPos;
                Vector2 side = new Vector2(-ToPlayer.normalized.y, ToPlayer.normalized.x) * 2.6f;
                var points = new List<Vector2> { p, Inside(p + side), Inside(p - side) };
                yield return CircleStrikes(points, crackRadius, 1.2f * Tele, 1.3f, "mid_exec_crack", new Color(0.55f, 0.3f, 0.85f, 0.5f));
                yield return Open(0.8f);
                yield break;
            }

            switch ((_step / 2) % 4)
            {
                case 0:
                    for (int i = 0; i < 3; i++) yield return Dash(0.7f * Tele, 11f, 1.1f, 1.3f, "mid_exec_dash", i == 2 ? 1.8f : 0.3f, 0.3f);
                    break;
                case 1:
                    yield return ArcStrike(2.8f, 140f, 0.8f * Tele, 1.4f, "mid_exec_swing");
                    yield return ArcStrike(2.8f, 140f, 0.55f * Tele, 1.4f, "mid_exec_swing");
                    yield return Open(1.3f);
                    break;
                case 2:
                {
                    var ring = new List<Vector2>();
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        ring.Add(Inside(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f));
                    }

                    yield return CircleStrikes(ring, 1.1f, 1f * Tele, 1f, "mid_exec_steam", new Color(0.95f, 0.95f, 1f, 0.5f));
                    yield return Open(1.2f);
                    break;
                }
                default:
                    yield return Summon(clonePrefab, 2, 2.2f, 0.9f * Tele, 0.12f);
                    Blink(PlayerPos + Random.insideUnitCircle.normalized * 3f);
                    yield return Open(0.8f);
                    break;
            }
        }
    }
}
