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
        private static readonly Color WaterColor = new Color(0.08f, 0.32f, 0.38f, 0.52f);
        private static readonly Color RippleColor = new Color(0.52f, 0.86f, 0.84f, 0.27f);
        private static readonly Color BankColor = new Color(0.28f, 0.42f, 0.36f, 0.42f);

        private Vector2 _size;
        private Transform[] _ripples;
        private float _offset;
        private bool _alongX;

        public void Setup(Vector2 size, Material water, Material flow)
        {
            _size = size;
            _alongX = size.x >= size.y;
            Mesh quad = Resources.GetBuiltinResource<Mesh>(Stage3D.BuiltinName(Stage3D.Shape.Floor));

            // 물 띠 (바닥보다 아주 조금 위)
            MeshRenderer bed = Quad("Water", quad, water, new Vector3(0f, 0f, -0.012f), new Vector3(size.x, size.y, 1f));
            Tint(bed, WaterColor);

            // 물의 양끝만 얇게 잡아 바닥에 놓인 지형으로 읽히게 한다.
            float across = _alongX ? size.y : size.x;
            for (int i = 0; i < 2; i++)
            {
                float edge = (i == 0 ? -1f : 1f) * (across * 0.5f - 0.03f);
                Vector3 position = _alongX ? new Vector3(0f, edge, -0.017f) : new Vector3(edge, 0f, -0.017f);
                Vector3 scale = _alongX ? new Vector3(size.x, 0.06f, 1f) : new Vector3(0.06f, size.y, 1f);
                Tint(Quad($"Bank{i}", quad, flow, position, scale), BankColor);
            }

            // 짧고 가는 물결이 개울의 긴 쪽을 따라 흐른다.
            _ripples = new Transform[Ripples];
            float length = _alongX ? size.x : size.y;
            float dash = Mathf.Min(0.5f, length * 0.32f);
            float thickness = Mathf.Min(0.055f, across * 0.12f);
            for (int i = 0; i < Ripples; i++)
            {
                Vector3 scale = _alongX ? new Vector3(dash, thickness, 1f) : new Vector3(thickness, dash, 1f);
                MeshRenderer r = Quad($"Ripple{i}", quad, flow, new Vector3(0f, 0f, -0.02f), scale);
                Tint(r, RippleColor);
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

        private static void Tint(MeshRenderer renderer, Color color)
        {
            if (renderer.sharedMaterial == null) renderer.material.color = color;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
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
