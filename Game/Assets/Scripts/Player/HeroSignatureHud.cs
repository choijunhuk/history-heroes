using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Player
{
    /// <summary>
    /// 머리 위 대표 효과 표시 (새 영웅 화면 점검 2번: "멈춤 보너스를 발밑 원과 따로 보여 주기").
    /// - 이순신 철갑: 쌓인 피해 막대 (주황, 가득 차면 두근)
    /// - 이성계 백발백중: 점 3개가 흰색 → 파랑 → 금색으로 차오른다
    /// - 다빈치 작업대: 부품 점 3개 (구리색, 비행 기계가 가득이면 흐리게)
    /// - 제갈량 동남풍: 멈춰 있는 동안 발밑에 느려지는 범위 원 (청록)
    /// 머리 위 것은 카메라를 본다. 발밑 원은 바닥에 눕는다 (위 = -Z)
    /// </summary>
    public sealed class HeroSignatureHud : MonoBehaviour
    {
        private const int Order = 60;
        private const float BarWidth = 1.1f;
        private const float PipGap = 0.46f;
        private const float PipSize = 0.34f;
        private static readonly Color Empty = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color Iron = new Color(1f, 0.55f, 0.15f, 0.95f);
        private static readonly Color Part = new Color(0.95f, 0.68f, 0.35f, 1f);
        private static readonly Color Wind = new Color(0.45f, 0.95f, 1f, 0.32f); // 화면 점검 2차: 벽 밖까지 보이는 원이 헷갈리지 않게 옅게
        private static readonly Color[] Stage = { new Color(1f, 1f, 1f, 0.95f), new Color(0.45f, 0.85f, 1f, 1f), new Color(1f, 0.82f, 0.25f, 1f) };

        private PlayerController _player;
        private Transform _head;
        private SpriteRenderer _barBack, _barFill;
        private readonly SpriteRenderer[] _pips = new SpriteRenderer[3];
        private readonly SpriteRenderer[] _pipBacks = new SpriteRenderer[3];
        private SpriteRenderer _wind;
        private SpriteRenderer _icon;
        private float _windAlpha;
        private int _lastFilled;
        private float _pop; // 점이 새로 켜질 때 톡 (1 → 0)

        public static HeroSignatureHud Create(PlayerController player)
        {
            var go = new GameObject("SignatureHud");
            go.transform.SetParent(player.transform, false);
            var hud = go.AddComponent<HeroSignatureHud>();
            hud._player = player;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            _head = new GameObject("Head").transform;
            _head.SetParent(transform, false);
            _barBack = Make("BarBack", _head, PlaceholderSprites.Square, new Color(0f, 0f, 0f, 0.6f), Order);
            _barBack.transform.localScale = new Vector3(BarWidth + 0.08f, 0.2f, 1f);
            _barFill = Make("BarFill", _head, PlaceholderSprites.Square, Iron, Order + 1);
            for (int i = 0; i < _pips.Length; i++)
            {
                // 어두운 받침 원 위에 색 점 (밝은 바닥·효과 위에서도 보이게)
                _pipBacks[i] = Make($"PipBack{i}", _head, PlaceholderSprites.Circle, new Color(0f, 0f, 0f, 0.6f), Order);
                _pipBacks[i].transform.localScale = Vector3.one * (PipSize + 0.1f);
                _pips[i] = Make($"Pip{i}", _head, PlaceholderSprites.Circle, Empty, Order + 1);
                _pips[i].transform.localScale = Vector3.one * PipSize;
            }

            // 뜻을 알리는 작은 아이콘 (방패 = 철갑, 활 = 모으기, 톱니 = 부품), 막대·점 왼쪽
            _icon = Make("Icon", _head, null, Color.white, Order + 2);
            _icon.transform.localScale = Vector3.one * 0.42f;
            _wind = Make("WindRange", transform, PlaceholderSprites.RimCircle, Wind, -4);
            _wind.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        }

        private static SpriteRenderer Make(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        private void LateUpdate()
        {
            if (_player == null || _player.Hero == null) return;
            bool alive = !_player.Health.IsDead;
            bool stopped = alive && _player.IsStopped;

            // 머리 위 자리: 모델 키 + 0.5 위 (위 = -Z), 카메라를 본다
            if (Common.Art2D.Enabled)
            {
                // 2D 그림: 머리는 화면 위(+Y) 쪽, 카메라가 바로 위에서 보므로 돌리지 않는다
                _head.position = _player.transform.position + new Vector3(0f, _player.Hero.modelHeight * 1.12f + 0.3f, -0.05f);
                _head.rotation = Quaternion.identity;
            }
            else
            {
                _head.position = _player.transform.position + new Vector3(0f, 0f, -(_player.Hero.modelHeight + 0.5f));
                Camera cam = Camera.main;
                if (cam != null) _head.rotation = cam.transform.rotation;
            }

            UpdateIronclad(alive);
            UpdatePips(alive, stopped);
            UpdateWind(stopped);
            UpdateIcon();
        }

        private void UpdateIcon()
        {
            Skills.SkillArt art = Skills.SkillArt.Get;
            Sprite sprite = null;
            float left = 0f;
            if (_barFill.enabled)
            {
                sprite = art != null ? art.hudIron : null;
                left = -BarWidth * 0.5f - 0.3f;
            }
            else if (_pips[0].enabled)
            {
                sprite = art != null ? (_player.Hero.chargeShot ? art.hudCharge : art.hudParts) : null;
                int total = _player.Hero.chargeShot ? 3 : Mathf.Clamp(_player.PartsPerMachine, 1, _pips.Length);
                left = -(total - 1) * 0.5f * PipGap - 0.42f;
            }

            _icon.enabled = sprite != null;
            if (!_icon.enabled) return;
            _icon.sprite = sprite;
            _icon.transform.localPosition = new Vector3(left, 0f, 0f);
            float k = Mathf.Max(0.0001f, sprite.bounds.size.x);
            _icon.transform.localScale = Vector3.one * (0.42f / k);
        }

        private void UpdateIronclad(bool alive)
        {
            float cap = _player.IroncladCap;
            bool show = alive && cap > 0f && _player.IroncladStore > 0.5f;
            _barBack.enabled = show;
            _barFill.enabled = show;
            if (!show) return;
            float k = Mathf.Clamp01(_player.IroncladStore / cap);
            _barFill.transform.localScale = new Vector3(BarWidth * k, 0.12f, 1f);
            _barFill.transform.localPosition = new Vector3(-BarWidth * 0.5f * (1f - k), 0f, 0f);
            float pulse = k >= 0.99f ? 0.75f + 0.25f * Mathf.Sin(Time.time * 12f) : 1f; // 가득: 두근
            _barFill.color = new Color(Iron.r, Iron.g * pulse, Iron.b, Iron.a);
        }

        private void UpdatePips(bool alive, bool stopped)
        {
            int filled = 0, total = 0;
            Color on = Part;
            bool charge = _player.Hero.chargeShot;
            if (charge)
            {
                filled = _player.ChargeStage;
                total = 3;
            }
            else if (_player.Hero.workbench)
            {
                filled = _player.WorkbenchParts;
                total = Mathf.Clamp(_player.PartsPerMachine, 1, _pips.Length);
                if (_player.MachineCount >= _player.MaxMachines) on.a = 0.45f; // 가득: 다음 기계는 기다린다
            }

            bool show = alive && total > 0 && (stopped || filled > 0);
            if (filled > _lastFilled) _pop = 1f; // 새 칸이 켜졌다
            _lastFilled = filled;
            _pop = Mathf.MoveTowards(_pop, 0f, Time.deltaTime * 5f);
            for (int i = 0; i < _pips.Length; i++)
            {
                SpriteRenderer pip = _pips[i];
                pip.enabled = show && i < total;
                _pipBacks[i].enabled = pip.enabled;
                if (!pip.enabled) continue;
                var at = new Vector3((i - (total - 1) * 0.5f) * PipGap, 0f, 0f);
                pip.transform.localPosition = at;
                _pipBacks[i].transform.localPosition = at;
                bool lit = i < filled;
                Color c = lit ? (charge ? Stage[i] : on) : Empty;
                if (!lit && i == filled && stopped) c.a = 0.25f + 0.2f * Mathf.PingPong(Time.time * 3f, 1f); // 다음 칸이 차오르는 중
                pip.color = c;
                float pop = lit && i == filled - 1 ? 1f + 0.6f * _pop : 1f; // 방금 켜진 칸은 톡 커졌다 돌아온다
                pip.transform.localScale = Vector3.one * (lit && charge && i == 2 ? PipSize * 1.3f : PipSize) * pop; // 3단은 크게
            }
        }

        private void UpdateWind(bool stopped)
        {
            float radius = _player.WindRadius;
            _windAlpha = Mathf.MoveTowards(_windAlpha, stopped && radius > 0f ? 1f : 0f, Time.deltaTime * 4f);
            _wind.enabled = _windAlpha > 0.01f;
            if (!_wind.enabled) return;
            _wind.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.85f, 1f, _windAlpha);
            Color c = Wind;
            c.a *= _windAlpha * (0.8f + 0.2f * Mathf.Sin(Time.time * 4f));
            _wind.color = c;
        }
    }
}
