using UnityEngine;

namespace HistoryHeroes.Stats
{
    /// <summary>능력치 상한·하한. Docs/Design/Stats.md 1장 "상한" 열과 같다.</summary>
    public static class StatCaps
    {
        public static float Clamp(StatType stat, float value)
        {
            switch (stat)
            {
                case StatType.AttackSpeed: return Mathf.Clamp(value, 0.1f, 3.0f);
                case StatType.MoveSpeed: return Mathf.Clamp(value, 0.5f, 8.0f);
                case StatType.CritRate: return Mathf.Clamp(value, 0f, 0.6f);
                case StatType.CritDamage: return Mathf.Clamp(value, 1f, 3.0f);
                case StatType.DamageReduction: return Mathf.Clamp(value, 0f, 0.6f);
                case StatType.Dodge: return Mathf.Clamp(value, 0f, 0.25f);
                case StatType.CooldownReduction: return Mathf.Clamp(value, 0f, 0.4f);
                case StatType.ExpGain: return Mathf.Clamp(value, 0f, 0.5f);
                case StatType.GoldGain: return Mathf.Clamp(value, 0f, 0.5f);
                default: return Mathf.Max(0f, value);
            }
        }
    }
}
