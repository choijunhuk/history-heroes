using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// "뽕맛" 연출 (개발자: "이게 게임이다보니까 뽕맛이 있으면 좋겟어", 2026-10-27):
    /// - 보스·중간 보스 처치: 잠깐 느려짐 + 큰 폭발 + 하얀 번쩍임 + 화면 흔들림 + "격파!"
    /// - 연속 처치: 2초 안에 이어서 잡으면 "12 연속 처치!" (많을수록 색이 뜨거워진다)
    /// - 진화·합체·전설 카드: 화면 가운데 큰 글
    /// 번쩍임 줄이기 설정이면 번쩍임을 아주 약하게 한다.
    ///
    /// 클리어 연출 (개발자 실기 피드백 2026-11-01: "끝냈을때 이펙트도 없고 텍스트로만 나와서 클리어한 맛이 없다"):
    /// Clear = 슬로모션 + 하얀 번쩍임 + 살짝 당겨 보기 + "클리어!" 띠 (통 튀어나옴) + 색종이. Defeat = 회색 막이 천천히 덮는다.
    /// 모두 실제 시간(unscaled)이라 멈춘 화면에서도 돈다. 화면을 아무 데나 새로 누르면 건너뛴다 (Beat가 바로 끝난다).
    /// 번쩍임 줄이기가 켜져 있으면 하얀 번쩍임 · 화면 흔들림은 뺀다 (느려짐 · 당겨 보기는 그대로).
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

        /// <summary>클리어 연출 길이 (실제 초). 결과 화면은 이 뒤에 연다</summary>
        public const float ClearSeconds = 1.2f;
        /// <summary>패배 연출 길이 (실제 초)</summary>
        public const float DefeatSeconds = 0.9f;
        private const int BitCount = 56;
        private const float PushInAmount = 0.07f;

        private static HypeFx _instance;
        private int _combo;
        private float _lastKill = -10f;
        private float _bannerUntil;
        private float _flashStart = -10f;
        private float _flashAlpha;

        // 클리어 · 패배 연출
        private RectTransform _ribbon;
        private TMP_Text _ribbonText;
        private RectTransform[] _bits;
        private Image[] _bitImages;
        private Vector2[] _bitVel;
        private float[] _bitSpin;
        private float[] _bitDelay;
        private Image _dim;
        private CameraFit _fit;
        private float _clearStart = -10f;
        private float _defeatStart = -10f;
        private bool _clearOn;
        private bool _bitsOn;
        private bool _defeatOn;
        private bool _skipped;

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
            if (_fit != null) _fit.PushIn = 0f;
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

        /// <summary>
        /// 판을 이긴 순간 (던전 클리어 · 보스 격파 · 서바이벌 생존). boss=true면 BossDown이 이미 느려짐 · 번쩍임 · 소리를 냈으니 띠 · 색종이 · 당겨 보기만 더한다.
        /// </summary>
        public static void Clear(string text, bool boss = false)
        {
            if (_instance != null) _instance.StartClear(text, boss);
        }

        /// <summary>판에서 진 순간: 회색 막이 천천히 덮는다 (색종이 없음). 결과 화면이 열리면 HideDim으로 걷는다</summary>
        public static void Defeat()
        {
            if (_instance != null) _instance.StartDefeat();
        }

        /// <summary>결과 화면이 열릴 때: 패배 회색 막을 걷는다 (결과 판이 따로 어둡다)</summary>
        public static void HideDim()
        {
            if (_instance != null && _instance._dim != null) _instance._dim.gameObject.SetActive(false);
        }

        /// <summary>
        /// 결과를 열기 전에 기다리는 박자. seconds가 지나거나, 연출 중에 화면을 새로 눌러 건너뛰면 끝난다.
        /// 연출이 없는 장면(HypeFx 없음)에서도 seconds만큼은 기다린다.
        /// </summary>
        public static IEnumerator Beat(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            if (_instance != null) _instance._skipped = false;
            while (Time.unscaledTime < end && !(_instance != null && _instance._skipped)) yield return null;
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
            if (bannerLabel == null || _clearOn) return; // 클리어 띠가 떠 있으면 작은 글은 양보
            bannerLabel.gameObject.SetActive(true);
            bannerLabel.text = text;
            bannerLabel.color = color;
            _bannerUntil = Time.unscaledTime + seconds;
            if (bannerPop != null) bannerPop.Play();
        }

        private static bool ReduceFlashes => Records.SaveSystem.Data != null && Records.SaveSystem.Data.settings.reduceFlashes;

        private void Flash(float alpha)
        {
            if (flash == null) return;
            bool reduce = ReduceFlashes;
            _flashAlpha = reduce ? alpha * 0.2f : alpha;
            _flashStart = Time.unscaledTime;
            flash.gameObject.SetActive(true);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            UpdateEnding(now);
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

        // ---------------------------------------------------------------- 클리어 · 패배 연출

        private void StartClear(string text, bool boss)
        {
            EnsureEndingUi();
            float now = Time.unscaledTime;
            _clearOn = true;
            _skipped = false;
            _clearStart = now;
            if (bannerLabel != null) bannerLabel.gameObject.SetActive(false); // BossDown의 "격파!"는 큰 띠로 바뀐다

            _ribbonText.text = text;
            _ribbon.gameObject.SetActive(true);
            _ribbon.localScale = Vector3.one * 0.3f;
            if (_fit == null) _fit = FindAnyObjectByType<CameraFit>();

            LaunchBits();
            if (boss) return;
            Audio.AudioManager.Play("victory");
            Audio.Haptics.Long();
            HitStop.Slow(0.25f, 0.6f); // 0.6초(실제 시간) 느리게, 이어서 UpdateEnding이 1배로 부드럽게 돌려 놓는다
            if (ReduceFlashes) return;
            Flash(0.7f);
            CameraShake.Add(0.3f);
        }

        private void StartDefeat()
        {
            EnsureEndingUi();
            _defeatOn = true;
            _skipped = false;
            _defeatStart = Time.unscaledTime;
            _dim.gameObject.SetActive(true);
            _dim.color = new Color(0.34f, 0.36f, 0.42f, 0f);
        }

        /// <summary>화면을 새로 눌렀다: 연출을 바로 끝 상태로</summary>
        private void SkipEnding()
        {
            _skipped = true;
            _clearOn = false;
            _defeatOn = false;
            _bitsOn = false;
            if (_ribbon != null) _ribbon.gameObject.SetActive(false);
            foreach (RectTransform bit in _bits ?? System.Array.Empty<RectTransform>()) bit.gameObject.SetActive(false);
            if (flash != null) flash.gameObject.SetActive(false);
            if (_dim != null && _dim.gameObject.activeSelf) _dim.color = new Color(0.34f, 0.36f, 0.42f, 0.5f);
            if (_fit != null) _fit.PushIn = 0f;
            HitStop.Slow(1f, 0.01f); // 느려짐을 바로 거둔다 (같은 요청을 1배로 덮어쓴다)
        }

        private void UpdateEnding(float now)
        {
            if (!_clearOn && !_bitsOn && !_defeatOn) return;
            bool pressed = Pointer.current != null && Pointer.current.press.wasPressedThisFrame; // 누르고 있던 조이스틱은 안 센다
            float since = now - Mathf.Max(_clearStart, _defeatStart);
            if ((_clearOn || _defeatOn) && pressed && since > 0.2f)
            {
                SkipEnding();
                return;
            }

            if (_clearOn)
            {
                float t = now - _clearStart;
                float pop = Mathf.Clamp01(t / 0.45f);
                _ribbon.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, UiPop.EaseOutBack(pop));
                float end = ClearSeconds - t;
                _ribbonText.alpha = Mathf.Clamp01(end / 0.2f);
                _ribbon.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(Mathf.Min(t / 0.1f, end / 0.2f));
                if (t > 0.6f && t < 0.9f) HitStop.Slow(Mathf.Lerp(0.25f, 1f, (t - 0.6f) / 0.3f), 0.1f); // 느려짐에서 부드럽게 돌아온다
                if (_fit != null) _fit.PushIn = PushInAmount * Mathf.Min(UiPop.EaseOutBack(Mathf.Clamp01(t / 0.5f)), 1f) * Mathf.Clamp01(end / 0.3f);
                if (t >= ClearSeconds)
                {
                    _clearOn = false;
                    _ribbon.gameObject.SetActive(false);
                    if (_fit != null) _fit.PushIn = 0f;
                }
            }

            if (_bitsOn) UpdateBits(now - _clearStart);

            if (_defeatOn)
            {
                float u = Mathf.Clamp01((now - _defeatStart) / DefeatSeconds);
                _dim.color = new Color(0.34f, 0.36f, 0.42f, 0.5f * u * u * (3f - 2f * u));
                if (u >= 1f) _defeatOn = false;
            }
        }

        private void LaunchBits()
        {
            for (int i = 0; i < BitCount; i++)
            {
                _bitDelay[i] = Random.Range(0f, 0.5f);
                _bitVel[i] = new Vector2(Random.Range(-160f, 160f), Random.Range(-1500f, -800f)); // 위에서 쏟아진다 (UI 픽셀/초)
                _bitSpin[i] = Random.Range(-540f, 540f);
                _bitImages[i].color = Color.HSVToRGB(Random.value, 0.65f, 1f);
                _bits[i].gameObject.SetActive(false);
            }

            _bitsOn = true;
        }

        private void UpdateBits(float t)
        {
            Rect area = ((RectTransform)transform).rect;
            bool any = false;
            for (int i = 0; i < BitCount; i++)
            {
                float life = t - _bitDelay[i];
                if (life < 0f)
                {
                    any = true;
                    continue;
                }

                // 시작 위치는 index로 정해 둔다 (가로로 고르게, 화면 위 바깥)
                float x = area.xMin + area.width * ((i * 0.618f) % 1f) + _bitVel[i].x * life + Mathf.Sin(life * 6f + i) * 30f;
                float y = area.yMax + 40f + _bitVel[i].y * life;
                bool alive = y > area.yMin - 60f;
                _bits[i].gameObject.SetActive(alive);
                if (!alive) continue;
                any = true;
                _bits[i].anchoredPosition = new Vector2(x - area.center.x, y - area.center.y);
                _bits[i].localRotation = Quaternion.Euler(0f, 0f, _bitSpin[i] * life);
            }

            if (!any) _bitsOn = false;
        }

        /// <summary>띠 · 색종이 · 회색 막을 처음 쓸 때 코드로 만든다 (장면 빌더를 건드리지 않게 — 글꼴은 기존 큰 글에서 빌려 온다)</summary>
        private void EnsureEndingUi()
        {
            if (_ribbon != null) return;
            Transform parent = transform;

            _dim = NewImage("DefeatDim", parent, new Color(0.34f, 0.36f, 0.42f, 0f));
            Stretch((RectTransform)_dim.transform);
            _dim.transform.SetAsFirstSibling(); // 다른 연출 글 아래
            _dim.gameObject.SetActive(false);

            Image band = NewImage("ClearRibbon", parent, new Color(0.72f, 0.12f, 0.2f, 0.94f));
            _ribbon = (RectTransform)band.transform;
            _ribbon.anchorMin = new Vector2(0f, 0.5f);
            _ribbon.anchorMax = new Vector2(1f, 0.5f);
            _ribbon.pivot = new Vector2(0.5f, 0.5f);
            _ribbon.anchoredPosition = new Vector2(0f, 260f);
            _ribbon.sizeDelta = new Vector2(0f, 250f);
            band.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            // 위 · 아래 금빛 줄
            for (int k = 0; k < 2; k++)
            {
                Image line = NewImage("Line", _ribbon, new Color(1f, 0.82f, 0.3f, 1f));
                var lr = (RectTransform)line.transform;
                lr.anchorMin = new Vector2(0f, k == 0 ? 1f : 0f);
                lr.anchorMax = new Vector2(1f, k == 0 ? 1f : 0f);
                lr.pivot = new Vector2(0.5f, k == 0 ? 1f : 0f);
                lr.anchoredPosition = Vector2.zero;
                lr.sizeDelta = new Vector2(0f, 12f);
            }

            if (bannerLabel != null)
            {
                _ribbonText = Instantiate(bannerLabel, _ribbon);
                _ribbonText.gameObject.name = "RibbonText";
                UiPop clonedPop = _ribbonText.GetComponent<UiPop>();
                if (clonedPop != null) Destroy(clonedPop);
                _ribbonText.gameObject.SetActive(true);
                _ribbonText.color = new Color(1f, 0.95f, 0.7f);
                _ribbonText.rectTransform.anchoredPosition = Vector2.zero;
            }
            else
            {
                var go = new GameObject("RibbonText", typeof(RectTransform));
                go.transform.SetParent(_ribbon, false);
                _ribbonText = go.AddComponent<TextMeshProUGUI>();
                _ribbonText.alignment = TextAlignmentOptions.Center;
                _ribbonText.fontSize = 150f;
            }

            _ribbon.gameObject.SetActive(false);

            _bits = new RectTransform[BitCount];
            _bitImages = new Image[BitCount];
            _bitVel = new Vector2[BitCount];
            _bitSpin = new float[BitCount];
            _bitDelay = new float[BitCount];
            for (int i = 0; i < BitCount; i++)
            {
                _bitImages[i] = NewImage("Confetti", parent, Color.white);
                _bits[i] = (RectTransform)_bitImages[i].transform;
                _bits[i].anchorMin = _bits[i].anchorMax = new Vector2(0.5f, 0.5f);
                _bits[i].sizeDelta = new Vector2(Random.Range(14f, 24f), Random.Range(26f, 44f));
                _bits[i].gameObject.SetActive(false);
            }
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false; // 터치를 막지 않는다
            return image;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
