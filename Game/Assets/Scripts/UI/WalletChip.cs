using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 위 띠의 골드 · 보석 칩. 값이 바뀌면 숫자가 올라가며 바뀌고 칩이 살짝 튄다.
    /// 보상 연출(RewardPopup)이 아이콘을 날려 보낼 곳 = 지금 보이는 칩 (Target).
    /// drive가 꺼져 있으면 숫자는 다른 스크립트가 쓰고 (로비 위 띠) 여기는 날아올 곳 · 튀기만 맡는다.
    /// </summary>
    public sealed class WalletChip : MonoBehaviour
    {
        [Tooltip("gold · gem")]
        [SerializeField] private string kind = "gold";
        [SerializeField] private TMP_Text label;
        [Tooltip("숫자를 여기서 쓴다 (서브 화면 위 띠). 로비 위 띠는 LobbyScreen이 쓴다")]
        [SerializeField] private bool drive = true;

        private static readonly List<WalletChip> Active = new List<WalletChip>();
        private int _shown = int.MinValue;
        private Coroutine _count, _bump;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        /// <summary>날아갈 곳: 화면에 보이는 그 종류 칩 중 가장 나중에 켜진 것 (창 위 띠가 로비 띠보다 앞)</summary>
        public static RectTransform Target(string kind)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                WalletChip c = Active[i];
                if (c != null && c.kind == kind && c.isActiveAndEnabled) return (RectTransform)c.transform;
            }

            return null;
        }

        private void OnEnable()
        {
            Active.Remove(this);
            Active.Add(this);
            if (!drive || label == null) return;
            _shown = Value();
            label.text = Wallet.Format(_shown);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            transform.localScale = Vector3.one; // 튀는 도중 창이 닫혀도 크기가 남지 않게
        }

        private int Value()
        {
            SaveData d = SaveSystem.Data;
            if (d == null) return 0;
            return kind == "gem" ? Wallet.Gems(d) : d.gold;
        }

        private void Update()
        {
            if (!drive || label == null) return;
            int now = Value();
            if (now == _shown) return;
            if (_count != null) StopCoroutine(_count);
            _count = StartCoroutine(CountTo(_shown, now));
            _shown = now;
        }

        /// <summary>0.7초 동안 숫자가 올라간다 (줄어들 땐 바로)</summary>
        private IEnumerator CountTo(int from, int to)
        {
            if (to < from)
            {
                label.text = Wallet.Format(to);
                yield break;
            }

            const float duration = 0.7f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - (1f - t / duration) * (1f - t / duration);
                label.text = Wallet.Format(Mathf.RoundToInt(Mathf.Lerp(from, to, k)));
                yield return null;
            }

            label.text = Wallet.Format(to);
        }

        /// <summary>날아온 아이콘이 닿을 때: 1 → 1.15 → 1 (0.18초)</summary>
        public void Bump()
        {
            if (!isActiveAndEnabled) return;
            if (_bump != null) StopCoroutine(_bump);
            _bump = StartCoroutine(BumpRoutine());
        }

        private IEnumerator BumpRoutine()
        {
            const float duration = 0.18f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                transform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t / duration * Mathf.PI));
                yield return null;
            }

            transform.localScale = Vector3.one;
        }
    }
}
