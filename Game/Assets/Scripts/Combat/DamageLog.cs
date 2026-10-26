using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 한 판 동안 플레이어가 적에게 준 피해를 종류별로 모은다.
    /// "기본 공격 피해 비중 40% 이상"(GameDesign 3-3 멈춤의 가치)을 숫자로 확인하려고 쓴다.
    /// </summary>
    public static class DamageLog
    {
        public const string BasicSource = "basic";

        private static readonly Dictionary<string, float> _bySource = new Dictionary<string, float>();

        /// <summary>출처(스킬 ID 등)별로 준 피해 (조사 A5: 결과 화면·기록 저장 화면)</summary>
        public static IReadOnlyDictionary<string, float> BySource => _bySource;

        public static float BasicAttack { get; private set; }
        public static float Active { get; private set; }
        public static float Total => BasicAttack + Active;

        /// <summary>기본 공격이 차지한 비율 (0~1). 아직 피해가 없으면 0.</summary>
        public static float BasicShare => Total > 0f ? BasicAttack / Total : 0f;

        public static void Reset()
        {
            BasicAttack = 0f;
            Active = 0f;
            _bySource.Clear();
        }

        /// <summary>출처별 비율 (0~1), 큰 것부터</summary>
        public static List<KeyValuePair<string, float>> Shares()
        {
            var list = new List<KeyValuePair<string, float>>(_bySource.Count);
            float total = Total;
            if (total <= 0f) return list;
            foreach (KeyValuePair<string, float> pair in _bySource) list.Add(new KeyValuePair<string, float>(pair.Key, pair.Value / total));
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }

        public static float ShareOf(string sourceId)
        {
            float total = Total;
            return total > 0f && sourceId != null && _bySource.TryGetValue(sourceId, out float v) ? v / total : 0f;
        }

        /// <summary>Health가 실제로 깎인 양으로 부른다 (넘친 피해는 세지 않음).</summary>
        public static void Record(DamageInfo info, float dealt)
        {
            if (dealt <= 0f) return;
            if (info.Kind == DamageKind.BasicAttack) BasicAttack += dealt;
            else if (info.Kind == DamageKind.Active) Active += dealt;
            else return;

            string source = info.SourceId ?? (info.Kind == DamageKind.BasicAttack ? BasicSource : "active");
            _bySource.TryGetValue(source, out float sum);
            _bySource[source] = sum + dealt;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
