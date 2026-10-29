using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>연속 타격 중 플레이어나 표적을 따라가지 않도록 발동 순간의 부채꼴을 고정한다.</summary>
    public sealed class FixedArcState
    {
        public Vector2 Center { get; private set; }
        public Vector2 Direction { get; private set; }
        public float Radius { get; private set; }
        public float Degrees { get; private set; }

        public void Set(Vector2 center, Vector2 direction, float radius, float degrees)
        {
            Center = center;
            Direction = direction.sqrMagnitude < 0.0001f ? Vector2.up : direction.normalized;
            Radius = radius;
            Degrees = degrees;
        }

        public bool Contains(Vector2 point)
        {
            Vector2 offset = point - Center;
            return offset.sqrMagnitude <= Radius * Radius &&
                (offset.sqrMagnitude < 0.0001f || Vector2.Angle(Direction, offset) <= Degrees * 0.5f);
        }
    }
}
