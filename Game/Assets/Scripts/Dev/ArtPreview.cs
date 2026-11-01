#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Reflection;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// 그래픽 방향 비교 (Art.md 3장): Play 중에 **그림만** 바꿔 끼워 같은 장면을 그림 세트마다 찍는다.
    /// 프리팹·장면·저장은 건드리지 않고, 끄면(End) 원래 도형으로 돌린다. 멈춘 화면(timeScale 0)에서도 바로 바뀐다.
    /// 바꾸는 것: 영웅 · 돌진병 · 궁병 · 아군/적 화살 · 회전 칼날 · 바닥 · 벽 · 높은 장애물(돌) · 둥근 기둥 · 부서지는 상자.
    /// </summary>
    public sealed class ArtPreview : MonoBehaviour
    {
        public sealed class Set
        {
            public string name;
            public Sprite hero, charger, archer, arrowPlayer, arrowEnemy, blade, floor, stone, rock, crate;
            public float arrowScale = 1f, bladeScale = 1f;
        }

        private struct Original
        {
            public Sprite Sprite;
            public Color Color;
            public bool Enabled;
            public Vector3 Scale;
        }

        private static readonly FieldInfo PlayerBody = typeof(PlayerController).GetField("bodyRenderer", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo FlashBase = typeof(HitFlash).GetField("_baseColor", BindingFlags.NonPublic | BindingFlags.Instance);

        private static ArtPreview _instance;
        private Set _set;
        private readonly Dictionary<SpriteRenderer, Original> _originals = new Dictionary<SpriteRenderer, Original>();
        private readonly Dictionary<SpriteRenderer, SpriteRenderer> _tiled = new Dictionary<SpriteRenderer, SpriteRenderer>();

        public static string Begin(Set set)
        {
            End();
            var go = new GameObject("[ArtPreview]");
            _instance = go.AddComponent<ArtPreview>();
            _instance._set = set;
            _instance.Apply();
            return "그림: " + set.name;
        }

        public static void End()
        {
            if (_instance == null) return;
            _instance.Restore();
            Destroy(_instance.gameObject);
            _instance = null;
        }

        private void LateUpdate() => Apply();

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Apply()
        {
            if (_set == null) return;

            foreach (PlayerController p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (PlayerBody?.GetValue(p) is SpriteRenderer body) Skin(body, _set.hero);
            }

            foreach (EnemyBase e in EnemyBase.Alive)
            {
                if (e == null) continue;
                Sprite sprite = e is ArcherEnemy ? _set.archer : e is ChargerEnemy ? _set.charger : null;
                if (sprite != null && e.TryGetComponent(out SpriteRenderer sr)) Skin(sr, sprite);
            }

            foreach (Projectile projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                Sprite sprite = projectile.name.StartsWith("Arrow_Player") ? _set.arrowPlayer : projectile.name.StartsWith("Arrow_Enemy") ? _set.arrowEnemy : null;
                if (sprite != null && projectile.TryGetComponent(out SpriteRenderer sr)) Skin(sr, sprite, _set.arrowScale);
            }

            foreach (OrbitBladesBehaviour blades in FindObjectsByType<OrbitBladesBehaviour>(FindObjectsSortMode.None))
            {
                foreach (SpriteRenderer sr in blades.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr.name.StartsWith("Blade ")) Skin(sr, _set.blade, _set.bladeScale);
                }
            }

            foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (_tiled.ContainsValue(sr)) continue;
                switch (sr.name)
                {
                    case "Floor":
                        Tile(sr, _set.floor);
                        break;
                    case "Wall Left":
                    case "Wall Right":
                    case "Wall Top":
                    case "Wall Bottom":
                        Tile(sr, _set.stone);
                        break;
                    case "High":
                        if (sr.sprite == PlaceholderSprites.Circle || (_originals.TryGetValue(sr, out Original o) && o.Sprite == PlaceholderSprites.Circle)) Skin(sr, _set.rock);
                        else Tile(sr, _set.stone);
                        break;
                    case "Breakable":
                        Skin(sr, _set.crate);
                        break;
                }
            }
        }

        /// <summary>같은 크기(균일 배율) 그대로 그림만 바꾼다. scale을 주면 늘려 그린 투사체·칼날을 균일하게 맞춘다</summary>
        private void Skin(SpriteRenderer sr, Sprite sprite, float scale = 0f)
        {
            if (sr == null || sprite == null) return;
            Remember(sr);
            sr.sprite = sprite;
            sr.color = Color.white;
            if (scale > 0f) sr.transform.localScale = Vector3.one * scale;
            if (FlashBase != null && sr.TryGetComponent(out HitFlash flash)) FlashBase.SetValue(flash, Color.white); // 맞은 뒤 원래 색(도형 색)으로 돌아가지 않게
        }

        /// <summary>늘려 그린 네모(바닥·벽·높은 장애물): 원래 그림은 숨기고 자식에 무늬를 이어 붙여 그린다</summary>
        private void Tile(SpriteRenderer sr, Sprite sprite)
        {
            if (sr == null || sprite == null) return;
            Remember(sr);
            if (!_tiled.TryGetValue(sr, out SpriteRenderer child) || child == null)
            {
                var go = new GameObject("ArtTiled");
                go.transform.SetParent(sr.transform, false);
                child = go.AddComponent<SpriteRenderer>();
                _tiled[sr] = child;
            }

            Vector3 lossy = sr.transform.lossyScale;
            child.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, lossy.x), 1f / Mathf.Max(0.0001f, lossy.y), 1f);
            child.sprite = sprite;
            child.drawMode = SpriteDrawMode.Tiled;
            child.tileMode = SpriteTileMode.Continuous;
            child.size = new Vector2(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
            child.sortingLayerID = sr.sortingLayerID;
            child.sortingOrder = sr.sortingOrder;
            sr.enabled = false;
        }

        private void Remember(SpriteRenderer sr)
        {
            if (_originals.ContainsKey(sr)) return;
            _originals[sr] = new Original { Sprite = sr.sprite, Color = sr.color, Enabled = sr.enabled, Scale = sr.transform.localScale };
        }

        private void Restore()
        {
            foreach (KeyValuePair<SpriteRenderer, Original> pair in _originals)
            {
                SpriteRenderer sr = pair.Key;
                if (sr == null) continue;
                sr.sprite = pair.Value.Sprite;
                sr.color = pair.Value.Color;
                sr.enabled = pair.Value.Enabled;
                sr.transform.localScale = pair.Value.Scale;
                if (FlashBase != null && sr.TryGetComponent(out HitFlash flash)) FlashBase.SetValue(flash, pair.Value.Color);
            }

            foreach (SpriteRenderer child in _tiled.Values)
            {
                if (child == null) continue;
                child.enabled = false; // Destroy는 프레임 끝이라, 같은 프레임에 다른 세트로 찍으면 옛 무늬가 겹쳐 보였다
                Destroy(child.gameObject);
            }

            _originals.Clear();
            _tiled.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
#endif
