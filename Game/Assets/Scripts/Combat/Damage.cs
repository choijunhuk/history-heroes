using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Combat
{
    public enum Team
    {
        Player,
        Enemy,
    }

    public enum DamageKind
    {
        BasicAttack,
        Active,
        Enemy,
    }

    /// <summary>피해 한 번의 정보. 맞는 쪽이 피해 감소를 적용한다.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public bool IsCrit;
        public DamageKind Kind;
        public Team AttackerTeam;

        /// <summary>어느 스킬의 피해인가 (스킬 ID, 기본 공격 "basic", 영웅 대표 효과 "sig_영웅ID"). 스킬별 피해 비율용</summary>
        public string SourceId;

        /// <summary>날아온 방향 (투사체·베기). 방패병이 정면 공격을 막는 데 쓴다. 범위 피해는 방향이 없다</summary>
        public Vector2 Direction;
        public bool HasDirection;
        /// <summary>관통하는 투사체 (방패를 뚫는다, Content 4장 "튕김·관통")</summary>
        public bool Piercing;
        /// <summary>적의 실제 투사체 충돌인가. 근접·장판은 방패 한 발 차단을 쓰지 않는다.</summary>
        public bool IsProjectile;
    }

    /// <summary>
    /// 피해 공식 (Docs/Design/Stats.md 2장).
    /// 기본 피해 = 공격력 × 계수, 치명타면 × 치명타 피해, 액티브면 × (1 + 액티브 피해 증가).
    /// 피해 감소는 맞는 쪽 Health에서 적용한다.
    /// </summary>
    public static class DamageCalculator
    {
        /// <param name="forceCrit">치명타 확정 (서리 창: 빙결한 적)</param>
        public static DamageInfo Roll(StatSheet attacker, float coefficient, DamageKind kind, bool forceCrit = false, string source = null)
        {
            StatTarget context = kind == DamageKind.Active ? StatTarget.Active : StatTarget.BasicAttack;

            float amount = attacker.Get(StatType.Attack, context) * coefficient;
            if (kind == DamageKind.Active)
            {
                amount *= 1f + attacker.Get(StatType.ActiveDamage, context);
            }

            bool crit = forceCrit || Random.value < attacker.Get(StatType.CritRate, context);
            if (crit) amount *= attacker.Get(StatType.CritDamage, context);

            return new DamageInfo
            {
                Amount = amount,
                IsCrit = crit,
                Kind = kind,
                AttackerTeam = Team.Player,
                SourceId = source ?? (kind == DamageKind.BasicAttack ? DamageLog.BasicSource : null),
            };
        }

        /// <param name="source">무엇에 맞았나 (적 ID, 보스 패턴 — 벤치에서 "무엇에 쓰러지나"를 센다)</param>
        public static DamageInfo Enemy(float amount, string source = null)
        {
            return new DamageInfo { Amount = amount, IsCrit = false, Kind = DamageKind.Enemy, AttackerTeam = Team.Enemy, SourceId = source };
        }
    }
}
