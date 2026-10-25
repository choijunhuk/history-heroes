using System;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 보상형 광고 (리롤 등). 실제 광고 SDK(LevelPlay)는 v0.4에 붙인다 (Monetization.md).
    /// 그 전까지는 바로 성공하는 가짜를 쓴다. 실패·취소면 false → 횟수를 차감하지 않는다 (GameDesign 6-7).
    /// </summary>
    public interface IRewardedAd
    {
        void Show(string placement, Action<bool> onFinished);
    }

    public sealed class InstantRewardedAd : IRewardedAd
    {
        public void Show(string placement, Action<bool> onFinished) => onFinished?.Invoke(true);
    }
}
