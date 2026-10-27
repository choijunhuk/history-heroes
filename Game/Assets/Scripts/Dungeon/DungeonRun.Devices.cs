using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>구간 장치 (확장 1차 2장): 방 레이아웃의 장치 칸을 만든다. 방을 옮기면 같이 지운다</summary>
    public sealed partial class DungeonRun
    {
        [Header("확장 1차 2장 구간 장치")]
        [SerializeField] private DeviceArt deviceArt = new DeviceArt();

        private DeviceRoom _deviceRoom;

        /// <summary>QA: 지금 방의 장치들</summary>
        public DeviceRoom Devices => _deviceRoom;

        /// <summary>바닥 장식이 피할 곳: 개울 · 쇠창살 줄(+양옆 통로) · 톱니 홈 · 함정 칸</summary>
        private static List<Rect> DeviceAvoidRects(RoomLayout layout)
        {
            var list = new List<Rect>();
            if (layout.devices != null)
            {
                foreach (DeviceSpot d in layout.devices)
                {
                    switch (d.kind)
                    {
                        case DeviceKind.Creek:
                        case DeviceKind.IronGate:
                            Vector2 size = d.kind == DeviceKind.IronGate ? d.size + new Vector2(3f, 0.4f) : d.size;
                            list.Add(Bounds(d.position, size, d.angle));
                            break;
                        case DeviceKind.SawGear:
                            Vector2 min = Vector2.Min(d.position, d.to) - Vector2.one * 0.6f;
                            Vector2 max = Vector2.Max(d.position, d.to) + Vector2.one * 0.6f;
                            list.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
                            break;
                    }
                }
            }

            foreach (TrapSpot t in layout.traps) list.Add(Bounds(t.position, t.size, t.angle));
            return list;
        }

        /// <summary>돌린 상자를 감싸는 네모</summary>
        private static Rect Bounds(Vector2 center, Vector2 size, float angle)
        {
            float a = angle * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(a)), s = Mathf.Abs(Mathf.Sin(a));
            var half = new Vector2(size.x * 0.5f * c + size.y * 0.5f * s, size.x * 0.5f * s + size.y * 0.5f * c);
            return new Rect(center - half, half * 2f);
        }

        private void SpawnDevices(RoomLayout layout)
        {
            _deviceRoom = new DeviceRoom { Player = player, Parent = layoutRoot };
            if (layout.devices == null) return;
            foreach (DeviceSpot d in layout.devices)
            {
                GameObject go = RoomDevices.Spawn(d, _deviceRoom, deviceArt);
                if (go != null) _layoutObjects.Add(go);
            }
        }
    }
}
