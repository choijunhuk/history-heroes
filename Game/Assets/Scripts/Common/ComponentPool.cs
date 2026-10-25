using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 컴포넌트 오브젝트 풀. 투사체·적·이펙트를 미리 만들어두고 재사용한다.
    /// 서바이벌 모드에서 수백 개가 생겼다 사라져도 생성·파괴 비용이 들지 않게. (GameDesign 8-5)
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _inactive = new Stack<T>();

        public ComponentPool(T prefab, Transform parent, int prewarm = 0)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < prewarm; i++)
            {
                T item = Object.Instantiate(_prefab, _parent);
                item.gameObject.SetActive(false);
                _inactive.Push(item);
            }
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T item = _inactive.Count > 0 ? _inactive.Pop() : Object.Instantiate(_prefab, _parent);
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            if (item == null || !item.gameObject.activeSelf) return;
            item.gameObject.SetActive(false);
            _inactive.Push(item);
        }
    }
}
