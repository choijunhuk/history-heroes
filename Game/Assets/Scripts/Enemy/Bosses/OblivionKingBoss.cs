using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 5구간 보스 👑 망각의 왕 (Content 3장: "위 패턴 전부 → 완성된 빌드"). 단계마다 앞 보스의 시험을 섞는다.
    /// - 1단계: 망령 부르기 + 저주 고리 (광역)
    /// - 2단계: + 회전 탄막 + 망각 균열 (회피·방어)
    /// - 3단계: + 그림자 돌진 2연속 (단일 순간 화력), 예고 0.8배
    /// 패턴마다 빈틈은 그대로 둔다 (완성된 빌드면 어느 빈틈에서도 몰아칠 수 있다).
    /// </summary>
    public sealed class OblivionKingBoss : PatternBoss
    {
        [SerializeField] private EnemyBase minion;
        [SerializeField] private int maxMinions = 6;

        private readonly List<EnemyBase> _minions = new List<EnemyBase>();
        private int _step;

        protected override void OnTick() => _minions.RemoveAll(m => m == null || m.Health.IsDead || !m.gameObject.activeInHierarchy);

        protected override IEnumerator Cycle()
        {
            _step++;
            int patterns = phase == 1 ? 2 : phase == 2 ? 4 : 5;
            switch (_step % patterns)
            {
                case 0:
                {
                    int want = Mathf.Min(3, maxMinions - _minions.Count);
                    int before = EnemyBase.Alive.Count;
                    if (want > 0) yield return Summon(minion, want, 2.4f, 1f * Tele, 1f);
                    IReadOnlyList<EnemyBase> alive = EnemyBase.Alive;
                    for (int i = before; i < alive.Count; i++)
                    {
                        if (alive[i] != this) _minions.Add(alive[i]);
                    }

                    yield return Open(1.6f);
                    break;
                }

                case 1:
                    yield return Glow(0.9f * Tele, new Color(0.7f, 0.4f, 1f, 0.45f));
                    Ring(14, 4.5f, 0.8f, "boss_king_ring");
                    yield return Wait(0.3f);
                    Ring(14, 4.5f, 0.8f, "boss_king_ring", 0.5f);
                    yield return Open(1.4f);
                    break;

                case 2:
                {
                    yield return Glow(0.7f * Tele, new Color(1f, 0.7f, 0.3f, 0.45f));
                    Enter(State.Attack);
                    float spin = 0f;
                    float end = bossTime + 1.8f;
                    while (bossTime < end)
                    {
                        for (int a = 0; a < 3; a++)
                        {
                            float angle = (spin + a * 120f) * Mathf.Deg2Rad;
                            var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                            EnemyProjectiles.Fire(Pos + d, d, 4.3f, Combat.DamageCalculator.Enemy(Damage * 0.55f, "boss_king_spiral"), playerMask, this);
                        }

                        spin += 17f;
                        yield return Wait(0.12f);
                    }

                    yield return Open(1.5f);
                    break;
                }

                case 3:
                {
                    Vector2 p = PlayerPos;
                    Vector2 side = new Vector2(-ToPlayer.normalized.y, ToPlayer.normalized.x) * 2.2f;
                    yield return CircleStrikes(new List<Vector2> { p, Inside(p + side), Inside(p - side) }, 1.4f, 1.2f * Tele, 1.3f, "boss_king_crack", new Color(0.55f, 0.3f, 0.85f, 0.5f));
                    yield return Open(1f);
                    break;
                }

                default:
                    yield return Dash(0.8f * Tele, 11f, 1f, 1.3f, "boss_king_dash", 0.3f, 0.3f);
                    yield return Dash(0.6f * Tele, 11f, 1f, 1.3f, "boss_king_dash", 1.8f, 1.2f);
                    break;
            }
        }
    }
}
