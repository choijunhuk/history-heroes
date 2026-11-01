using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>가까운 적에게 잠깐 표식을 보이고 기본 공격의 추가 피해 판정은 영웅에게 맡긴다.</summary>
    public sealed class OwlEyeBehaviour : ActiveSkillBehaviour
    {
        [SerializeField] private Color markColor = new Color(0.95f, 0.79f, 0.42f, 0.55f);

        private readonly List<EnemyBase> _chosen = new List<EnemyBase>(3);
        private readonly List<GameObject> _visuals = new List<GameObject>(3);

        protected override bool Activate()
        {
            if (player.Health.IsDead) return false;
            Vector2 origin = player.transform.position;
            float rangeSquared = data.RadiusAt(level) * data.RadiusAt(level);
            _chosen.Clear();
            for (int slot = 0; slot < data.CountAt(level); slot++)
            {
                EnemyBase nearest = null;
                float best = rangeSquared;
                foreach (EnemyBase enemy in EnemyBase.Alive)
                {
                    if (enemy == null || enemy.Health.IsDead || _chosen.Contains(enemy)) continue;
                    float distance = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                    if (distance < best) { best = distance; nearest = enemy; }
                }
                if (nearest == null) break;
                _chosen.Add(nearest);
            }
            if (_chosen.Count == 0) return false;

            ClearVisuals();
            player.BeginOwlMarks(this);
            foreach (EnemyBase enemy in _chosen)
            {
                Health target = enemy.Health;
                player.AddOwlMark(target, data.DurationAt(level), data.CountAt(level));
                var marker = new GameObject("OwlMark");
                marker.transform.SetParent(enemy.transform, false);
                marker.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                marker.transform.localScale = Vector3.one * 0.8f;
                var sprite = marker.AddComponent<SpriteRenderer>();
                sprite.sprite = PlaceholderSprites.RimCircle;
                sprite.color = markColor;
                sprite.sortingOrder = 8;
                _visuals.Add(marker);
                StartCoroutine(ExpireMarker(marker, target, target.Life, data.DurationAt(level)));
            }
            return true;
        }

        private IEnumerator ExpireMarker(GameObject marker, Health target, int life, float duration)
        {
            float until = Time.time + duration;
            while (Time.time < until && marker != null && target != null && !target.IsDead && target.Life == life)
                yield return null;
            if (marker != null) Destroy(marker);
            _visuals.Remove(marker);
        }

        private void ClearVisuals()
        {
            StopAllCoroutines();
            foreach (GameObject marker in _visuals) if (marker != null) Destroy(marker);
            _visuals.Clear();
        }

        public override void Dispose()
        {
            ClearVisuals();
            if (player != null) player.ClearOwlMarks(this);
            base.Dispose();
        }

        protected override void OnDestroy()
        {
            ClearVisuals();
            if (player != null) player.ClearOwlMarks(this);
            base.OnDestroy();
        }
    }
}
