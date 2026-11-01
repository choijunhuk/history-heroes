using System.Collections;
using HistoryHeroes.Enemy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 보스전 화면 위쪽: 보스 이름 + 체력 막대 (2/3, 1/3 지점에 단계 눈금) + 시작할 때 보스 소개.
    /// (Modes.md 2-9: 체력 1/3, 2/3에서 패턴 단계 전환)
    /// </summary>
    public sealed class BossHud : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text phaseLabel;

        [Header("시작 소개")]
        [SerializeField] private CanvasGroup intro;
        [SerializeField] private TMP_Text introTitle;
        [SerializeField] private TMP_Text introBody;

        private EnemyBase _boss;
        private IPhasedBoss _warden;
        private int _phase = -1;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (intro != null) intro.alpha = 0f;
        }

        public void ShowIntro(string bossName, string tests)
        {
            if (intro == null) return;
            introTitle.text = bossName;
            introBody.text = tests;
            StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                intro.alpha = t / 0.25f;
                yield return null;
            }

            intro.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.6f);
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                intro.alpha = 1f - t / 0.4f;
                yield return null;
            }

            intro.alpha = 0f;
        }

        public void Bind(EnemyBase boss, string bossName)
        {
            // 뽕맛: 보스를 잡는 순간 느려짐 + 큰 폭발 + "격파!" (같은 적을 다시 묶어도 한 번만 걸리게)
            if (_killHooked != null) _killHooked.Killed -= OnBossKilled;
            _killHooked = boss;
            if (boss != null) boss.Killed += OnBossKilled;
            _boss = boss;
            _warden = boss as IPhasedBoss;
            _phase = -1;
            nameLabel.text = bossName;
            root.SetActive(true);
        }

        private EnemyBase _killHooked;

        private static void OnBossKilled(EnemyBase boss)
        {
            if (boss != null) HypeFx.BossDown(boss.transform.position);
        }

        private void Update()
        {
            if (_boss == null) return;

            float max = Mathf.Max(1f, _boss.Health.Max);
            fill.fillAmount = Mathf.Clamp01(_boss.Health.Current / max);

            int phase = _warden != null ? _warden.Phase : 1;
            if (phase != _phase)
            {
                _phase = phase;
                phaseLabel.SetText("{0}단계", phase);
            }

            if (_boss.Health.IsDead) root.SetActive(false);
        }
    }
}
