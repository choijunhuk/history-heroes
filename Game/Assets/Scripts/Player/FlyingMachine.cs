using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using UnityEngine;

namespace HistoryHeroes.Player
{
    /// <summary>
    /// 다빈치 작업대의 비행 기계 (확장: 새 영웅): 영웅 둘레 위쪽에 떠서 따라다니고, **영웅이 멈춰 있을 때만** 영웅이 노리는 적을 쏜다 (공격력 35%).
    /// 날개는 코드로 퍼덕인다 (블렌더 Wing_R · Wing_L, 경첩이 원점). 20초 뒤 연기와 함께 사라진다
    /// </summary>
    public sealed class FlyingMachine : MonoBehaviour
    {
        private const float Height = 1.6f;
        private const float FlapSpeed = 14f;
        private const float FlapAngle = 28f;

        private PlayerController _owner;
        private int _slot;
        private float _until;
        private float _damage;
        private float _interval;
        private float _nextShot;
        private Transform _wingR, _wingL;
        private Quaternion _wingRBase, _wingLBase;
        private Transform _body;

        public static FlyingMachine Create(PlayerController owner, GameObject model, int slot, float life, float damage, float fireRate)
        {
            var go = new GameObject("FlyingMachine");
            go.transform.position = owner.transform.position;
            var m = go.AddComponent<FlyingMachine>();
            m._owner = owner;
            m._slot = slot;
            m._until = Time.time + life;
            m._damage = damage;
            m._interval = 1f / Mathf.Max(0.1f, fireRate);
            m._nextShot = Time.time + 0.5f;

            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            m._body = body;
            GameObject inst = Dungeon.RoomDevices.FitModel(body, model, 1.1f);
            if (inst != null)
            {
                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.EndsWith("Wing_R")) m._wingR = t;
                    else if (t.name.EndsWith("Wing_L")) m._wingL = t;
                }

                if (m._wingR != null) m._wingRBase = m._wingR.localRotation;
                if (m._wingL != null) m._wingLBase = m._wingL.localRotation;
            }
            else
            {
                var sr = body.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderSprites.Circle;
                sr.color = new Color(0.85f, 0.7f, 0.45f);
                body.localScale = Vector3.one * 0.5f;
            }

            FxBursts.Puff(go.transform.position + Vector3.back * Height, new Color(0.95f, 0.85f, 0.6f), 1.2f);
            return m;
        }

        private void Update()
        {
            if (_owner == null || Time.time >= _until || _owner.Health.IsDead)
            {
                FxBursts.Puff(transform.position + Vector3.back * Height, new Color(0.7f, 0.7f, 0.7f), 1f);
                Destroy(gameObject);
                return;
            }

            if (GamePause.IsPaused) return;
            float t = Time.time;
            // 영웅 위쪽 둘레 (자리마다 다른 각도) + 둥실
            float angle = t * 0.8f + _slot * Mathf.PI;
            Vector2 goal = (Vector2)_owner.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * 1.3f;
            Vector2 pos = Vector2.Lerp(transform.position, goal, 1f - Mathf.Exp(-6f * Time.deltaTime));
            transform.position = new Vector3(pos.x, pos.y, -Height - 0.15f * Mathf.Sin(t * 3f + _slot));

            // 날개 퍼덕 (오른쪽 +, 왼쪽 −)
            float flap = Mathf.Sin(t * FlapSpeed) * FlapAngle;
            if (_wingR != null) _wingR.localRotation = _wingRBase * Quaternion.Euler(0f, 0f, flap);
            if (_wingL != null) _wingL.localRotation = _wingLBase * Quaternion.Euler(0f, 0f, -flap);

            Health target = _owner.HelperTarget(_owner.Stats.Get(Stats.StatType.Range) + 1f);
            if (target != null)
            {
                Vector2 to = (Vector2)target.transform.position - pos;
                if (to.sqrMagnitude > 0.01f) _body.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
            }

            // 영웅이 멈춰 있을 때만 쏜다 (원칙: 기본 공격은 멈춰야 나간다 — 도우미도 같다)
            if (target == null || !_owner.Stop.IsStopped || t < _nextShot) return;
            _nextShot = t + _interval;
            Vector2 dir = ((Vector2)target.transform.position - pos).normalized;
            _owner.LaunchHelperShot(pos, dir, _damage);
        }
    }
}
