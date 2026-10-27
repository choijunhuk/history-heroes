using System;
using UnityEngine;

namespace HistoryHeroes.Common
{
    /// <summary>
    /// 판 시드 하나에서 갈라진 난수 4갈래 (Modes 2-4): ① 방 고르기 ② 적 생성 ③ 레벨업 카드 ④ 갈림길·특수 방 (확장 1차 3장).
    /// 서로 영향을 주지 않아서, 플레이어가 다른 카드를 골라도 방 구성은 그대로다 → 일일 도전 "모두 같은 판".
    /// 판이 시작될 때 Begin(시드). 시작하지 않았으면 시계로 아무 시드나 쓴다.
    /// </summary>
    public static class RunRandom
    {
        private static StreamRandom _rooms;
        private static StreamRandom _enemies;
        private static StreamRandom _cards;
        private static StreamRandom _forks;

        public static int Seed { get; private set; }
        public static bool Started => _rooms != null;

        public static void Begin(int seed)
        {
            Seed = seed;
            _rooms = new StreamRandom(Mix(seed, 0x51ED));
            _enemies = new StreamRandom(Mix(seed, 0x2B79));
            _cards = new StreamRandom(Mix(seed, 0x6A09));
            _forks = new StreamRandom(Mix(seed, 0x3C6E));
        }

        /// <summary>판 이어하기: 4갈래의 지금 상태 (방 · 적 · 카드 · 갈림길 순서)</summary>
        public static System.Collections.Generic.List<string> Capture()
        {
            Ensure();
            return new System.Collections.Generic.List<string> { _rooms.State.ToString(), _enemies.State.ToString(), _cards.State.ToString(), _forks.State.ToString() };
        }

        /// <summary>판 이어하기: 저장해 둔 상태로 되돌린다 → 껐다 켜도 같은 카드·같은 적</summary>
        public static bool Restore(System.Collections.Generic.IReadOnlyList<string> states)
        {
            Ensure();
            if (states == null || states.Count < 3) return false;
            if (!ulong.TryParse(states[0], out ulong a) || !ulong.TryParse(states[1], out ulong b) || !ulong.TryParse(states[2], out ulong c)) return false;
            _rooms.State = a;
            _enemies.State = b;
            _cards.State = c;
            if (states.Count > 3 && ulong.TryParse(states[3], out ulong d)) _forks.State = d; // 옛 저장(3갈래)은 시드에서 새로
            return true;
        }

        /// <summary>
        /// 방에 들어갈 때: 적 난수를 (씨앗, 방 번호)로 새로 시작한다 (코드 검토 2026-10-28 1번).
        /// 앞 방에서 적을 몇 번 뽑았든(갈림길의 도전 방 · 플레이 차이) 이 방의 적 자리·속성은 같다
        /// </summary>
        public static void BeginRoom(int room)
        {
            Ensure();
            _enemies = new StreamRandom(Mix(Mix(Seed, 0x2B79), 0x7F4A + room * 0x10DCD));
        }

        /// <summary>
        /// 특수 방 보상 카드(도전 성공 · 저주받은 엘리트)는 카드 난수 대신 갈림길 난수로 뽑는다 → 문을 골라도 뒤 카드 난수가 밀리지 않는다.
        /// using (RunRandom.CardDetour()) { ... } 안에서 Cards가 갈림길 난수를 돌려준다
        /// </summary>
        public static IDisposable CardDetour()
        {
            Ensure();
            _detour = _forks;
            return new DetourScope();
        }

        private static StreamRandom _detour;

        private sealed class DetourScope : IDisposable
        {
            public void Dispose() => _detour = null;
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

        /// <summary>갈림길 · 특수 방 (확장 1차 3장): 판 시작 때 양쪽을 미리 정한다. 어느 문을 골라도 방·적·카드 난수는 그대로</summary>
        public static System.Random Forks
        {
            get
            {
                Ensure();
                return _forks;
            }
        }

        public static System.Random Cards
        {
            get
            {
                Ensure();
                return _detour ?? _cards; // 특수 방 보상 카드는 갈림길 난수 (CardDetour)
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

    /// <summary>
    /// 상태(64비트 하나)를 저장·복원할 수 있는 난수 (SplitMix64). System.Random을 상속해서 쓰던 곳은 그대로 쓴다.
    /// System.Random 자체는 속 상태를 꺼낼 수 없어서 판 이어하기에 못 쓴다.
    /// </summary>
    public sealed class StreamRandom : System.Random
    {
        public ulong State;

        public StreamRandom(int seed) : base(0)
        {
            unchecked
            {
                State = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL ^ 0xD1B54A32D192ED03UL;
            }
        }

        private ulong NextU64()
        {
            unchecked
            {
                ulong z = State += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>[0, 1)</summary>
        protected override double Sample() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);

        public override double NextDouble() => Sample();

        public override int Next() => (int)(NextU64() >> 33);

        public override int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return maxValue <= 1 ? 0 : (int)(Sample() * maxValue);
        }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            long range = (long)maxValue - minValue;
            return range <= 1 ? minValue : (int)(minValue + (long)(Sample() * range));
        }

        public override void NextBytes(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)NextU64();
        }
    }
}
