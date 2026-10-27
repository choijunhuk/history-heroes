using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 맞으면 반응하는 방 물건 (Wall 레이어): 부서지는 상자 · 열매 덤불 · 폭약 통 · 레버.
    /// 투사체는 튕기지 않고 Hit만 부르고 사라진다, 근접 공격도 Hit을 부른다.
    /// </summary>
    public interface IShotTarget
    {
        void Hit();

        // ReSharper disable once InconsistentNaming — MonoBehaviour.transform이 그대로 채운다
        Transform transform { get; }
    }
}
