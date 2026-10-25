#if UNITY_EDITOR || DEVELOPMENT_BUILD
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Dev
{
    /// <summary>
    /// 빌드 비교 시나리오 측정기 (QaTools.Scenario가 붙인다, GameDesign 6-8 · 3-3 검증용).
    /// 매 프레임 확인하므로 처치 시간이 정확하다 (밖에서 몇 초마다 물어보는 방식은 오차가 컸다).
    /// - 플레이어 체력을 계속 채운다 (생존은 따로 본다. 여기서는 처치 속도만)
    /// - "정지": 가장 가까운 적이 사거리 밖이면 다가가고, 사거리 안이면 멈춰서 쏜다
    ///   (제자리에만 서 있으면 거리를 두는 궁병을 영원히 못 맞힌다. 실제 플레이어도 다가간다)
    /// - "이동만": 시작 자리를 가운데 두고 좌우로 계속 움직인다 (멈추지 않음 → 기본 공격 없음, 벽에 안 닿음)
    /// </summary>
    public sealed class ScenarioMonitor : MonoBehaviour
    {
        public const float TimeLimit = 120f;
        private const float TurnInterval = 0.6f;
        private const float ApproachRangeRatio = 0.85f;

        public string ScenarioName { get; private set; }
        public bool Finished { get; private set; }
        public bool Cleared { get; private set; }
        public float Elapsed { get; private set; }

        private PlayerController _player;
        private bool _moving;
        private float _start;
        private float _nextTurn;
        private float _direction = 1f;

        public void Begin(string scenarioName, bool moving, PlayerController player)
        {
            ScenarioName = scenarioName;
            _moving = moving;
            _player = player;
            _start = Time.time;
            _nextTurn = Time.time + TurnInterval * 0.5f; // 처음엔 반만 가서 시작 자리를 가운데로
            FloatingJoystick.DevOverride = moving ? Vector2.right : (Vector2?)null;
        }

        private void Update()
        {
            if (Finished || _player == null) return;

            _player.Health.Heal(1e9f);
            Elapsed = Time.time - _start;

            if (EnemyBase.Alive.Count == 0 || Elapsed >= TimeLimit)
            {
                Cleared = EnemyBase.Alive.Count == 0;
                Finished = true;
                FloatingJoystick.DevOverride = null;
                return;
            }

            if (_moving)
            {
                if (Time.time >= _nextTurn)
                {
                    _direction = -_direction;
                    FloatingJoystick.DevOverride = new Vector2(_direction, 0f);
                    _nextTurn = Time.time + TurnInterval;
                }
            }
            else
            {
                FloatingJoystick.DevOverride = ApproachIfOutOfRange();
            }
        }

        private Vector2? ApproachIfOutOfRange()
        {
            Vector2 from = _player.transform.position;
            EnemyBase nearest = null;
            float best = float.MaxValue;
            foreach (EnemyBase e in EnemyBase.Alive)
            {
                float d = ((Vector2)e.transform.position - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = e;
                }
            }

            if (nearest == null) return null;
            float range = _player.Stats.Get(StatType.Range) * ApproachRangeRatio;
            Vector2 toEnemy = (Vector2)nearest.transform.position - from;
            return toEnemy.magnitude > range ? toEnemy.normalized : (Vector2?)null;
        }

        private void OnDestroy()
        {
            if (!Finished) FloatingJoystick.DevOverride = null;
        }
    }
}
#endif
