using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 오프닝 컷 3장면 (UX 4장 0:00, Content 1장 세계관). "다음"으로 넘기고 "건너뛰기"로 바로 튜토리얼.
    /// 그림이 생기기 전이라 글과 색으로만 (Art 단계에서 장면 그림을 넣는다).
    /// 같은 화면으로 이야기 예고도 연다 (망각의 왕 격파 뒤 신의 심판 예고, Story.GodsTeaser).
    /// </summary>
    public sealed class OpeningPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text textLabel;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private Button skipButton;

        private static readonly string[] OpeningScenes =
        {
            "역사가 지워지기 시작했다.\n\n<size=80%>시간의 틈으로 <b>망각의 군단</b>이 쏟아져 나와\n사람들의 기억을 먹어치운다.</size>",
            "기억이 사라진 역사는\n<b>처음부터 없었던 일</b>이 된다.\n\n<size=80%>영웅들의 이름도, 그들이 바꾼 세상도.</size>",
            "<color=#FFD966>클리오</color>  \"기록 수호자님, 와 주셨군요!\n저는 시간의 기록관 클리오예요.\n기록서 속 영웅들과 함께 역사를 지켜 주세요!\"",
        };

        private static readonly Color[] OpeningTints =
        {
            new Color(0.08f, 0.06f, 0.12f), new Color(0.12f, 0.05f, 0.08f), new Color(0.06f, 0.1f, 0.14f),
        };

        private string[] _scenes = OpeningScenes;
        private Color[] _tints = OpeningTints;
        private string _lastLabel = "시작하기";
        private int _page;
        private Action _onDone;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            nextButton.onClick.AddListener(Next);
            skipButton.onClick.AddListener(Finish);
        }

        public void Open(Action onDone) => Open(OpeningScenes, OpeningTints, "시작하기", onDone);

        /// <summary>장면 글 · 배경색 · 마지막 단추 글을 골라 연다 (이야기 예고)</summary>
        public void Open(string[] scenes, Color[] tints, string lastLabel, Action onDone)
        {
            _scenes = scenes;
            _tints = tints;
            _lastLabel = lastLabel;
            _onDone = onDone;
            _page = 0;
            root.SetActive(true);
            Render();
        }

        private void Next()
        {
            if (_page >= _scenes.Length - 1)
            {
                Finish();
                return;
            }

            _page++;
            Render();
        }

        private void Render()
        {
            textLabel.text = _scenes[_page];
            pageLabel.text = $"{_page + 1} / {_scenes.Length}";
            nextLabel.text = _page >= _scenes.Length - 1 ? _lastLabel : "다음";
            if (background != null && _tints != null && _page < _tints.Length) background.color = _tints[_page];
        }

        private void Finish()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }
    }
}
