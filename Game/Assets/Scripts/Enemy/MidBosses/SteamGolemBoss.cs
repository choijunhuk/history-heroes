using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 3구간 중간 보스 ⚙️ 증기 골렘 (Modes 2-8): 증기 분출 (몸 둘레 원 6개) + 컨베이어 (플레이어를 골렘 쪽으로 끌다가 반대로).
    /// 내려찍기 (플레이어 자리 원) 뒤 빈틈. 2단계: 증기가 두 겹.
    /// </summary>
    public sealed class SteamGolemBoss : PatternBoss
    {
        [SerializeField] private float steamRadius = 1.1f;
        [SerializeField] private float steamDistance = 2f;
        [SerializeField] private float steamTelegraph = 1f;
        [SerializeField] private float steamDamage = 1f;
        [SerializeField] private float conveyorSpeed = 2.2f;
        [SerializeField] private float slamRadius = 1.8f;

        private int _step;

        protected override IEnumerator Cycle()
        {
            _step++;
            switch (_step % 3)
            {
                case 1:
                {
                    var points = new List<Vector2>();
                    int rings = phase >= 2 ? 2 : 1;
                    for (int r = 0; r < rings; r++)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            float a = (i + r * 0.5f) * Mathf.PI / 3f;
                            points.Add(Inside(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * steamDistance * (1f + r * 0.9f)));
                        }
                    }

                    yield return CircleStrikes(points, steamRadius, steamTelegraph * Tele, steamDamage, "mid_golem_steam", new Color(0.95f, 0.95f, 1f, 0.5f));
                    yield return Open(1.2f);
                    break;
                }

                case 2:
                {
                    // 컨베이어: 골렘 쪽으로 끌다가 → 반대로 (플레이어가 밀리는 방향이 바뀐다)
                    Vector2 toGolem = (Pos - PlayerPos).normalized;
                    yield return PushPlayer(toGolem, conveyorSpeed, 1.6f, new Color(0.6f, 0.6f, 0.65f, 0.25f));
                    yield return PushPlayer(-toGolem, conveyorSpeed * 1.2f, 1.2f, new Color(0.85f, 0.55f, 0.3f, 0.25f));
                    break;
                }

                default:
                    yield return CircleStrike(PlayerPos, slamRadius, 1f * Tele, 1.4f, "mid_golem_slam");
                    yield return Open(1.6f);
                    break;
            }
        }
    }
}
