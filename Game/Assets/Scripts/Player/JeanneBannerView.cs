using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Player
{
    /// <summary>깃발은 바닥에 고정하고, 영역 테두리로 정지 보정이 닿는 곳을 보여 준다.</summary>
    public sealed class JeanneBannerView : MonoBehaviour
    {
        private PlayerController _player;
        private SpriteRenderer _range;
        private Transform _cloth;

        public static JeanneBannerView Create(PlayerController player)
        {
            var go = new GameObject("JeanneBanner");
            go.transform.SetParent(player.transform, false);
            var view = go.AddComponent<JeanneBannerView>();
            view._player = player;
            view._range = Make("Range", go.transform, PlaceholderSprites.RimCircle,
                Vector3.zero, Vector3.one * player.Hero.bannerRadius * 2f, new Color(0.45f, 0.7f, 1f, 0.3f), -4);

            var sign = new GameObject("Flag").transform;
            sign.SetParent(go.transform, false);
            sign.localPosition = Vector3.back * 0.8f;
            Camera camera = Camera.main;
            if (camera != null) sign.rotation = camera.transform.rotation;
            Make("Pole", sign, PlaceholderSprites.Square, Vector3.zero, new Vector3(0.055f, 1.6f, 1f),
                new Color(0.8f, 0.83f, 0.9f), 12);
            view._cloth = Make("Cloth", sign, PlaceholderSprites.Square, new Vector3(0.27f, 0.48f, 0f),
                new Vector3(0.55f, 0.4f, 1f), Color.white, 13).transform;
            Make("BlueStripe", view._cloth, PlaceholderSprites.Square, Vector3.zero, new Vector3(0.25f, 1f, 1f),
                new Color(0.12f, 0.26f, 0.7f), 14);
            return view;
        }

        private static SpriteRenderer Make(string name, Transform parent, Sprite sprite, Vector3 position, Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void LateUpdate()
        {
            if (_player == null || !_player.HasBanner) return;
            transform.position = (Vector3)_player.BannerCenter + Vector3.back * 0.025f;
            _range.color = new Color(0.45f, 0.7f, 1f, _player.BannerBuffActive ? 0.45f : 0.2f);
            Vector3 scale = _cloth.localScale;
            scale.x = 0.55f * (1f + 0.05f * Mathf.Sin(Time.time * 5f));
            _cloth.localScale = scale;
        }
    }
}
