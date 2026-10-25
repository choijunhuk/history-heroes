using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 한 판 동안 플레이어가 적에게 준 피해를 종류별로 모은다.
    /// "기본 공격 피해 비중 40% 이상"(GameDesign 3-3 멈춤의 가치)을 숫자로 확인하려고 쓴다.
    /// </summary>
    public static class DamageLog
    {
        public static float BasicAttack { get; private set; }
        public static float Active { get; private set; }
        public static float Total => BasicAttack + Active;

        /// <summary>기본 공격이 차지한 비율 (0~1). 아직 피해가 없으면 0.</summary>
        public static float BasicShare => Total > 0f ? BasicAttack / Total : 0f;

        public static void Reset()
        {
            BasicAttack = 0f;
            Active = 0f;
        }

        /// <summary>Health가 실제로 깎인 양으로 부른다 (넘친 피해는 세지 않음).</summary>
        public static void Record(DamageKind kind, float dealt)
        {
            if (dealt <= 0f) return;
            if (kind == DamageKind.BasicAttack) BasicAttack += dealt;
            else if (kind == DamageKind.Active) Active += dealt;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
