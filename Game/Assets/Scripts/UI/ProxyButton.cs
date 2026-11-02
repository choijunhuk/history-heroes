using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 이 단추를 누르면 다른 단추를 누른 것과 같다. 위 띠의 뒤로 화살표가 창의 원래 "뒤로" 단추를 대신 누른다
    /// (창 스크립트는 원래 단추만 알면 된다 — 화면마다 연결을 다시 하지 않는다).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ProxyButton : MonoBehaviour
    {
        [SerializeField] private Button target;

        private void Awake() => GetComponent<Button>().onClick.AddListener(() =>
        {
            if (target != null && target.interactable) target.onClick.Invoke();
        });
    }
}
