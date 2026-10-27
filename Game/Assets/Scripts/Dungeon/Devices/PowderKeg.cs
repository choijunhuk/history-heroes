using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 2구간 폭약 통 (확장 1차 2장 + 검토 9번): 3번 맞으면 1초 깜빡(빨간 원 예고) → 반경 1.5 폭발.
    /// 나 = 최대체력 15% · 일반 적 = 내 공격력 200% · 엘리트 100% · 보스 50%. 통으로 잡은 적은 내 처치 (액티브 피해).
    /// 옆 통도 터뜨린다 (연쇄는 한 번에 3개까지, 이어지는 통은 0.25초 깜빡)
    /// </summary>
    public sealed class PowderKeg : MonoBehaviour
    {
        public const float Radius = 1.5f;
        public const float Fuse = 1f;
        public const float ChainFuse = 0.25f;
        public const int ChainMax = 3;
        public const float PlayerDamage = 0.15f;

        private DeviceRoom _room;
        private Transform _model;
        private Vector3 _modelScale;
        private bool _lit;
        private bool _done;
        private float _litAt;
        private float _fuse;
        private int _chain;
        private SpriteRenderer _telegraph;
        private static readonly Collider2D[] Hits = new Collider2D[32];

        public bool Done => _done;

        public void Setup(DeviceRoom room, Breakable breakable, Transform model)
        {
            _room = room;
            _model = model;
            _modelScale = model != null ? model.localScale : Vector3.one;
            breakable.HideOnBreak = false;
            breakable.BrokenNow += _ => Light(0, Fuse);
        }

        /// <summary>불을 붙인다 (chain = 이 연쇄에서 몇 번째)</summary>
        public void Light(int chain, float fuse)
        {
            if (_lit || _done) return;
            _lit = true;
            _chain = chain;
            _fuse = fuse;
            _litAt = Time.time;
            _telegraph = Telegraphs.Circle(transform.position, Radius, Telegraphs.Danger);
            Audio.AudioManager.Play("telegraph", 0.6f);
        }

        private void Update()
        {
            if (!_lit || _done) return;
            float t = Mathf.Clamp01((Time.time - _litAt) / Mathf.Max(0.05f, _fuse));
            if (_model != null) _model.localScale = _modelScale * (1f + 0.12f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 6f)));
            if (_telegraph != null) Telegraphs.Fill(_telegraph, Telegraphs.Danger, t);
            if (t >= 1f) Explode();
        }

        private void Explode()
        {
            _done = true;
            if (_telegraph != null) Telegraphs.Hide(_telegraph);
            _telegraph = null;
            Vector2 at = transform.position;
            FxBursts.Explosion(at, Radius, new Color(1f, 0.55f, 0.15f));
            CameraShake.Add(0.35f);
            Audio.AudioManager.Play("explode", 0.9f);

            int n = Physics2D.OverlapCircleNonAlloc(at, Radius, Hits, RoomDevices.BodyMask);
            float attack = _room != null ? _room.PlayerAttack : 100f;
            for (int i = 0; i < n; i++)
            {
                if (!Hits[i].TryGetComponent(out Health h) || h.IsDead) continue;
                if (h.Team == Team.Player)
                {
                    h.TryTakeDamage(RoomDevices.ToPlayer(h.Max * PlayerDamage, "device_keg"));
                    continue;
                }

                EnemyBase enemy = Hits[i].GetComponent<EnemyBase>();
                float scale = enemy is IPhasedBoss ? 0.5f : enemy != null && enemy.IsElite ? 1f : 2f;
                h.TryTakeDamage(RoomDevices.FromPlayer(attack * scale, "device_keg"));
            }

            // 옆 통 연쇄 (한 번에 3개까지)
            if (_room != null && _chain + 1 < ChainMax)
            {
                foreach (PowderKeg other in _room.Kegs)
                {
                    if (other == null || other == this || other._lit || other._done) continue;
                    if (((Vector2)other.transform.position - at).sqrMagnitude > (Radius + 0.3f) * (Radius + 0.3f)) continue;
                    other.Light(_chain + 1, ChainFuse);
                }
            }

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_telegraph != null) Telegraphs.Hide(_telegraph);
            _telegraph = null;
        }
    }
}
