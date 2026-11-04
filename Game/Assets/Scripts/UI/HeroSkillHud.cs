using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Player;
using HistoryHeroes.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>실제 발동하는 영웅 전용기의 이름과 다음 자동 발동까지의 시간을 보여 준다.</summary>
    public sealed class HeroSkillHud : MonoBehaviour
    {
        private PlayerController _player;
        private SkillManager _skills;
        private CanvasGroup _group;
        private Image _icon;
        private Image _fill;
        private TMP_Text _name;
        private TMP_Text _status;
        private RectTransform _bossBar;
        private SkillInstance _shown;
        private ActiveSkillBehaviour _bound;
        private string _heroId;
        private bool _rendered;
        private float _readyAt;
        private float _cooldown;
        private float _firedAt = float.NegativeInfinity;
        private int _seconds = -1;

        public static HeroSkillHud Create(RectTransform parent, PlayerController player, SkillManager skills, TMP_Text fontSource)
        {
            var root = new GameObject("HeroSkillHud", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -206f);
            rect.sizeDelta = new Vector2(620f, 88f);
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.04f, 0.08f, 0.17f, 0.79f);
            background.raycastTarget = false;

            var view = root.AddComponent<HeroSkillHud>();
            view._player = player;
            view._skills = skills;
            view._group = root.GetComponent<CanvasGroup>();
            view._group.alpha = 0f;
            view._group.blocksRaycasts = false;
            view._group.interactable = false;
            view._bossBar = parent.Find("BossHud/Bar") as RectTransform;
            view._icon = MakeImage(rect, "SkillIcon", new Vector2(-268f, -13f), new Vector2(58f, 58f));
            view._icon.enabled = false;
            view._icon.preserveAspect = true;
            view._name = MakeLabel(rect, "SkillName", new Vector2(20f, -5f), new Vector2(500f, 38f), 32f, fontSource);
            view._status = MakeLabel(rect, "SkillState", new Vector2(20f, -46f), new Vector2(500f, 31f), 30f, fontSource);
            var track = MakeImage(rect, "CooldownTrack", new Vector2(0f, -82f), new Vector2(600f, 5f));
            track.color = new Color(1f, 1f, 1f, 0.18f);
            view._fill = MakeImage((RectTransform)track.transform, "CooldownFill", Vector2.zero, Vector2.zero);
            var bar = (RectTransform)view._fill.transform;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = Vector2.one;
            bar.offsetMin = bar.offsetMax = Vector2.zero;
            view._fill.type = Image.Type.Filled;
            view._fill.fillMethod = Image.FillMethod.Horizontal;
            view._fill.sprite = PlaceholderSprites.Square;
            view._fill.color = new Color(1f, 0.78f, 0.28f, 0.95f);
            view._fill.fillAmount = 0f;
            return view;
        }

        private static Image MakeImage(RectTransform parent, string name, Vector2 offset, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            Image image = obj.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text MakeLabel(RectTransform parent, string name, Vector2 offset, Vector2 size, float fontSize, TMP_Text source)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            var label = obj.GetComponent<TextMeshProUGUI>();
            label.font = source.font;
            label.fontSharedMaterial = source.fontSharedMaterial;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize - 4f;
            label.fontSizeMax = fontSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        public static SkillInstance SelectExclusive(IReadOnlyList<SkillInstance> actives, string heroId)
        {
            if (actives == null || string.IsNullOrEmpty(heroId)) return null;
            SkillInstance chosen = null;
            foreach (SkillInstance skill in actives)
            {
                if (skill.Data == null || skill.Data.heroOnlyId != heroId) continue;
                if (skill.IsFusionResult) return skill;
                chosen = skill;
            }

            return chosen;
        }

        private void OnDisable()
        {
            if (_bound != null) _bound.Activated -= OnActivated;
            _bound = null;
        }

        private void Update()
        {
            if (_player == null || _skills == null || _player.Hero == null) return;
            string heroId = _player.Hero.id;
            SkillInstance selected = SelectExclusive(_skills.Loadout.Actives, heroId);
            if (!_rendered || selected != _shown || heroId != _heroId)
            {
                _rendered = true;
                _heroId = heroId;
                _shown = selected;
                SkillData data = selected != null ? selected.Data : _player.Hero.exclusiveActive;
                _name.text = data != null ? data.displayName : _player.Hero.displayName;
                _icon.sprite = HeroSkillArt.IconFor(data);
                _icon.enabled = _icon.sprite != null;
                _readyAt = _cooldown = 0f;
                _firedAt = float.NegativeInfinity;
                _seconds = -1;
                _status.text = selected != null ? "자동 발동 대기"
                    : GameSession.Mode == BattleMode.Boss ? "이 기록에 고유 기술 없음" : "카드에서 고유 기술 선택";
            }

            // 보스 체력 막대가 열리면 알림 띠 아래로 옮긴다.
            ((RectTransform)transform).anchoredPosition = new Vector2(0f, _bossBar != null && _bossBar.gameObject.activeInHierarchy ? -365f : -206f);

            ActiveSkillBehaviour behaviour = selected != null ? selected.Behaviour : null;
            if (behaviour != _bound)
            {
                if (_bound != null) _bound.Activated -= OnActivated;
                _bound = behaviour;
                if (_bound != null) _bound.Activated += OnActivated;
            }

            _group.alpha = _player.Health == null || _player.Health.IsDead || GamePause.IsPaused ? 0f : 1f;
            if (selected == null) { _fill.fillAmount = 0f; return; }

            float remaining = Mathf.Max(0f, _readyAt - Time.time);
            _fill.fillAmount = _cooldown > 0f ? 1f - remaining / _cooldown : 1f;
            if (Time.time - _firedAt < 0.6f)
            {
                if (_seconds != -2) { _status.text = "고유 기술 발동!"; _seconds = -2; }
            }
            else
            {
                int seconds = Mathf.CeilToInt(remaining);
                if (seconds != _seconds)
                {
                    _status.text = seconds > 0 ? $"자동 발동 · {seconds}초" : "자동 발동 대기";
                    _seconds = seconds;
                }
            }
        }

        private void OnActivated(float cooldown)
        {
            _cooldown = Mathf.Max(0f, cooldown);
            _readyAt = Time.time + _cooldown;
            _firedAt = Time.time;
            _seconds = -1;
        }
    }
}
