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
        public int Kills { get; private set; }

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

        /// <summary>방을 깨면 남은 보석을 모두 끌어온다 (줍느라 돌아다니지 않게).</summary>
        public void AttractAll()
        {
            for (int i = 0; i < _active.Count; i++) _active[i].Attract();
        }

        private void OnKilled(EnemyBase enemy)
        {
            Kills++;
            float value = enemy.Data.experience * (enemy.IsElite ? 5f : 1f);
            ExperienceGem gem = _gems.Get(enemy.transform.position, Quaternion.identity);
            gem.Drop(player.transform, value, OnCollected);
            _active.Add(gem);
        }

        private void OnCollected(ExperienceGem gem, float value)
        {
            _active.Remove(gem);
            _gems.Release(gem);
            Level.Add(value, player.Stats.Get(StatType.ExpGain));
        }
    }
}
