using System.Collections;
using UnityEngine;

namespace HistoryHeroes.Enemy
{
    /// <summary>
    /// 4구간 중간 보스 🌑 그림자 사냥꾼 (Modes 2-8): 분신 2개를 부르고 자리를 바꾼다. 진짜만 발밑에 그림자가 있다 → 진짜를 찾아 쏜다.
    /// 공격: 단검 부채꼴, 돌진. 분신은 약하지만 같이 공격한다. 2단계: 분신 3개.
    /// </summary>
    public sealed class ShadowHunterBoss : PatternBoss
    {
        [SerializeField] private EnemyBase clonePrefab;
        [SerializeField] private float cloneHp = 0.12f;
        [SerializeField] private float knifeSpeed = 6.5f;
        [SerializeField] private float knifeDamage = 0.8f;
        [SerializeField] private SpriteRenderer shadow;

        private int _step;

        protected override void OnTick()
        {
            if (shadow != null) shadow.enabled = true; // 진짜 표시
        }

        protected override IEnumerator Cycle()
        {
            _step++;
            if (_step % 3 == 1)
            {
                // 분신: 마법진 → 분신이 나오면서 진짜는 다른 자리로
                int clones = phase >= 2 ? 3 : 2;
                yield return Summon(clonePrefab, clones, 2.2f, 0.9f * Tele, cloneHp);
                Blink(PlayerPos + Random.insideUnitCircle.normalized * 3f);
                yield return Open(0.6f);
                yield break;
            }

            if (_step % 3 == 2)
            {
                yield return Glow(0.6f * Tele, new Color(0.5f, 0.3f, 0.8f, 0.5f));
                Fan(phase >= 2 ? 7 : 5, 70f, knifeSpeed, knifeDamage, "mid_hunter_knife");
                yield return Open(1.3f);
                yield break;
            }

            yield return Dash(0.7f * Tele, 10f, 0.9f, 1.2f, "mid_hunter_dash", 1.6f, 1f);
        }
    }
}
