using System;
using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Stats;
using UnityEngine;

namespace HistoryHeroes.Progress
{
    /// <summary>적이 죽으면 보석을 떨어뜨리고, 먹으면 판 안 레벨을 올린다.</summary>
    public sealed class ExperienceSystem : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private ExperienceGem gemPrefab;

        private ComponentPool<ExperienceGem> _gems;
        private readonly List<ExperienceGem> _active = new List<ExperienceGem>();

        public PlayerLevel Level { get; } = new PlayerLevel();

        /// <summary>살아 있는 보석 상한 (0 = 없음). 넘으면 새 보석을 가장 가까운 보석에 합친다 (서바이벌, GameDesign 8-5)</summary>
        public int MaxGems { get; set; }

        /// <summary>보석 경험치 배수 (서바이벌은 적이 훨씬 많아서 낮춘다 — SurvivalData.expMultiplier)</summary>
        public float ExpMultiplier { get; set; } = 1f;
        public int Kills { get; private set; }

        /// <summary>판 이어하기</summary>
        public void RestoreKills(int kills) => Kills = Mathf.Max(0, kills);

        public event Action<int> LeveledUp;

        private void Awake()
        {
            _gems = new ComponentPool<ExperienceGem>(gemPrefab, transform, prewarm: 64);
            Level.LeveledUp += level => LeveledUp?.Invoke(level);
        }

        public void Register(EnemyBase enemy)
        {
            enemy.Killed -= OnKilled;
            enemy.Killed += OnKilled;
        }

        public void GrantLevelUp() => Level.GrantLevel();

        /// <summary>방을 깨면 남은 보석을 모두 끌어온다 (줍느라 돌아다니지 않게).</summary>
        public void AttractAll()
        {
            for (int i = 0; i < _active.Count; i++) _active[i].Attract();
        }

        private void OnKilled(EnemyBase enemy)
        {
            Kills++;
            float value = enemy.Data.experience * (enemy.IsElite ? 5f : 1f) * ExpMultiplier;
            if (MaxGems > 0 && _active.Count >= MaxGems && MergeIntoNearest(enemy.transform.position, value)) return;
            ExperienceGem gem = _gems.Get(enemy.transform.position, Quaternion.identity);
            gem.Drop(player.transform, value, OnCollected);
            _active.Add(gem);
        }

        private bool MergeIntoNearest(Vector2 at, float value)
        {
            ExperienceGem best = null;
            float bestSqr = float.MaxValue;
            foreach (ExperienceGem g in _active)
            {
                float d = ((Vector2)g.transform.position - at).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = g;
                }
            }

            if (best == null) return false;
            best.Absorb(value);
            return true;
        }

        public int ActiveGems => _active.Count;
        public IReadOnlyList<ExperienceGem> Gems => _active;

        private void OnCollected(ExperienceGem gem, float value)
        {
            _active.Remove(gem);
            _gems.Release(gem);
            Level.Add(value, player.Stats.Get(StatType.ExpGain));
        }
    }
}
