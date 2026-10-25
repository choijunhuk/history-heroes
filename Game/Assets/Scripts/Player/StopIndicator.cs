using UnityEngine;

namespace HistoryHeroes.Player
{
    /// <summary>
    /// 멈추면 발밑에 원이 차오르고, 다 차면 기본 공격이 나간다는 걸 눈으로 보여준다. (UX.md 2장)
    /// 차오르는 시간 = 멈춤 판정 0.1초 + 영웅의 첫 공격 준비 시간 (칭기즈 칸은 0이라 거의 바로 참).
    /// </summary>
    public sealed class StopIndicator : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private float fullSize = 1.3f;
        [SerializeField] private Color chargingColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color readyColor = new Color(1f, 0.85f, 0.3f, 0.35f);

        private void LateUpdate()
        {
            if (player == null || fill == null) return;

            bool show = player.IsStopped && !player.Health.IsDead;
            fill.enabled = show;
            if (!show) return;

            float chargeTime = StopDetector.StopThreshold + player.Hero.firstShotDelay;
            float k = Mathf.Clamp01((player.Stop.StoppedTime + StopDetector.StopThreshold) / Mathf.Max(0.01f, chargeTime));
            fill.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, fullSize, k);
            fill.color = k >= 1f ? readyColor : chargingColor;
        }
    }
}
