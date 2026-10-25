using UnityEngine;

namespace HistoryHeroes.Records
{
    /// <summary>
    /// 기록 용량 (GameDesign 7-2 "용량은 고정값이 아니라 비율로").
    /// 용량 = 기준 비용 × 0.8 × (깬 방 ÷ 전체 방). 클리어하면 비율 1.
    /// 기준 비용 34pt = 대표 완성 빌드(레벨업 15회)의 총비용 (임시 확정, v0.2 플레이 기록으로 다시 맞춘다).
    /// </summary>
    public static class RecordCapacity
    {
        public const int ReferenceBuildCost = 34;
        public const float BaseRatio = 0.8f;

        /// <param name="roomsCleared">깬 방 수 (죽은 방은 빼고)</param>
        /// <param name="bonusRatio">판 밖 성장으로 늘어나는 비율 (예: +0.1 → 90%). v0.4 이후</param>
        public static int For(int roomsCleared, int roomCount, bool cleared, float bonusRatio = 0f)
        {
            float progress = cleared ? 1f : roomCount <= 0 ? 0f : Mathf.Clamp01((float)roomsCleared / roomCount);
            return Mathf.FloorToInt(ReferenceBuildCost * (BaseRatio + bonusRatio) * progress + 0.0001f);
        }
    }
}
