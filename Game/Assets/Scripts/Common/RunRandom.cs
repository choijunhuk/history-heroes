using System;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 판 시드 하나에서 갈라진 난수 3갈래 (Modes 2-4): ① 방 고르기 ② 적 생성 ③ 레벨업 카드.
    /// 서로 영향을 주지 않아서, 플레이어가 다른 카드를 골라도 방 구성은 그대로다 → 일일 도전 "모두 같은 판".
    /// 판이 시작될 때 Begin(시드). 시작하지 않았으면 시계로 아무 시드나 쓴다.
    /// </summary>
    public static class RunRandom
    {
        private static System.Random _rooms;
        private static System.Random _enemies;
        private static System.Random _cards;

        public static int Seed { get; private set; }
        public static bool Started => _rooms != null;

        public static void Begin(int seed)
        {
            Seed = seed;
            _rooms = new System.Random(Mix(seed, 0x51ED));
            _enemies = new System.Random(Mix(seed, 0x2B79));
            _cards = new System.Random(Mix(seed, 0x6A09));
        }

        /// <summary>시드를 정할 이유가 없는 판 (던전·서바이벌)</summary>
        public static int BeginRandom()
        {
            int seed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
            Begin(seed);
            return seed;
        }

        private static void Ensure()
        {
            if (_rooms == null) BeginRandom();
        }

        public static System.Random Rooms
        {
            get
            {
                Ensure();
                return _rooms;
            }
        }

        public static System.Random Enemies
        {
            get
            {
                Ensure();
                return _enemies;
            }
        }

        public static System.Random Cards
        {
            get
            {
                Ensure();
                return _cards;
            }
        }

        /// <summary>카드 난수 0~1 (UnityEngine.Random.value 대신)</summary>
        public static float Card01() => (float)Cards.NextDouble();

        /// <summary>카드 난수 [min, max)</summary>
        public static int CardRange(int min, int max) => max <= min ? min : Cards.Next(min, max);

        public static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        /// <summary>날짜·문자열 → 같은 시드 (string.GetHashCode는 실행마다 달라질 수 있어서)</summary>
        public static int Hash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s ?? string.Empty) h = (h ^ c) * 16777619;
                return h;
            }
        }

        private static int Mix(int seed, int salt)
        {
            unchecked
            {
                uint x = (uint)seed * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u;
                x ^= x >> 15;
                x *= 0x2C1B3C6Du;
                x ^= x >> 12;
                return (int)x;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _rooms = null;
            _enemies = null;
            _cards = null;
            Seed = 0;
        }
    }
}
