using System;
using UnityEngine;

namespace HistoryHeroes.Records
{
    /// <summary>
    /// 게임 전체가 쓰는 저장 데이터 하나. 처음 쓸 때 파일에서 읽고, Save()로 쓴다.
    /// 저장은 안전한 시점(방 클리어, 판 끝, 기록 저장, 보스 격파)에만 한다 (GameDesign 12-2).
    /// 테스트는 Use()로 임시 폴더의 저장 장치를 끼운다.
    /// </summary>
    public static class SaveSystem
    {
        private static SaveStore _store;
        private static SaveData _data;
        private static bool _dirty;

        public static SaveStore Store => _store ??= SaveStore.ForDevice();

        public static SaveData Data
        {
            get
            {
                if (_data == null)
                {
                    _data = Store.Load();
                    SaveStore.LoadReport report = Store.LastLoad;
                    if (report.Problems.Count > 0)
                    {
                        Debug.LogWarning("[SaveSystem] 저장 파일 문제: " + string.Join(" / ", report.Problems) +
                                         (report.FromBackup ? " → 백업에서 읽음" : report.IsFresh ? " → 새로 시작 (원본은 보존)" : ""));
                    }
                }

                return _data;
            }
        }

        /// <summary>다른 저장 장치로 바꾼다 (테스트용). 데이터는 다음에 읽을 때 새로 읽는다.</summary>
        public static void Use(SaveStore store)
        {
            _store = store;
            _data = null;
            _dirty = false;
        }

        /// <summary>바뀐 게 있다고 표시만 한다. 실제 쓰기는 SaveIfDirty / Save에서.</summary>
        public static void MarkDirty() => _dirty = true;

        public static void Save()
        {
            try
            {
                Store.Save(Data);
                _dirty = false;
            }
            catch (Exception e)
            {
                // 저장 실패로 게임이 멈추면 안 된다. 다음 안전한 시점에 다시 시도한다.
                _dirty = true;
                Debug.LogError("[SaveSystem] 저장 실패: " + e.Message);
            }
        }

        public static void SaveIfDirty()
        {
            if (_dirty) Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _store = null;
            _data = null;
            _dirty = false;
        }
    }
}
