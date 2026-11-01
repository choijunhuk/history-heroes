using UnityEngine;

namespace HistoryHeroes.Combat
{
    /// <summary>
    /// 3D 캐릭터 프리팹에 붙는 정보 (Art3D-Plan 4번). 에디터에서 조립할 때(Art3DCharacters) 잰다.
    /// 게임 중에는 재지 않는다 (재려고 프리팹을 하나 더 만들면 첫 등장 때 끊긴다 — Codex 3D 리뷰 5번).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Model3DInfo : MonoBehaviour
    {
        [Tooltip("모델 원래 키 (배율 1, 서 있는 자세, 유닛)")]
        public float height = 1.8f;
        [Tooltip("그림자를 드리운다 (영웅·보스만. 일반 적은 끈다 — 250마리 성능)")]
        public bool castShadows = true;
    }
}
