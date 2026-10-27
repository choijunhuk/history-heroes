using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 2구간 쇠창살 (확장 1차 2장 + 검토 10번): 8초마다 3초 동안 바닥에서 솟아 높은 장애물(이동·탄 막음).
    /// 1초 전에 바닥 그림자 + 양옆 열린 통로에 밝은 표시 줄. 솟는 순간 줄 안의 몸(영웅·적)은 가까운 쪽으로 밀려난다.
    /// 줄은 방을 다 막지 않는다 — 닫혀도 양옆으로 지나갈 수 있다 (도달 검사).
    /// </summary>
    public sealed class IronGate : MonoBehaviour
    {
        public const float Period = 8f;
        public const float Down = 3f;
        public const float Warn = 1f;
        /// <summary>방에 들어오고 처음 닫히기까지 (예고 포함 여유)</summary>
        public const float FirstClose = 4f;

        private Vector2 _size;
        private BoxCollider2D _collider;
        private Transform _bars;
        private SpriteRenderer _shadow;
        private SpriteRenderer[] _lanes;
        private float _clock;
        private bool _closed;
        private float _raise; // 0 = 바닥 밑 · 1 = 다 솟음
        private static readonly Collider2D[] Hits = new Collider2D[24];

        public bool Closed => _closed;

        public static GameObject Create(DeviceSpot d, DeviceRoom room, GameObject prefab)
        {
            var go = new GameObject("IronGate") { layer = RoomDevices.WallLayer };
            go.transform.SetParent(room.Parent, false);
            go.transform.position = d.position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, d.angle);
            var gate = go.AddComponent<IronGate>();
            gate.Build(d.size, prefab);
            return go;
        }

        private void Build(Vector2 size, GameObject prefab)
        {
            _size = size;
            _collider = gameObject.AddComponent<BoxCollider2D>();
            _collider.size = size;
            _collider.enabled = false;

            _shadow = Sprite("Shadow", Vector2.zero, size, new Color(0f, 0f, 0f, 0f), -7);
            // 열린 통로 표시: 줄 양끝 바깥 (방 안쪽만, 닫힐 때 밝아진다)
            _lanes = new[]
            {
                Sprite("LaneL", new Vector2(-size.x * 0.5f - 0.75f, 0f), new Vector2(1.2f, size.y + 0.2f), new Color(0.6f, 1f, 0.7f, 0f), -7),
                Sprite("LaneR", new Vector2(size.x * 0.5f + 0.75f, 0f), new Vector2(1.2f, size.y + 0.2f), new Color(0.6f, 1f, 0.7f, 0f), -7),
            };

            // 쇠창살 모델: 줄 길이에 맞춰 여러 개 (바닥 밑에 숨어 있다가 솟는다)
            _bars = new GameObject("Bars").transform;
            _bars.SetParent(transform, false);
            if (prefab != null)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(size.x / 1.5f));
                float step = size.x / count;
                for (int i = 0; i < count; i++)
                {
                    var holder = new GameObject($"Bar{i}").transform;
                    holder.SetParent(_bars, false);
                    holder.localPosition = new Vector3(-size.x * 0.5f + step * (i + 0.5f), 0f, 0f);
                    RoomDevices.FitModel(holder, prefab, step * 1.02f);
                }
            }
            else
            {
                SpriteRenderer bar = Sprite("Bar2D", Vector2.zero, size, new Color(0.35f, 0.35f, 0.4f, 1f), -5);
                bar.transform.SetParent(_bars, true);
            }

            SetRaise(0f);
            _clock = Period - FirstClose; // 처음 닫힘 = 들어오고 4초 뒤 (예고는 3초부터)
        }

        private SpriteRenderer Sprite(string name, Vector2 local, Vector2 size, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.localPosition = local;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.sprite = PlaceholderSprites.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _clock = (_clock + dt) % Period;
            float closeAt = Period - Down;
            float warnAt = closeAt - Warn;
            bool shouldClose = _clock >= closeAt;
            bool warning = _clock >= warnAt && _clock < closeAt;

            // 예고: 그림자가 짙어지고 통로 표시가 깜빡인다
            float w = warning ? Mathf.Clamp01((_clock - warnAt) / Warn) : 0f;
            float blink = warning ? 0.5f + 0.5f * Mathf.Sin(_clock * 18f) : 0f;
            _shadow.color = new Color(0f, 0f, 0f, shouldClose ? 0.45f : 0.55f * w);
            foreach (SpriteRenderer lane in _lanes) lane.color = new Color(0.6f, 1f, 0.7f, warning ? 0.25f + 0.3f * blink : shouldClose ? 0.18f : 0f);

            if (shouldClose && !_closed) Close();
            else if (!shouldClose && _closed) Open();

            float target = _closed ? 1f : 0f;
            if (!Mathf.Approximately(_raise, target)) SetRaise(Mathf.MoveTowards(_raise, target, dt / 0.18f));
        }

        private void Close()
        {
            _closed = true;
            PushOutBodies();
            _collider.enabled = true;
            CameraShake.Add(0.12f);
            Audio.AudioManager.Play("slam", 0.5f);
        }

        private void Open()
        {
            _closed = false;
            _collider.enabled = false;
        }

        /// <summary>솟는 줄 안에 있는 몸은 줄의 가까운 옆(위·아래)으로 민다 — 끼지 않게</summary>
        private void PushOutBodies()
        {
            int n = Physics2D.OverlapBoxNonAlloc(transform.position, _size, transform.eulerAngles.z, Hits, RoomDevices.BodyMask);
            for (int i = 0; i < n; i++)
            {
                Vector2 local = transform.InverseTransformPoint(Hits[i].transform.position);
                float side = local.y >= 0f ? 1f : -1f;
                float need = _size.y * 0.5f - Mathf.Abs(local.y) + 0.45f;
                RoomDevices.Push(Hits[i], (Vector2)(transform.rotation * new Vector2(0f, side * need)));
            }
        }

        /// <summary>창살 높이: 0 = 바닥 밑(바닥이 가린다), 1 = 다 솟음 (위 = -Z)</summary>
        private void SetRaise(float r)
        {
            _raise = r;
            _bars.localPosition = new Vector3(0f, 0f, Mathf.Lerp(2.2f, 0f, r));
            _bars.gameObject.SetActive(r > 0.001f);
        }
    }
}
