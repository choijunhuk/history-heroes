using System;
using UnityEngine;

namespace HistoryHeroes.Progress
{
    /// <summary>
    /// 판 안 레벨 (Docs/Design/Content.md 2-3). 필요 경험치 = 기본 × 레벨^1.3 → 초반엔 빠르고 후반엔 느리다.
    /// 기본값 1.5면 한 판(적 약 120마리 + 엘리트) 동안 약 15번 레벨업한다.
    /// </summary>
    public sealed class PlayerLevel
    {
        public const float DefaultBaseRequirement = 1.5f;
        public const float Exponent = 1.3f;

        private readonly float _baseRequirement;

        public int Level { get; private set; } = 1;
        public float Experience { get; private set; }
        public float Required => _baseRequirement * Mathf.Pow(Level, Exponent);
        public float Progress01 => Mathf.Clamp01(Experience / Required);

        /// <summary>레벨이 오를 때마다 한 번씩 (한 번에 여러 레벨이 올라도 각각) 알린다.</summary>
        public event Action<int> LeveledUp;

        public PlayerLevel(float baseRequirement = DefaultBaseRequirement)
        {
            _baseRequirement = baseRequirement;
        }

        /// <param name="expGainBonus">판 안 "배움" 패시브만 준다. 판 밖 출처는 0 (Stats.md 4-1).</param>
        public void Add(float amount, float expGainBonus = 0f)
        {
            if (amount <= 0f) return;
            Experience += amount * (1f + expGainBonus);

            while (Experience >= Required)
            {
                Experience -= Required;
                Level++;
                LeveledUp?.Invoke(Level);
            }
        }
    }
}
