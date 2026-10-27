using HistoryHeroes.Combat;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>1구간 열매 덤불 (확장 1차 2장): 2번 맞으면 부서지며 회복 구슬 (최대체력 5%). 방마다 구슬은 2개까지</summary>
    public sealed class BerryBush : MonoBehaviour
    {
        private DeviceRoom _room;

        public void Setup(DeviceRoom room, Breakable breakable)
        {
            _room = room;
            breakable.BrokenNow += OnBroken;
        }

        private void OnBroken(Breakable b)
        {
            b.BrokenNow -= OnBroken;
            FxBursts.Puff(transform.position + Vector3.back * 0.4f, new Color(0.45f, 0.85f, 0.35f), 1.2f);
            if (_room == null || _room.OrbsLeft <= 0) return;
            _room.OrbsLeft--;
            _room.Extras.Add(HealOrb.Create(transform.position, RoomDevices.BerryHeal, _room.Parent));
        }
    }
}
