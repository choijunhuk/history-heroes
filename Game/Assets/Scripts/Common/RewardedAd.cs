using System;
using System.Collections;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 보상형 광고 (리롤 등). 진짜 광고 SDK(LevelPlay 또는 AdMob)는 v1.0에 붙인다 (GameDesign 11장).
    /// 그 전까지는 가짜 광고를 쓴다. 실패·취소면 false → 횟수를 차감하지 않는다 (GameDesign 6-7).
    /// </summary>
    public interface IRewardedAd
    {
        void Show(string placement, Action<bool> onFinished);
    }

    /// <summary>게임 전체가 쓰는 광고 하나. 진짜 SDK가 붙으면 여기만 바꾼다.</summary>
    public static class Ads
    {
        public static IRewardedAd Current { get; set; } = new FakeRewardedAd();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = new FakeRewardedAd();
            FakeRewardedAd.Mode = FakeAdMode.Success;
        }
    }

    public enum FakeAdMode
    {
        /// <summary>바로 성공</summary>
        Success,
        /// <summary>광고를 못 불러옴</summary>
        Fail,
        /// <summary>보다가 닫음</summary>
        Cancel,
        /// <summary>2초 뒤 성공 (기다리는 동안 버튼을 막는지 확인)</summary>
        Delay,
    }

    /// <summary>
    /// 가짜 광고 (GameDesign 12-2 "광고": 성공 / 실패 / 취소 / 지연 모드).
    /// 개발용 단축키 A로 모드를 바꾼다 (DevCheats).
    /// </summary>
    public sealed class FakeRewardedAd : IRewardedAd
    {
        public const float DelaySeconds = 2f;

        public static FakeAdMode Mode { get; set; } = FakeAdMode.Success;

        public void Show(string placement, Action<bool> onFinished)
        {
            switch (Mode)
            {
                case FakeAdMode.Success:
                    onFinished?.Invoke(true);
                    break;
                case FakeAdMode.Fail:
                case FakeAdMode.Cancel:
                    onFinished?.Invoke(false);
                    break;
                case FakeAdMode.Delay:
                    AdRunner.Instance.StartCoroutine(After(DelaySeconds, () => onFinished?.Invoke(true)));
                    break;
            }
        }

        private static IEnumerator After(float seconds, Action action)
        {
            // 레벨업 화면은 게임을 멈춘 채로 광고를 기다리므로 실제 시간으로 잰다
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        /// <summary>코루틴을 돌릴 숨은 오브젝트 (장면이 바뀌어도 남음)</summary>
        private sealed class AdRunner : MonoBehaviour
        {
            private static AdRunner _instance;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ResetStatics() => _instance = null;

            public static AdRunner Instance
            {
                get
                {
                    if (_instance != null) return _instance;
                    var go = new GameObject("FakeAdRunner") { hideFlags = HideFlags.HideInHierarchy };
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AdRunner>();
                    return _instance;
                }
            }
        }
    }
}
