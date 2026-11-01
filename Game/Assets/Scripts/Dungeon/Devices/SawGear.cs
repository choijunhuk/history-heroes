using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 3구간 회전 톱니 (확장 1차 2장): 바닥 홈(예고 = 늘 보이는 길) 위를 두 점 사이로 왕복하는 높은 장애물.
    /// 닿으면 밀려나고 최대체력 10% (영웅·적 똑같이, 같은 몸은 0.8초에 한 번)
    /// </summary>
    public sealed class SawGear : MonoBehaviour
    {
        public const float Speed = 1.6f;
        public const float DamageFraction = 0.1f;
        public const float HitCooldown = 0.8f;
        public const float PushDistance = 0.9f;

        private Vector2 _a, _b;
        private float _radius;
        private float _t;
        private Rigidbody2D _body;
        private Transform _spin;
        private readonly Dictionary<Health, float> _ready = new Dictionary<Health, float>();
        private static readonly Collider2D[] Hits = new Collider2D[24];

        public static GameObject Create(DeviceSpot d, DeviceRoom room, GameObject prefab)
        {
            float size = Mathf.Max(0.8f, d.size.x);
            var go = new GameObject("SawGear") { layer = RoomDevices.WallLayer };
            go.transform.SetParent(room.Parent, false);
            go.transform.position = d.position;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<CircleCollider2D>().radius = size * 0.5f;
            var saw = go.AddComponent<SawGear>();
            saw._a = d.position;
            saw._b = d.to;
            saw._radius = size * 0.5f;
            saw._body = body;

            // 홈: 두 점을 잇는 어두운 줄 (어디로 움직이는지 늘 보인다)
            var groove = new GameObject("Groove").AddComponent<SpriteRenderer>();
            groove.transform.SetParent(room.Parent, false);
            Vector2 mid = (d.position + d.to) * 0.5f;
            Vector2 dir = d.to - d.position;
            groove.transform.position = mid;
            groove.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            groove.transform.localScale = new Vector3(dir.magnitude + size, size * 0.35f, 1f);
            groove.sprite = PlaceholderSprites.Square;
            groove.color = new Color(0.95f, 0.72f, 0.18f, 0.5f); // 화면 점검: 경로가 바닥보다 밝게 (노란 경고 띠)
            groove.sortingOrder = -8;
            room.Extras.Add(groove.gameObject);

            var spin = new GameObject("Spin").transform;
            spin.SetParent(go.transform, false);
            if (prefab != null) RoomDevices.FitModel(spin, prefab, size);
            else
            {
                var sr = spin.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderSprites.Circle;
                sr.color = new Color(0.6f, 0.6f, 0.65f);
                sr.sortingOrder = -5;
                spin.localScale = Vector3.one * size;
            }

            saw._spin = spin;
            return go;
        }

        private void FixedUpdate()
        {
            float length = Mathf.Max(0.01f, Vector2.Distance(_a, _b));
            _t += Time.fixedDeltaTime * Speed / length;
            Vector2 p = Vector2.Lerp(_a, _b, Mathf.PingPong(_t, 1f));
            _body.MovePosition(p);
            Touch(p);
        }

        private void Update()
        {
            if (_spin != null) _spin.Rotate(0f, 0f, 540f * Time.deltaTime, Space.Self);
        }

        private void Touch(Vector2 center)
        {
            int n = Physics2D.OverlapCircleNonAlloc(center, _radius + 0.2f, Hits, RoomDevices.BodyMask);
            float now = Time.time;
            for (int i = 0; i < n; i++)
            {
                if (!Hits[i].TryGetComponent(out Health h) || h.IsDead) continue;
                if (_ready.TryGetValue(h, out float ready) && now < ready) continue;
                _ready[h] = now + HitCooldown;
                Vector2 away = (Vector2)Hits[i].transform.position - center;
                if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
                RoomDevices.Push(Hits[i], away.normalized * PushDistance);
                float amount = h.Max * DamageFraction;
                h.TryTakeDamage(h.Team == Team.Player ? RoomDevices.ToPlayer(amount, "device_saw") : RoomDevices.ToEnemy(amount, "device_saw"));
                FxBursts.Puff((Vector3)(center + away.normalized * _radius) + Vector3.back * 0.4f, new Color(1f, 0.85f, 0.5f), 0.6f);
            }
        }
    }
}
