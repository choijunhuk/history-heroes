using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// "뽕맛" 연출 (개발자: "이게 게임이다보니까 뽕맛이 있으면 좋겟어", 2026-10-27):
    /// - 보스·중간 보스 처치: 잠깐 느려짐 + 큰 폭발 + 하얀 번쩍임 + 화면 흔들림 + "격파!"
    /// - 연속 처치: 2초 안에 이어서 잡으면 "12 연속 처치!" (많을수록 색이 뜨거워진다)
    /// - 진화·합체·전설 카드: 화면 가운데 큰 글
    /// 번쩍임 줄이기 설정이면 번쩍임을 아주 약하게 한다.
    /// </summary>
    public sealed class HypeFx : MonoBehaviour
    {
        [SerializeField] private TMP_Text comboLabel;
        [SerializeField] private UiPop comboPop;
        [SerializeField] private TMP_Text bannerLabel;
        [SerializeField] private UiPop bannerPop;
        [SerializeField] private Image flash;
        [Tooltip("이만큼 이어 잡아야 연속 처치가 보인다")]
        [SerializeField] private int comboMin = 5;
        [SerializeField] private float comboWindow = 2f;

        private static HypeFx _instance;
        private int _combo;
        private float _lastKill = -10f;
        private float _bannerUntil;
        private float _flashStart = -10f;
        private float _flashAlpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private void OnEnable()
        {
            _instance = this;
            Health.AnyDamaged += OnAnyDamaged;
            if (comboLabel != null) comboLabel.gameObject.SetActive(false);
            if (bannerLabel != null) bannerLabel.gameObject.SetActive(false);
            if (flash != null) flash.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            if (_instance == this) _instance = null;
        }

        private void OnAnyDamaged(Health target, DamageInfo info, float dealt)
        {
            if (target == null || target.Team != Team.Enemy || !target.IsDead || dealt <= 0f) return;
            float now = Time.unscaledTime;
            _combo = now - _lastKill <= comboWindow ? _combo + 1 : 1;
            _lastKill = now;
            if (_combo < comboMin || comboLabel == null) return;
            comboLabel.gameObject.SetActive(true);
            comboLabel.text = $"{_combo} <size=60%>연속 처치!</size>";
            comboLabel.color = _combo >= 50 ? new Color(1f, 0.3f, 0.3f) : _combo >= 30 ? new Color(1f, 0.55f, 0.2f) : _combo >= 15 ? new Color(1f, 0.85f, 0.3f) : Color.white;
            if (comboPop != null && (_combo == comboMin || _combo % 5 == 0)) comboPop.Play(); // 5개마다 통 튀어나온다
        }

        /// <summary>보스·중간 보스를 잡은 순간</summary>
        public static void BossDown(Vector3 position)
        {
            HitStop.Slow(0.2f, 0.9f); // 잠깐 느려진다 (실제 시간 0.9초)
            CameraShake.Add(0.9f);
            Vector2 p = position;
            FxBursts.Explosion(p, 2.4f, new Color(1f, 0.8f, 0.35f));
            FxBursts.Explosion(p + new Vector2(0.8f, 0.5f), 1.4f, new Color(1f, 0.55f, 0.25f));
            FxBursts.Explosion(p + new Vector2(-0.7f, -0.4f), 1.4f, new Color(1f, 0.95f, 0.6f));
            Audio.AudioManager.Play("explode");
            if (_instance == null) return;
            _instance.Flash(0.65f);
            _instance.ShowBanner("격파!", new Color(1f, 0.82f, 0.3f), 1.6f);
        }

        /// <summary>화면 가운데 큰 글 (진화 · 합체 · 전설)</summary>
        public static void Banner(string text, Color color)
        {
            if (_instance == null) return;
            _instance.ShowBanner(text, color, 1.3f);
            _instance.Flash(0.25f);
        }

        private void ShowBanner(string text, Color color, float seconds)
        {
            if (bannerLabel == null) return;
            bannerLabel.gameObject.SetActive(true);
            bannerLabel.text = text;
            bannerLabel.color = color;
            _bannerUntil = Time.unscaledTime + seconds;
            if (bannerPop != null) bannerPop.Play();
        }

        private void Flash(float alpha)
        {
            if (flash == null) return;
            bool reduce = Records.SaveSystem.Data != null && Records.SaveSystem.Data.settings.reduceFlashes;
            _flashAlpha = reduce ? alpha * 0.2f : alpha;
            _flashStart = Time.unscaledTime;
            flash.gameObject.SetActive(true);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (comboLabel != null && comboLabel.gameObject.activeSelf && now - _lastKill > comboWindow)
            {
                comboLabel.gameObject.SetActive(false);
                _combo = 0;
            }

            if (bannerLabel != null && bannerLabel.gameObject.activeSelf)
            {
                float left = _bannerUntil - now;
                if (left <= 0f) bannerLabel.gameObject.SetActive(false);
                else
                {
                    Color c = bannerLabel.color;
                    c.a = Mathf.Clamp01(left / 0.35f); // 마지막 0.35초에 사라진다
                    bannerLabel.color = c;
                }
            }

            if (flash != null && flash.gameObject.activeSelf)
            {
                float t = (now - _flashStart) / 0.35f;
                if (t >= 1f) flash.gameObject.SetActive(false);
                else flash.color = new Color(1f, 1f, 0.92f, _flashAlpha * (1f - t) * (1f - t));
            }
        }
    }
}
