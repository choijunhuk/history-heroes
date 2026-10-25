using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HistoryHeroes.Records
{
    /// <summary>
    /// 저장 파일 읽기·쓰기 (GameDesign 12-2 "저장", 12-3 체크리스트).
    /// - 쓰기: 임시 파일에 끝까지 쓴 다음 교체한다. 지금 파일은 백업 1개로 남긴다.
    ///   → 쓰는 도중에 앱이 꺼져도 이전 파일이 살아 있다.
    /// - 읽기: 본 파일 → (교체 도중에 꺼졌으면) 임시 파일 → 백업 순서로 시도한다.
    ///   읽지 못한 파일은 지우거나 덮어쓰지 않고 "broken" 사본으로 따로 보존한다.
    /// </summary>
    public sealed class SaveStore
    {
        public const string FileName = "save.json";

        public string Folder { get; }
        public string MainPath => Path.Combine(Folder, FileName);
        public string TempPath => MainPath + ".tmp";
        public string BackupPath => MainPath + ".bak";

        /// <summary>마지막 Load가 어디서 읽었고 무슨 문제가 있었는지 (로그·테스트용)</summary>
        public LoadReport LastLoad { get; private set; } = new LoadReport();

        public SaveStore(string folder)
        {
            Folder = folder;
        }

        public static SaveStore ForDevice() => new SaveStore(Application.persistentDataPath);

        public void Save(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            data.schemaVersion = SaveData.CurrentSchemaVersion;
            data.savedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string json = JsonUtility.ToJson(data, true);

            Directory.CreateDirectory(Folder);

            // 1) 임시 파일에 끝까지 쓰고 디스크까지 내려보낸다
            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            // 2) 다 써졌으면 교체: 지금 파일 → 백업, 임시 파일 → 본 파일
            //    (두 단계 사이에 꺼지면 본 파일이 없고 임시 파일이 완성본이다 → Load가 임시 파일을 읽는다)
            if (File.Exists(MainPath))
            {
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                File.Move(MainPath, BackupPath);
            }

            File.Move(TempPath, MainPath);
        }

        /// <summary>저장 파일을 읽는다. 파일이 하나도 없거나 전부 읽지 못하면 새 데이터를 돌려준다 (기존 파일은 그대로 둔다).</summary>
        public SaveData Load()
        {
            var report = new LoadReport();
            LastLoad = report;

            bool mainExists = File.Exists(MainPath);
            var candidates = new List<string> { MainPath };
            if (!mainExists) candidates.Add(TempPath); // 교체 도중에 꺼진 경우에만 임시 파일을 믿는다
            candidates.Add(BackupPath);

            foreach (string path in candidates)
            {
                if (!File.Exists(path)) continue;

                if (TryRead(path, out SaveData data, out string problem))
                {
                    report.Source = path;
                    report.FromBackup = path != MainPath;
                    return data;
                }

                report.Problems.Add($"{Path.GetFileName(path)}: {problem}");
                report.PreservedCopies.Add(PreserveBroken(path));
            }

            report.IsFresh = true;
            return new SaveData();
        }

        private static bool TryRead(string path, out SaveData data, out string problem)
        {
            data = null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    problem = "빈 파일";
                    return false;
                }

                if (!json.Contains("\"schemaVersion\""))
                {
                    problem = "저장 구조 버전이 없음";
                    return false;
                }

                data = JsonUtility.FromJson<SaveData>(json);
                if (data == null)
                {
                    problem = "읽은 결과가 비어 있음";
                    return false;
                }

                if (data.schemaVersion > SaveData.CurrentSchemaVersion)
                {
                    problem = $"더 새 버전의 게임이 만든 파일 (구조 {data.schemaVersion} > {SaveData.CurrentSchemaVersion})";
                    data = null;
                    return false;
                }

                if (data.schemaVersion < 1)
                {
                    problem = $"알 수 없는 구조 버전 {data.schemaVersion}";
                    data = null;
                    return false;
                }

                Migrate(data);
                Normalize(data);
                problem = null;
                return true;
            }
            catch (Exception e)
            {
                problem = e.GetType().Name + ": " + e.Message;
                data = null;
                return false;
            }
        }

        /// <summary>옛 구조를 지금 구조로 옮겨 담는다. 구조 버전을 올릴 때 여기에 단계별로 추가한다.</summary>
        private static void Migrate(SaveData data)
        {
            // 지금은 구조 1 하나뿐
            data.schemaVersion = SaveData.CurrentSchemaVersion;
        }

        /// <summary>빠진 목록을 채우고 말이 안 되는 값을 바로잡는다 (손으로 고친 파일, 옛 파일 대비).</summary>
        private static void Normalize(SaveData data)
        {
            data.records ??= new List<RunRecord>();
            data.discoveredRecipes ??= new List<string>();
            data.defeatedBosses ??= new List<string>();
            if (data.recordSlots < 1) data.recordSlots = 3;
            if (data.unlockedChapter < 1) data.unlockedChapter = 1;

            data.records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id));
            foreach (RunRecord r in data.records)
            {
                r.skills ??= new List<RecordSkill>();
                r.bossesDefeated ??= new List<string>();
                r.skills.RemoveAll(s => s == null || string.IsNullOrEmpty(s.id));
            }
        }

        /// <summary>읽지 못한 파일을 지우지 않고 사본으로 남긴다. 이미 같은 이름이 있으면 번호를 붙인다.</summary>
        private string PreserveBroken(string path)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                string target = $"{MainPath}.broken-{stamp}";
                for (int i = 2; File.Exists(target); i++) target = $"{MainPath}.broken-{stamp}-{i}";
                File.Copy(path, target, false);
                return target;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveStore] 읽지 못한 저장 파일을 보존하지 못함: {path} ({e.Message})");
                return null;
            }
        }

        public sealed class LoadReport
        {
            /// <summary>실제로 읽은 파일 (새로 시작이면 null)</summary>
            public string Source;
            public bool FromBackup;
            public bool IsFresh;
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> PreservedCopies = new List<string>();
        }
    }
}
