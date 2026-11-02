using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 설정 줄 하나의 모양 (왼쪽 아이콘 + 이름은 빌더가 만든다, 오른쪽 조절 칸).
    /// - 켜기/끄기: 줄 전체가 누르는 단추, 오른쪽에 스위치 (켜면 초록 + 손잡이가 오른쪽)
    /// - 고르기: 칸마다 단추 (배경음 · 효과음은 0~100% 5칸, 나머지는 2~3칸). 고른 칸이 노랗게
    /// 값은 SettingsPanel이 정하고 Show로 그리기만 시킨다 (설정 로직은 그대로).
    /// </summary>
    public sealed class SettingRow : MonoBehaviour
    {
        [SerializeField] private bool toggle;
        [Tooltip("켜기/끄기: [0] 줄 전체 단추. 고르기: 칸마다 단추")]
        [SerializeField] private Button[] buttons = new Button[0];
        [Tooltip("켜기/끄기: [0] 스위치 바탕. 고르기: 칸 바탕")]
        [SerializeField] private Image[] images = new Image[0];
        [SerializeField] private TMP_Text[] labels = new TMP_Text[0];
        [SerializeField] private RectTransform knob;

        private static readonly Color On = new Color(0.3f, 0.82f, 0.45f);
        private static readonly Color Off = UiTone.Muted;
        private static readonly Color Picked = new Color(1f, 0.8f, 0.25f);
        private static readonly Color Idle = UiTone.Idle;
        private static readonly Color PickedText = Color.white;
        private static readonly Color IdleText = new Color(0.85f, 0.9f, 1f, 0.9f);

        /// <summary>칸을 눌렀다 (고르기: 칸 번호, 켜기/끄기: 바꾼 뒤의 값 1=켜기 0=끄기)</summary>
        public event System.Action<int> Chosen;

        private bool _on;

        private void Awake()
        {
            if (toggle)
            {
                if (buttons.Length > 0 && buttons[0] != null) buttons[0].onClick.AddListener(() => Chosen?.Invoke(_on ? 0 : 1));
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                if (buttons[i] != null) buttons[i].onClick.AddListener(() => Chosen?.Invoke(index));
            }
        }

        /// <summary>켜기/끄기면 0/1, 고르기면 고른 칸 번호</summary>
        public void Show(int value)
        {
            if (toggle)
            {
                _on = value != 0;
                if (images.Length > 0 && images[0] != null) images[0].color = _on ? On : Off;
                if (knob != null)
                {
                    knob.anchorMin = knob.anchorMax = new Vector2(_on ? 1f : 0f, 0.5f);
                    knob.pivot = new Vector2(_on ? 1f : 0f, 0.5f);
                    knob.anchoredPosition = new Vector2(_on ? -6f : 6f, 0f);
                }

                if (labels.Length > 0 && labels[0] != null)
                {
                    labels[0].text = _on ? "켜짐" : "꺼짐";
                    labels[0].alignment = _on ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
                }

                return;
            }

            for (int i = 0; i < images.Length; i++)
            {
                bool picked = i == value;
                if (images[i] != null) images[i].color = picked ? Picked : Idle;
                if (i < labels.Length && labels[i] != null) labels[i].color = picked ? PickedText : IdleText;
            }
        }
    }
}
