using System.Collections.Generic;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 적이 받은 피해 숫자 (조사 A9, UX 6장 손맛 규칙).
    /// - 같은 적에게 0.15초 안에 들어온 피해는 숫자 하나로 합친다 (기본 공격이 초당 여러 발이라)
    /// - 기본 공격 흰색, 액티브 하늘색(멈춰서 쏜 피해가 눈에 띄게), 치명타 노란색 1.4배
    /// - 동시에 최대 40개 (넘으면 가장 오래된 것을 다시 쓴다), 0.6초 동안 위로 뜨며 사라짐
    /// 오브젝트는 미리 만들어 두고 다시 쓴다 (GameDesign 8-5). 설정에서 끌 수 있다.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, Min(1)] private int maxActive = 40;
        [SerializeField] private float mergeWindow = 0.15f;
        [SerializeField] private float lifetime = 0.6f;
        [Tooltip("사라질 때까지 위로 뜨는 거리 (유닛)")]
        [SerializeField] private float rise = 0.9f;
        [Tooltip("글자 크기 (TextMeshPro 월드 글자)")]
        [SerializeField] private float fontSize = 5f;
        [SerializeField] private float critScale = 1.4f;
        [SerializeField] private int sortingOrder = 60;

        public static readonly Color BasicColor = Color.white;
        public static readonly Color ActiveColor = new Color(0.62f, 0.85f, 1f);
        public static readonly Color CritColor = new Color(1f, 0.84f, 0.25f);

        private sealed class Entry
        {
            public TextMeshPro Text;
            public Health Target;
            public float Born;
            public float LastHit;
            public float Amount;
            public bool Crit;
            public Color Color;
            public Vector3 Start;
        }

        private readonly List<Entry> _active = new List<Entry>();
        private readonly Stack<TextMeshPro> _free = new Stack<TextMeshPro>();

        /// <summary>지금 떠 있는 숫자 수 (QA용)</summary>
        public int ActiveCount => _active.Count;

        private void OnEnable() => Health.AnyDamaged += OnAnyDamaged;

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            for (int i = _active.Count - 1; i >= 0; i--) Recycle(i);
        }

        private void OnAnyDamaged(Health target, DamageInfo info, float dealt)
        {
            if (target.Team != Team.Enemy || dealt <= 0f || !SaveSystem.Data.settings.damageNumbers) return;
            float now = Time.unscaledTime;

            foreach (Entry e in _active)
            {
                if (e.Target != target || now - e.LastHit > mergeWindow) continue;
                e.Amount += dealt;
                e.LastHit = now;
                e.Born = now; // 합쳐지면 다시 처음부터 떠오른다
                e.Crit |= info.IsCrit;
                if (target.IsDead) e.Target = null; // 다시 나온 적이 이 숫자에 합쳐지지 않게
                Refresh(e);
                return;
            }

            if (_active.Count >= maxActive) Recycle(0);

            var entry = new Entry
            {
                Text = Take(),
                Target = target,
                Born = now,
                LastHit = now,
                Amount = dealt,
                Crit = info.IsCrit,
                Color = info.Kind == DamageKind.Active ? ActiveColor : BasicColor,
                Start = target.transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0.6f, 0f),
            };
            if (target.IsDead) entry.Target = null; // 처치 피해는 보여주되, 같은 오브젝트가 풀에서 다시 나오면 새 숫자로
            _active.Add(entry);
            Refresh(entry);
        }

        private void Refresh(Entry e)
        {
            e.Text.text = Format(e.Amount);
            e.Text.fontSize = fontSize * (e.Crit ? critScale : 1f);
            e.Text.color = e.Crit ? CritColor : e.Color;
            e.Text.transform.position = e.Start;
        }

        /// <summary>정수로. 1만이 넘으면 "1.2만"</summary>
        public static string Format(float amount)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(amount));
            if (n < 10000) return n.ToString();
            return (n / 10000f).ToString(n < 100000 ? "0.0" : "0") + "만";
        }

        private void Update()
        {
            float now = Time.unscaledTime; // 멈춘 동안(레벨업 창)에도 마저 사라지게
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Entry e = _active[i];
                float t = (now - e.Born) / lifetime;
                if (t >= 1f)
                {
                    Recycle(i);
                    continue;
                }

                float ease = 1f - (1f - t) * (1f - t);
                e.Text.transform.position = e.Start + Vector3.up * (rise * ease);
                Color c = e.Text.color;
                c.a = 1f - t * t;
                e.Text.color = c;
            }
        }

        private TextMeshPro Take()
        {
            TextMeshPro text = _free.Count > 0 ? _free.Pop() : Create();
            text.gameObject.SetActive(true);
            return text;
        }

        private void Recycle(int index)
        {
            Entry e = _active[index];
            _active.RemoveAt(index);
            if (e.Text == null) return;
            e.Text.gameObject.SetActive(false);
            _free.Push(e.Text);
        }

        private TextMeshPro Create()
        {
            var go = new GameObject("DamageNumber");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontStyle = FontStyles.Bold;
            text.sortingOrder = sortingOrder;
            text.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(0, 0, 0, 220);
            return text;
        }
    }
}
