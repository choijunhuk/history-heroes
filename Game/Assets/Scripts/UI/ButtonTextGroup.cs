using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 같은 역할 단추는 같은 글자 크기 (UX.md "같은 역할 버튼 = 같은 글자 크기", 2026-11-01 개발자: "계속 / 설정 / 판 포기 글씨 크기가 달라서 보기 불편").
    /// 원인: 단추마다 크기를 따로 정했고 TMP 자동 축소도 글마다 따로 줄었다.
    /// 방법: 묶음의 모든 글을 자동 크기로 한 번 재서 그중 가장 작은 값으로 모두 맞춘다.
    /// 글이 런타임에 바뀌어도(판 포기 → "정말 포기? 한 번 더") 글 내용 · 칸 크기가 바뀐 프레임에 다시 맞춘다 (바뀌지 않으면 아무것도 안 한다).
    /// 묶음 = 장면 빌더가 같은 부모 · 같은 높이의 단추끼리 자동으로 만든다 (SceneBuilder.ApplyButtonGroups), 또는 scanChildren으로 목록 단추 전부.
    /// </summary>
    public sealed class ButtonTextGroup : MonoBehaviour
    {
        [Tooltip("묶을 단추. 비어 있고 scanChildren이 켜져 있으면 아래 모든 단추")]
        [SerializeField] private Button[] buttons = new Button[0];
        [SerializeField] private bool scanChildren;

        private struct Entry
        {
            public TMP_Text Label;
            public bool Auto;
            public float Min, Max;
        }

        /// <summary>빌더가 목록 단추 전부를 묶을 때 (줄이 런타임에 복제된다)</summary>
        public void SetScan(bool value) => scanChildren = value;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Button> _scan = new List<Button>();
        private int _signature = int.MinValue;

        private void OnEnable() => _signature = int.MinValue; // 켜질 때 다시 맞춘다

        private void LateUpdate()
        {
            int signature = Signature();
            if (signature == _signature) return;
            _signature = signature;
            Equalize();
        }

        private IReadOnlyList<Button> Targets()
        {
            if (!scanChildren) return buttons;
            _scan.Clear();
            GetComponentsInChildren(false, _scan);
            return _scan;
        }

        /// <summary>글 내용 · 칸 크기 · 켜짐 상태가 하나라도 바뀌면 값이 달라진다 (할당 없음)</summary>
        private int Signature()
        {
            unchecked
            {
                int h = 17;
                IReadOnlyList<Button> targets = Targets();
                for (int i = 0; i < targets.Count; i++)
                {
                    Button b = targets[i];
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    TMP_Text label = LabelOf(b);
                    if (label == null) continue;
                    h = h * 31 + (label.text != null ? label.text.GetHashCode() : 0);
                    Rect r = label.rectTransform.rect;
                    h = h * 31 + Mathf.RoundToInt(r.width) * 7 + Mathf.RoundToInt(r.height);
                    h = h * 31 + i;
                }

                return h;
            }
        }

        private static TMP_Text LabelOf(Button button) => button.GetComponentInChildren<TMP_Text>(false);

        private void Equalize()
        {
            IReadOnlyList<Button> targets = Targets();
            // 글마다 "원래 범위"를 처음 한 번만 기억한다 (맞춘 뒤에는 자동 크기를 꺼 두므로)
            for (int i = 0; i < targets.Count; i++)
            {
                Button b = targets[i];
                if (b == null) continue;
                TMP_Text label = LabelOf(b);
                if (label == null || Known(label)) continue;
                bool auto = label.enableAutoSizing;
                float max = auto ? label.fontSizeMax : label.fontSize;
                float min = auto ? label.fontSizeMin : Mathf.Min(max, Mathf.Max(22f, max * 0.55f));
                _entries.Add(new Entry { Label = label, Auto = auto, Min = min, Max = max });
            }

            float smallest = float.MaxValue;
            int counted = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                if (e.Label == null || !e.Label.gameObject.activeInHierarchy || string.IsNullOrEmpty(e.Label.text)) continue;
                e.Label.enableAutoSizing = true;
                e.Label.fontSizeMin = e.Min;
                e.Label.fontSizeMax = e.Max;
                e.Label.ForceMeshUpdate();
                smallest = Mathf.Min(smallest, e.Label.fontSize);
                counted++;
            }

            if (counted < 2) // 혼자면 원래대로 (자동 크기 범위 복구)
            {
                Restore();
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                if (e.Label == null || !e.Label.gameObject.activeInHierarchy) continue;
                e.Label.enableAutoSizing = false;
                e.Label.fontSize = smallest;
            }
        }

        private void Restore()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                if (e.Label == null) continue;
                e.Label.enableAutoSizing = e.Auto;
                if (e.Auto)
                {
                    e.Label.fontSizeMin = e.Min;
                    e.Label.fontSizeMax = e.Max;
                }
                else e.Label.fontSize = e.Max;
            }
        }

        private bool Known(TMP_Text label)
        {
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].Label == label) return true;
            return false;
        }
    }
}
