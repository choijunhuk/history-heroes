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
        /// <summary>저장을 못 읽었거나 오래된 백업에서 읽었다 → 클라우드와 다시 맞출 때까지 남는다 (Play 중에는 PlayerPrefs에도)</summary>
        private static bool _recheckCloud;
        /// <summary>계정 삭제 뒤 이 기기 진행을 아직 다 못 지웠다 (Play 중에는 PlayerPrefs에도 → 다시 켜면 이어서 지운다)</summary>
        private static bool _wipePending;

        private const string RecheckCloudKey = "hh.save.recheckCloud";
        private const string WipePendingKey = "hh.save.wipePending";

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

        /// <summary>파일에 저장이 끝났다 (계정이 클라우드에 올릴 차례를 센다)</summary>
        public static event Action Saved;

        public const string UnsupportedNewerSaveText = "더 새 버전에서 만든 저장 파일이에요. 게임을 업데이트한 뒤 다시 열어 주세요. 저장 파일은 그대로 보관했어요.";

        public static bool UnsupportedNewerSave
        {
            get
            {
                _ = Data;
                return Store.LastLoad.UnsupportedNewerSchema;
            }
        }

        /// <summary>
        /// 이번 실행에서 최근 저장을 잃었다: 저장 파일이 있었는데 전부 못 읽어 새 게임으로 시작했거나(원본은 broken 사본으로 보존),
        /// 본 파일 대신 더 오래된 백업(.bak · 판 백업)에서 읽었다. 이 저장을 클라우드에 올리면 더 최근 클라우드 진행 · 결제 장부를 덮는다.
        /// </summary>
        public static bool LostSaveOnLoad
        {
            get
            {
                _ = Data;
                SaveStore.LoadReport r = Store.LastLoad;
                return (r.IsFresh && r.Problems.Count > 0) || r.FromOlderCopy;
            }
        }

        /// <summary>
        /// 최근 저장을 잃은 뒤(LostSaveOnLoad) 아직 클라우드와 맞추지 않았다 — 다시 켜도 남는다 (그사이 새로 저장해 본 파일이 멀쩡해져도).
        /// 계정은 이게 켜져 있으면 클라우드를 덮지 않고 고르기(클라우드 복구)를 띄운다. 맞추면 CloudRechecked.
        /// </summary>
        public static bool CloudRecheckNeeded
        {
            get
            {
                _ = Data;
                return _recheckCloud;
            }
        }

        public static void CloudRechecked() => SetFlag(ref _recheckCloud, RecheckCloudKey, false);

        /// <summary>계정 삭제 뒤 이 기기 진행을 아직 다 못 지웠다 → 로비에 들어가지 않고, 저장도 지우기가 끝날 때까지 막는다</summary>
        public static bool WipePending
        {
            get
            {
                _ = Data;
                return _wipePending;
            }
        }

        public const string WipeFailedText = "계정은 지웠지만 이 기기의 진행을 아직 다 지우지 못했어요. 저장 공간을 확인한 뒤 게임을 다시 열어 주세요 (열 때 이어서 지워요).";

        /// <summary>표시를 메모리와 (Play 중이면) PlayerPrefs에 같이 둔다. EditMode 테스트는 에디터 PlayerPrefs를 건드리지 않는다</summary>
        private static void SetFlag(ref bool field, string key, bool on)
        {
            field = on;
            if (!Application.isPlaying) return;
            if (on) PlayerPrefs.SetInt(key, 1);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save(); // 안드로이드는 Save 전에 꺼지면 사라진다
        }

        private static bool Flag(string key) => Application.isPlaying && PlayerPrefs.GetInt(key, 0) == 1;

        /// <summary>
        /// 메모리의 저장이 통째로 바뀔 때마다 하나씩 커진다 (다시 읽기 · 저장 장치 바꾸기 · 클라우드 진행으로 바꾸기 · 계정 삭제).
        /// 서버에 결제를 묻는 동안 저장이 바뀌었으면 그 답을 다른 진행에 넣지 않으려고 본다 (IapService.AskServer)
        /// </summary>
        public static int Generation { get; private set; }

        public static SaveData Data
        {
            get
            {
                if (_data == null)
                {
                    SaveLifecycle.Ensure();
                    if (_wipePending || Flag(WipePendingKey))
                    {
                        // 지운 계정의 진행은 메모리에도 다시 읽지 않는다. 남은 파일을 지우고 새 게임으로 (실패하면 표시가 남아 저장이 막힌다)
                        _wipePending = true;
                        _data = SaveStore.NewGame();
                        Generation++;
                        FinishWipe();
                        return _data;
                    }

                    _data = Store.Load();
                    Generation++;
                    SaveStore.LoadReport report = Store.LastLoad;
                    if (report.Problems.Count > 0)
                    {
                        Debug.LogWarning("[SaveSystem] 저장 파일 문제: " + string.Join(" / ", report.Problems) +
                                         (report.FromBackup ? " → 백업에서 읽음" : report.IsFresh ? " → 새로 시작 (원본은 보존)" : ""));
                    }

                    if ((report.IsFresh && report.Problems.Count > 0) || report.FromOlderCopy) SetFlag(ref _recheckCloud, RecheckCloudKey, true);
                    else _recheckCloud = Flag(RecheckCloudKey);
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
            _recheckCloud = false;
            _wipePending = false;
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

                if (_wipePending)
                {
                    // 지운 계정의 파일이 남아 있는 동안은 아무것도 쓰지 않는다 (옛 백업 옆에 새 진행이 섞이지 않게).
                    // 다시 지우기가 되면 그게 이번 저장이다 (지우고 지금 메모리를 새 본 파일로 쓴다)
                    if (!FinishWipe())
                    {
                        _dirty = true;
                        return false;
                    }
                }
                else Store.Save(Data);

                _dirty = false;
                LastError = null;
                Saved?.Invoke();
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

        /// <summary>
        /// 계정 전환 (클라우드 진행 고르기): 저장을 통째로 바꾼다. 지금 본 파일은 판 백업(.run1)으로 먼저 남기고,
        /// 저장하면서 본 파일이 .bak로도 남는다. 저장하지 못하면 바꾸기 전으로 되돌리고 false.
        /// </summary>
        public static bool Replace(SaveData data)
        {
            if (data == null || UnsupportedNewerSave) return false;
            SaveIfDirty(); // 로비에서 막 바뀐 것까지 백업에 들어가게
            RotateRunBackups();
            SaveData before = _data;
            _data = data;
            Generation++;
            if (Save()) return true;
            _data = before;
            return false;
        }

        /// <summary>
        /// 계정 삭제 (서버 계정을 지운 뒤): 지우기 표시를 먼저 남기고(다시 켜면 이어서), 메모리를 새 게임으로 바꾼 다음 파일 · 백업을 지운다.
        /// 파일을 다 못 지워도 옛 진행은 메모리에 남지 않고, 표시가 지워질 때까지 저장 · 로비 진입이 막힌다. 동의 기록도 없어져 다시 묻는다.
        /// </summary>
        public static bool ResetToNewGame()
        {
            SetFlag(ref _wipePending, WipePendingKey, true);
            _data = SaveStore.NewGame();
            Generation++;
            _dirty = false;
            CloudRechecked();
            return FinishWipe();
        }

        /// <summary>남은 저장 파일을 모두 지우고 새 게임을 쓴 뒤에만 표시를 지운다</summary>
        private static bool FinishWipe()
        {
            try
            {
                Store.DeleteAllFiles();
                Store.Save(_data);
            }
            catch (Exception e)
            {
                LastError = WipeFailedText;
                Debug.LogError("[SaveSystem] 지운 계정의 저장 파일을 지우지 못함 (다음에 이어서): " + e.Message);
                return false;
            }

            SetFlag(ref _wipePending, WipePendingKey, false);
            LastError = null;
            return true;
        }

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
            _recheckCloud = false;
            _wipePending = false;
            LastError = null;
            Saved = null;
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
