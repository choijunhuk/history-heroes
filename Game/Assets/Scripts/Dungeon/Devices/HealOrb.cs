using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Player;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>회복 구슬 (열매 덤불): 영웅이 닿으면 최대체력의 비율만큼 회복. 둥실 떠서 카메라를 본다</summary>
    public sealed class HealOrb : MonoBehaviour
    {
        private float _fraction;
        private Transform _glow;
        private float _born;

        public static GameObject Create(Vector2 at, float fraction, Transform parent)
        {
            var go = new GameObject("HealOrb");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.5f;
            var orb = go.AddComponent<HealOrb>();
            orb._fraction = fraction;
            orb._born = Time.time;

            var glow = new GameObject("Glow").transform;
            glow.SetParent(go.transform, false);
            AddDisc(glow, 0.9f, new Color(0.45f, 1f, 0.55f, 0.45f), 30);
            AddDisc(glow, 0.45f, new Color(0.85f, 1f, 0.85f, 1f), 31);
            orb._glow = glow;
            return go;
        }

        private static void AddDisc(Transform parent, float size, Color color, int order)
        {
            var sr = new GameObject("Disc").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localScale = Vector3.one * size;
            sr.sprite = PlaceholderSprites.Circle;
            sr.color = color;
            sr.sortingOrder = order;
        }

        private void LateUpdate()
        {
            if (_glow == null) return;
            float t = Time.time - _born;
            float pop = Mathf.Clamp01(t / 0.25f);
            _glow.localScale = Vector3.one * (pop * (1f + 0.08f * Mathf.Sin(t * 6f)));
            _glow.localPosition = Vector3.back * (0.45f + 0.12f * Mathf.Sin(t * 3f)); // 위 = -Z
            Camera cam = Camera.main;
            if (cam != null) _glow.rotation = cam.transform.rotation;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.Health.IsDead) return;
            player.Health.Heal(player.Health.Max * _fraction);
            FxBursts.Puff(transform.position + Vector3.back * 0.5f, new Color(0.5f, 1f, 0.6f), 1.1f);
            Audio.AudioManager.Play("gem", 0.6f, 0.1f);
            Destroy(gameObject);
        }
    }
}
