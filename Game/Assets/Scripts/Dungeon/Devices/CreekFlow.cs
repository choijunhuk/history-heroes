using HistoryHeroes.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 1구간 개울 보이기 (확장 1차 2장): 물 띠 + 긴 쪽으로 흐르는 밝은 물결 줄 (검토 10번 "읽히게").
    /// 판정은 주인의 BoxCollider2D (Default 레이어 = 몸만 막고 탄은 넘어간다)
    /// </summary>
    public sealed class CreekFlow : MonoBehaviour
    {
        private const int Ripples = 4;
        private const float Speed = 0.9f;

        private Vector2 _size;
        private Transform[] _ripples;
        private float _offset;
        private bool _alongX;

        public void Setup(Vector2 size, Material water, Material flow)
        {
            _size = size;
            _alongX = size.x >= size.y;
            Mesh quad = Resources.GetBuiltinResource<Mesh>(Stage3D.BuiltinName(Stage3D.Shape.Floor));

            // 물 띠 (바닥보다 아주 조금 위). 2D 그림 시험판이면 코덱스 물 그림을 반복해 깐다
            Sprite water2D = Art2D.Enabled ? Art2D.Library.water : null;
            if (water2D != null)
            {
                var w = new GameObject("Water2D");
                w.transform.SetParent(transform, false);
                w.transform.localPosition = new Vector3(0f, 0f, -0.012f);
                float k = 2.5f / Mathf.Max(0.01f, water2D.bounds.size.y); // 그림 한 장 = 2.5 유닛
                w.transform.localScale = new Vector3(k, k, 1f);
                var ws = w.AddComponent<SpriteRenderer>();
                ws.sprite = water2D;
                ws.drawMode = SpriteDrawMode.Tiled;
                ws.size = size / k;
                ws.sortingOrder = -1500;
            }
            else
            {
                MeshRenderer bed = Quad("Water", quad, water, new Vector3(0f, 0f, -0.012f), new Vector3(size.x, size.y, 1f));
                if (water == null) bed.material.color = new Color(0.22f, 0.5f, 0.75f, 0.85f);
            }

            // 흐르는 물결 줄 (가는 줄 몇 개가 긴 쪽으로 미끄러져 돈다)
            _ripples = new Transform[water2D != null ? 0 : Ripples]; // 2D: 물 그림에 물결이 있어서 흰 줄은 뺀다
            float across = _alongX ? size.y : size.x;
            for (int i = 0; i < Ripples; i++)
            {
                Vector3 scale = _alongX ? new Vector3(0.12f, across * 0.7f, 1f) : new Vector3(across * 0.7f, 0.12f, 1f);
                MeshRenderer r = Quad($"Ripple{i}", quad, flow, new Vector3(0f, 0f, -0.02f), scale);
                if (flow == null) r.material.color = new Color(0.75f, 0.9f, 1f, 0.5f);
                _ripples[i] = r.transform;
            }

            Place();
        }

        private MeshRenderer Quad(string name, Mesh mesh, Material mat, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private void Update()
        {
            _offset += Speed * Time.deltaTime;
            Place();
        }

        private void Place()
        {
            if (_ripples == null) return;
            float length = _alongX ? _size.x : _size.y;
            float lane = (_alongX ? _size.y : _size.x) * 0.22f;
            for (int i = 0; i < _ripples.Length; i++)
            {
                float u = Mathf.Repeat(_offset + i * length / _ripples.Length, length) - length * 0.5f;
                float v = (i % 2 == 0 ? 1f : -1f) * lane;
                _ripples[i].localPosition = _alongX ? new Vector3(u, v, -0.02f) : new Vector3(v, u, -0.02f);
            }
        }
    }
}
