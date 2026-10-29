using System.Collections;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 3구간 보스 ⚙️ 강철 거상 (Content 3장: "탄막 → 칼날 탱커형, 회피"). 느리게 걷고 탄을 쏟아붓는다.
    /// - 회전 탄막: 팔 3개(2단계 4개)가 돌며 2초 동안 쏜다
    /// - 개틀링: 플레이어 쪽 3발 부채꼴을 연달아 (3단계: 5발)
    /// - 고리 폭발: 사방 16발 + (2단계부터) 플레이어 자리 포격 원
    /// 탄막 뒤마다 빈틈. 투사체를 막는 칼날(강철 회오리)·방어력 기록이 유리하다.
    /// </summary>
    public sealed class SteelColossusBoss : PatternBoss
    {
        [SerializeField] private float bulletSpeed = 4.2f;
        [SerializeField] private float bulletDamage = 0.55f;
        [SerializeField] private float barrageRecover = 1.6f;
        [SerializeField] private bool lightningBarrage;

        public bool LightningBarrage => lightningBarrage;

        private int _step;

        protected override IEnumerator Cycle()
        {
            _step++;
            switch (_step % 3)
            {
                case 1:
                {
                    yield return Glow(0.8f * Tele, new Color(1f, 0.7f, 0.3f, 0.45f));
                    Enter(State.Attack);
                    int arms = phase >= 2 ? 4 : 3;
                    float spin = 0f;
                    float end = bossTime + 2f;
                    while (bossTime < end)
                    {
                        for (int a = 0; a < arms; a++)
                        {
                            float angle = (spin + a * 360f / arms) * Mathf.Deg2Rad;
                            var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                            EnemyProjectiles.Fire(Pos + d * 1f, d, bulletSpeed, Combat.DamageCalculator.Enemy(Damage * bulletDamage, "boss_colossus_spiral"), playerMask, this);
                        }

                        spin += phase >= 3 ? 19f : 15f;
                        yield return Wait(0.12f);
                    }

                    yield return Open(1.8f);
                    break;
                }

                case 2:
                {
                    yield return Glow(0.5f * Tele, new Color(1f, 0.85f, 0.4f, 0.4f));
                    Enter(State.Attack);
                    int bursts = 5 + phase;
                    for (int i = 0; i < bursts; i++)
                    {
                        Fan(phase >= 3 ? 5 : 3, 30f, bulletSpeed * 1.4f, bulletDamage, "boss_colossus_gatling");
                        yield return Wait(0.22f);
                    }

                    yield return Open(barrageRecover);
                    break;
                }

                default:
                    yield return Glow(0.9f * Tele, new Color(1f, 0.5f, 0.2f, 0.45f));
                    Ring(16, bulletSpeed * 0.9f, bulletDamage, "boss_colossus_ring");
                    if (phase >= 2) yield return CircleStrike(PlayerPos, 1.5f, 1f * Tele, 1.3f, "boss_colossus_shell");
                    yield return Open(barrageRecover);
                    break;
            }
        }
    }
}
