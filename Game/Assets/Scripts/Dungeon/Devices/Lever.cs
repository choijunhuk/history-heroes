using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 3구간 레버 (확장 1차 2장): 쏘면(근접도) 3초 동안 이 방의 컨베이어가 반대로 돌고 증기가 멈춘다. 쿨다운 6초.
    /// 바닥 고리 색: 초록 = 쏠 수 있음 · 주황 = 켜짐 · 회색 = 쉬는 중
    /// </summary>
    public sealed class Lever : MonoBehaviour, IShotTarget
    {
        public const float Active = 3f;
        public const float Cooldown = 6f;

        private DeviceRoom _room;
        private Transform _handle;
        private SpriteRenderer _ring;
        private float _readyAt;
        private float _activeUntil;

        public bool IsActive => Time.time < _activeUntil;

        public static GameObject Create(DeviceSpot d, DeviceRoom room, GameObject prefab)
        {
            var go = new GameObject("Lever") { layer = RoomDevices.WallLayer };
            go.transform.SetParent(room.Parent, false);
            go.transform.position = d.position;
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.7f, 0.7f);
            var lever = go.AddComponent<Lever>();
            lever._room = room;

            var ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(go.transform, false);
            ring.transform.localScale = Vector3.one * 1.3f;
            ring.sprite = PlaceholderSprites.Circle;
            ring.sortingOrder = -7;
            lever._ring = ring;

            var handle = new GameObject("Handle").transform;
            handle.SetParent(go.transform, false);
            if (prefab != null) RoomDevices.FitModel(handle, prefab, 0.8f);
            else
            {
                var sr = handle.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderSprites.Square;
                sr.color = new Color(0.55f, 0.45f, 0.3f);
                sr.sortingOrder = -5;
                handle.localScale = new Vector3(0.25f, 0.7f, 1f);
            }

            lever._handle = handle;
            return go;
        }

        public void Hit()
        {
            float now = Time.time;
            if (now < _readyAt) return;
            _activeUntil = now + Active;
            _readyAt = now + Cooldown;
            if (_room != null)
            {
                foreach (TrapZone zone in _room.Zones)
                {
                    if (zone != null) zone.Disrupt(Active);
                }
            }

            if (_handle != null) _handle.localRotation = Quaternion.Euler(0f, 0f, 180f); // 반대로 젖힌다
            FxBursts.Puff(transform.position + Vector3.back * 0.6f, new Color(1f, 0.75f, 0.3f), 0.9f);
            Audio.AudioManager.Play("door", 0.5f);
        }

        private void Update()
        {
            float now = Time.time;
            if (_handle != null && now >= _activeUntil && _handle.localRotation != Quaternion.identity) _handle.localRotation = Quaternion.identity;
            if (_ring == null) return;
            Color c = now < _activeUntil ? new Color(1f, 0.6f, 0.2f, 0.65f)
                : now < _readyAt ? new Color(0.5f, 0.5f, 0.5f, 0.35f)
                : new Color(0.4f, 1f, 0.5f, 0.45f + 0.2f * Mathf.Sin(now * 5f));
            _ring.color = c;
        }
    }
}
