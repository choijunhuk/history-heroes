using System;
using HistoryHeroes.Combat;
using HistoryHeroes.Player;
using UnityEngine;

namespace HistoryHeroes.Survival
{
    /// <summary>서바이벌 줍는 물건 (확장 1차 6-4장) + 엘리트 보물 상자</summary>
    public enum PickupKind
    {
        Magnet,
        Bomb,
        Meat,
        GoldBag,
        Chest,
    }

    /// <summary>
    /// 땅에 떨어진 물건 하나: 영웅이 닿으면 Picked. 효과는 SurvivalRun이 정한다 (자석 · 폭탄 · 고기 · 금화 · 보물 상자).
    /// 천천히 돌고 둥실 뜬다 (상자는 돌지 않는다). 바닥 빛 고리로 멀리서도 보인다
    /// </summary>
    public sealed class SurvivalPickup : MonoBehaviour
    {
        private PickupKind _kind;
        private Transform _model;
        private SpriteRenderer _ring;
        private float _born;
        private Action<SurvivalPickup> _picked;

        public PickupKind Kind => _kind;

        public static SurvivalPickup Create(PickupKind kind, Vector2 at, Transform parent, GameObject prefab, Action<SurvivalPickup> picked)
        {
            var go = new GameObject("Pickup_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = kind == PickupKind.Chest ? 0.8f : 0.6f;
            var pickup = go.AddComponent<SurvivalPickup>();
            pickup._kind = kind;
            pickup._born = Time.time;
            pickup._picked = picked;

            var ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(go.transform, false);
            ring.transform.localScale = Vector3.one * (kind == PickupKind.Chest ? 1.9f : 1.2f);
            ring.sprite = Common.PlaceholderSprites.Circle;
            ring.color = ColorOf(kind) * new Color(1f, 1f, 1f, 0.35f);
            ring.sortingOrder = -7;
            pickup._ring = ring;

            var holder = new GameObject("Spin").transform;
            holder.SetParent(go.transform, false);
            GameObject model = Dungeon.RoomDevices.FitModel(holder, prefab, kind == PickupKind.Chest ? 1.25f : 0.75f);
            if (model == null)
            {
                var sr = holder.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = Common.PlaceholderSprites.Circle;
                sr.color = ColorOf(kind);
                sr.sortingOrder = 5;
                holder.localScale = Vector3.one * 0.6f;
            }

            pickup._model = holder;
            return pickup;
        }

        public static Color ColorOf(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Magnet: return new Color(0.45f, 0.75f, 1f);
                case PickupKind.Bomb: return new Color(1f, 0.45f, 0.25f);
                case PickupKind.Meat: return new Color(0.5f, 1f, 0.55f);
                case PickupKind.GoldBag: return new Color(1f, 0.85f, 0.3f);
                default: return new Color(1f, 0.8f, 0.35f);
            }
        }

        private void Update()
        {
            float t = Time.time - _born;
            float pop = Mathf.Clamp01(t / 0.25f);
            if (_model != null)
            {
                _model.localScale = Vector3.one * pop;
                bool chest = _kind == PickupKind.Chest;
                _model.localPosition = Vector3.back * (chest ? 0f : 0.25f + 0.12f * Mathf.Sin(t * 3f)); // 위 = -Z
                if (!chest) _model.localRotation = Quaternion.Euler(0f, 0f, t * 90f);
            }

            if (_ring != null)
            {
                Color c = ColorOf(_kind);
                c.a = 0.25f + 0.15f * Mathf.Sin(t * 4f);
                _ring.color = c;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            Action<SurvivalPickup> picked = _picked;
            _picked = null;
            picked?.Invoke(this);
            FxBursts.Puff(transform.position + Vector3.back * 0.5f, ColorOf(_kind), 1.2f);
            Destroy(gameObject);
        }
    }
}
