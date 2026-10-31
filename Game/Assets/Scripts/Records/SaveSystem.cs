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

        /// <summary>판이 끝날 때 백업을 돌린다 (최근 3개). 실패해도 게임은 계속</summary>
        public static void RotateRunBackups()
        {
            try
            {
                Store.RotateRunBackups();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SaveSystem] 판 백업 실패: " + e.Message);
            }
        }

        /// <summary>저장하지 못한 변경이 남아 있는가 (실패 후 다시 시도할 것)</summary>
        public static bool IsDirty => _dirty;

        /// <summary>마지막 저장 실패 이유 (화면에 보여줄 때)</summary>
        public static string LastError { get; private set; }

        public const string UnsupportedNewerSaveText = "더 새 버전에서 만든 저장 파일이에요. 게임을 업데이트한 뒤 다시 열어 주세요. 저장 파일은 그대로 보관했어요.";

        public static bool UnsupportedNewerSave
        {
            get
            {
                _ = Data;
                return Store.LastLoad.UnsupportedNewerSchema;
            }
        }

        public static SaveData Data
        {
            get
            {
                if (_data == null)
                {
                    SaveLifecycle.Ensure();
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
            LastError = null;
        }

        /// <summary>바뀐 게 있다고 표시만 한다. 실제 쓰기는 SaveIfDirty / Save에서.</summary>
        public static void MarkDirty() => _dirty = true;

        /// <summary>저장한다. 실패하면 false (게임은 멈추지 않고, 다음 안전한 시점에 다시 시도한다)</summary>
        public static bool Save()
        {
            try
            {
                if (UnsupportedNewerSave)
                {
                    LastError = UnsupportedNewerSaveText;
                    return false;
                }
                Store.Save(Data);
                _dirty = false;
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                _dirty = true;
                LastError = e.Message;
                Debug.LogError("[SaveSystem] 저장 실패: " + e.Message);
                return false;
            }
        }

        public static bool SaveIfDirty() => !_dirty || Save();

        public enum CommitResult
        {
            Rejected,   // 조건이 안 돼서 아무것도 안 바뀜
            Saved,      // 바뀌고 저장됨
            SaveFailed, // 저장을 못 해서 바뀌기 전으로 되돌림
        }

        /// <summary>
        /// 바꾸고 바로 저장한다. **저장이 실패하면 바꾸기 전으로 되돌린다** — 뽑기 결과를 보고 앱을 다시 켜서 또 뽑는 길,
        /// 재화만 빠진 것처럼 보이는 상태를 막는다 (Codex v0.4 2번). change가 false면 아무것도 바꾸지 않은 것으로 본다.
        /// </summary>
        public static CommitResult Commit(Func<bool> change)
        {
            if (UnsupportedNewerSave) return CommitResult.Rejected;
            string before = JsonUtility.ToJson(Data);
            if (!change()) return CommitResult.Rejected;
            if (Save()) return CommitResult.Saved;
            JsonUtility.FromJsonOverwrite(before, Data);
            return CommitResult.SaveFailed;
        }

        public const string SaveFailedText = "<color=#FF7A6B>저장하지 못해서 되돌렸어요. 기기 저장 공간을 확인하고 다시 해 주세요</color>";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _store = null;
            _data = null;
            _dirty = false;
            LastError = null;
        }

        /// <summary>
        /// 앱이 뒤로 가거나 꺼질 때 남은 변경을 저장한다 (모바일은 OnApplicationQuit을 믿으면 안 된다, 12-2).
        /// 장면이 바뀌어도 남는 숨은 오브젝트 하나.
        /// </summary>
        private sealed class SaveLifecycle : MonoBehaviour
        {
            private static SaveLifecycle _instance;

            public static void Ensure()
            {
                if (_instance != null || !Application.isPlaying) return;
                var go = new GameObject("SaveLifecycle") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SaveLifecycle>();
            }

            private void OnApplicationPause(bool paused)
            {
                if (!paused) return;
                SaveIfDirty();
                Common.LocalNotices.Reschedule(Data); // 하트·순찰 알림 다시 예약 (v1.0)
            }

            private void OnApplicationFocus(bool focused)
            {
                if (!focused) SaveIfDirty();
            }

            private void OnApplicationQuit()
            {
                SaveIfDirty();
                Common.LocalNotices.Reschedule(Data);
            }

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ResetStatics() => _instance = null;
        }
    }
}
