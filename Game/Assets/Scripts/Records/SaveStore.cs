using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary>판이 끝날 때마다 돌려 쓰는 백업 3개 (가장 최근 = 1)</summary>
        public const int RunBackups = 3;
        public string RunBackupPath(int i) => $"{MainPath}.run{i}";

        // ---------------------------------------------------------------- v1.0 해시 검사 (손쉬운 편집 막기)
        // 공개 저장소라 키는 숨길 수 없다. 진짜 막기는 서버 저장 (v1.0 갈래 2, LiveOps 15장)

        private static readonly byte[] IntegrityKey = Encoding.UTF8.GetBytes("history-heroes/save/v1");
        private static readonly Regex IntegrityField = new Regex("(\"integrity\"\\s*:\\s*\")([^\"]*)(\")", RegexOptions.CultureInvariant);

        private static string Hash(string text)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA256(IntegrityKey))
            {
                byte[] h = hmac.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>해시 칸을 비운 글 (쓸 때와 읽을 때 같은 글에서 해시를 낸다)</summary>
        private static string WithoutHash(string json, out string stored)
        {
            Match m = IntegrityField.Match(json);
            stored = m.Success ? m.Groups[2].Value : null;
            return m.Success ? json.Substring(0, m.Groups[2].Index) + json.Substring(m.Groups[2].Index + m.Groups[2].Length) : json;
        }

        /// <summary>글 안 해시 칸에 해시를 채운다</summary>
        private static string Sign(string json)
        {
            Match m = IntegrityField.Match(json);
            if (!m.Success) return json;
            string hash = Hash(json);
            return json.Substring(0, m.Groups[2].Index) + hash + json.Substring(m.Groups[2].Index + m.Groups[2].Length);
        }

        /// <summary>해시가 맞나. 해시 칸이 없거나 비었으면 옛 파일 → 통과</summary>
        public static bool IntegrityOk(string json)
        {
            string body = WithoutHash(json, out string stored);
            return string.IsNullOrEmpty(stored) || stored == Hash(body);
        }

        /// <summary>마지막 Load가 어디서 읽었고 무슨 문제가 있었는지 (로그·테스트용)</summary>
        public LoadReport LastLoad { get; private set; } = new LoadReport();

        /// <summary>
        /// 지금 본 파일이 "읽을 수 있는 정상 파일"인가. 망가진 본 파일을 백업 자리로 돌리면
        /// 정상 백업이 사라지므로, 망가졌으면 백업을 그대로 두고 본 파일만 바꾼다.
        /// </summary>
        private bool _mainIsValid = true;

        /// <summary>망가진 본 파일의 사본을 이미 남겼는가 (남기지 못했으면 지우지 않는다)</summary>
        private bool _brokenMainPreserved;

        /// <summary>읽은 임시 파일을 본 파일로 옮기지 못했다 → 그 임시 파일이 유일한 정상본이므로 덮어쓰면 안 된다</summary>
        private bool _tempIsOnlyCopy;

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
            data.integrity = string.Empty;
            string json = Sign(JsonUtility.ToJson(data, true));

            Directory.CreateDirectory(Folder);

            if (_tempIsOnlyCopy)
            {
                // 지난번에 못 옮긴 임시 파일(유일한 정상본)을 먼저 본 파일로 옮긴다. 또 실패하면 저장하지 않는다
                if (File.Exists(MainPath)) throw new IOException("본 파일과 복구한 임시 파일이 둘 다 있어 저장을 멈춤");
                File.Move(TempPath, MainPath);
                _tempIsOnlyCopy = false;
                _mainIsValid = true;
            }

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
                if (_mainIsValid)
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(MainPath, BackupPath);
                }
                else
                {
                    // 망가진 본 파일: 정상 백업은 그대로 두고, 망가진 파일은 사본으로 남긴 뒤 치운다.
                    // 사본을 못 남기면 지우지 않고 저장 실패로 돌린다 (원본 보존이 먼저)
                    if (!_brokenMainPreserved && PreserveBroken(MainPath) == null)
                    {
                        File.Delete(TempPath);
                        throw new IOException("망가진 저장 파일을 보존하지 못해서 저장을 멈춤");
                    }

                    File.Delete(MainPath);
                    _brokenMainPreserved = false;
                }
            }

            File.Move(TempPath, MainPath);
            _mainIsValid = true;
        }

        /// <summary>저장 파일을 읽는다. 파일이 하나도 없거나 전부 읽지 못하면 새 데이터를 돌려준다 (기존 파일은 그대로 둔다).</summary>
        public SaveData Load()
        {
            var report = new LoadReport();
            LastLoad = report;

            bool mainExists = File.Exists(MainPath);
            _mainIsValid = true;
            _brokenMainPreserved = false;
            _tempIsOnlyCopy = false;
            var candidates = new List<string> { MainPath };
            if (!mainExists) candidates.Add(TempPath); // 교체 도중에 꺼진 경우에만 임시 파일을 믿는다
            candidates.Add(BackupPath);
            for (int i = 1; i <= RunBackups; i++) candidates.Add(RunBackupPath(i));

            SaveData tampered = null;
            string tamperedPath = null;
            foreach (string path in candidates)
            {
                if (!File.Exists(path)) continue;

                if (TryRead(path, out SaveData data, out string problem, out bool hashFailed) && hashFailed)
                {
                    // 읽히지만 해시가 틀렸다 (손으로 고친 파일): 다른 정상본을 먼저 찾는다. 하나도 없으면 이걸 쓴다 (진행을 날리지 않게)
                    report.Problems.Add($"{Path.GetFileName(path)}: 저장 파일이 바뀜 (해시 불일치)");
                    report.PreservedCopies.Add(PreserveBroken(path));
                    if (tampered == null)
                    {
                        tampered = data;
                        tamperedPath = path;
                    }

                    if (path == MainPath) _mainIsValid = false;
                    continue;
                }

                if (data != null)
                {
                    report.Source = path;
                    report.FromBackup = path != MainPath;

                    // 교체 도중에 꺼져서 임시 파일만 완성본으로 남았다 → 바로 본 파일로 확정한다.
                    // 그대로 두면 다음 저장이 임시 파일을 새로 쓰면서(잘라내며) 유일한 정상 파일을 지운다.
                    if (path == TempPath && !File.Exists(MainPath))
                    {
                        try
                        {
                            File.Move(TempPath, MainPath);
                            report.Source = MainPath;
                        }
                        catch (Exception e)
                        {
                            // 읽은 데이터는 그대로 쓰되, 다음 저장이 이 임시 파일을 덮어쓰지 못하게 막는다
                            _tempIsOnlyCopy = true;
                            report.Problems.Add("임시 파일을 본 파일로 옮기지 못함: " + e.Message);
                        }
                    }

                    return data;
                }

                report.Problems.Add($"{Path.GetFileName(path)}: {problem}");
                string copy = PreserveBroken(path);
                report.PreservedCopies.Add(copy);
                if (path == MainPath)
                {
                    _mainIsValid = false;
                    _brokenMainPreserved = copy != null;
                }
            }

            if (tampered != null)
            {
                report.Source = tamperedPath;
                report.FromBackup = tamperedPath != MainPath;
                report.Tampered = true;
                tampered.integrityFailures++;
                return tampered;
            }

            report.IsFresh = true;
            return new SaveData();
        }

        /// <summary>판이 끝날 때: 지금 본 파일을 백업 1로, 1 → 2 → 3으로 민다 (Game 12-2 · LiveOps 20장 "최근 3개")</summary>
        public void RotateRunBackups()
        {
            if (!File.Exists(MainPath) || !_mainIsValid) return;
            string last = RunBackupPath(RunBackups);
            if (File.Exists(last)) File.Delete(last);
            for (int i = RunBackups - 1; i >= 1; i--)
            {
                if (File.Exists(RunBackupPath(i))) File.Move(RunBackupPath(i), RunBackupPath(i + 1));
            }

            File.Copy(MainPath, RunBackupPath(1), false);
        }

        private static bool TryRead(string path, out SaveData data, out string problem, out bool hashFailed)
        {
            data = null;
            hashFailed = false;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    problem = "빈 파일";
                    return false;
                }

                hashFailed = !IntegrityOk(json);

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
            if (data.schemaVersion < 2)
            {
                // 1 → 2: 재화·하트·계정 레벨·별이 생겼다. 옛 파일에는 없으므로 처음 값으로 채운다
                // (JsonUtility는 파일에 없는 칸을 기본값 0으로 두므로 하트·레벨을 직접 넣는다)
                data.hearts = 5;
                data.heartsUpdatedAt = 0;
                data.accountLevel = 1;
                data.accountExp = 0;
            }

            if (data.schemaVersion < 3)
            {
                // 2 → 3: 판 밖 성장이 생겼다. 모두 처음 상태 (장비 없음, 칸 강화 0, 영웅 Lv1 ★1). 옛 기록은 판 밖 성장 없음
                data.equipment = new EquipmentSave();
                data.heroProgress = new List<HeroProgress>();
                data.training = new List<int>();
                data.gacha = new GachaSave();
                data.shop = new ShopSave();
                data.runes = new RuneSave();
                data.relics = new RelicSave();
                foreach (RunRecord r in data.records ?? new List<RunRecord>()) r.meta = new MetaSnapshot();
            }

            if (data.schemaVersion < 4)
            {
                // 3 → 4 (v0.5): 서바이벌 기록·펫·미션은 처음 상태. 옛 기록은 펫 없음
                data.survival = new SurvivalSave();
                data.pets = new PetSave();
                data.missions = new MissionSave();
                foreach (RunRecord r in data.records ?? new List<RunRecord>())
                {
                    if (r.meta == null) continue;
                    r.meta.petId = null;
                    r.meta.petLevel = 0;
                    r.meta.petBond = false;
                }
            }

            data.schemaVersion = SaveData.CurrentSchemaVersion;
        }

        /// <summary>빠진 목록을 채우고 말이 안 되는 값을 바로잡는다 (손으로 고친 파일, 옛 파일 대비).</summary>
        private static void Normalize(SaveData data)
        {
            data.records ??= new List<RunRecord>();
            data.discoveredRecipes ??= new List<string>();
            data.defeatedBosses ??= new List<string>();
            data.stars ??= new List<string>();
            data.items ??= new List<ItemCount>();
            data.daily ??= new DailyCounters();
            data.daily.heartRefunds ??= new List<ItemCount>();
            data.unlockedHeroes ??= new List<string>();
            data.settings ??= new GameSettings();
            data.settings.shake = Math.Clamp(data.settings.shake, 0, 2);

            // v0.4 판 밖 성장
            data.equipment ??= new EquipmentSave();
            data.equipment.items ??= new List<EquipmentItem>();
            data.equipment.items.RemoveAll(i => i == null || string.IsNullOrEmpty(i.uid) || string.IsNullOrEmpty(i.dataId));
            data.equipment.slotLevels ??= new List<int>();
            while (data.equipment.slotLevels.Count < 3) data.equipment.slotLevels.Add(0);
            for (int i = 0; i < data.equipment.slotLevels.Count; i++) data.equipment.slotLevels[i] = Math.Clamp(data.equipment.slotLevels[i], 0, 40);
            data.equipment.loadouts ??= new List<HeroLoadout>();
            data.equipment.loadouts.RemoveAll(l => l == null || string.IsNullOrEmpty(l.heroId));
            foreach (HeroLoadout l in data.equipment.loadouts)
            {
                l.uids ??= new List<string>();
                while (l.uids.Count < 3) l.uids.Add("");
                for (int i = 0; i < l.uids.Count; i++) l.uids[i] ??= "";
            }

            if (data.equipment.nextUid < 1) data.equipment.nextUid = 1;
            data.equipment.bossRewardsGiven ??= new List<string>();
            data.heroProgress ??= new List<HeroProgress>();
            data.heroProgress.RemoveAll(h => h == null || string.IsNullOrEmpty(h.heroId));
            foreach (HeroProgress h in data.heroProgress)
            {
                h.level = Math.Clamp(h.level, 1, 30);
                h.stars = Math.Clamp(h.stars, 1, 5);
            }

            data.training ??= new List<int>();
            data.gacha ??= new GachaSave();
            data.gacha.log ??= new List<GachaLogEntry>();
            data.gacha.premiumPity = Math.Max(0, data.gacha.premiumPity);
            data.shop ??= new ShopSave();
            data.runes ??= new RuneSave();
            data.runes.owned ??= new List<RuneLevel>();
            data.runes.owned.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id) || r.level <= 0);
            foreach (RuneLevel r in data.runes.owned) r.level = Math.Clamp(r.level, 1, 3);
            data.runes.loadouts ??= new List<HeroRunes>();
            data.runes.loadouts.RemoveAll(h => h == null || string.IsNullOrEmpty(h.heroId));
            foreach (HeroRunes h in data.runes.loadouts)
            {
                h.ids ??= new List<string>();
                while (h.ids.Count < 2) h.ids.Add("");
                for (int i = 0; i < h.ids.Count; i++) h.ids[i] ??= "";
            }

            data.runes.choicesTaken ??= new List<string>();
            data.runes.choiceRunes ??= new List<string>();
            data.relics ??= new RelicSave();
            data.relics.owned ??= new List<RelicStar>();
            data.relics.owned.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id) || r.stars <= 0);
            foreach (RelicStar r in data.relics.owned) r.stars = Math.Clamp(r.stars, 1, 3);
            data.relics.sourcesClaimed ??= new List<string>();
            data.relics.heroDungeonClears ??= new List<string>();
            data.rift ??= new RiftSave();
            data.rift.rewardsClaimed ??= new List<int>();
            data.rift.checkpoint ??= new RiftCheckpoint();
            data.rift.checkpoint.skills ??= new List<RecordSkill>();
            data.rift.checkpoint.boosts ??= new List<int>();
            data.run ??= new RunSnapshot();
            data.run.skills ??= new List<RecordSkill>();
            data.run.boosts ??= new List<int>();
            data.run.fusions ??= new List<string>();
            data.run.rng ??= new List<string>();
            data.dailyChallenge ??= new DailySave();
            data.dailyChallenge.history ??= new List<DailyResult>();
            data.dailyChallenge.history.RemoveAll(h => h == null);
            data.missions ??= new MissionSave();
            data.missions.daily ??= new List<MissionProgress>();
            data.missions.daily.RemoveAll(m => m == null || string.IsNullOrEmpty(m.id));
            data.missions.weekly ??= new List<MissionProgress>();
            data.missions.weekly.RemoveAll(m => m == null || string.IsNullOrEmpty(m.id));
            foreach (MissionProgress m in data.missions.daily) m.seen ??= new List<string>();
            foreach (MissionProgress m in data.missions.weekly) m.seen ??= new List<string>();
            data.missions.dailyChests ??= new List<int>();
            data.missions.weeklyChests ??= new List<int>();
            data.missions.counters ??= new MissionCounters();
            data.missions.counters.noLegendChapters ??= new List<int>();
            data.missions.counters.chapter3Heroes ??= new List<string>();
            data.missions.counters.bossWinRecords ??= new List<string>();
            data.missions.achievements ??= new List<AchievementClaim>();
            data.pets ??= new PetSave();
            data.pets.owned ??= new List<PetLevel>();
            data.pets.owned.RemoveAll(p => p == null || string.IsNullOrEmpty(p.id));
            foreach (PetLevel p in data.pets.owned) p.level = Mathf.Max(1, p.level);
            data.pets.loadouts ??= new List<HeroPet>();
            data.pets.loadouts.RemoveAll(l => l == null || string.IsNullOrEmpty(l.heroId));
            data.survival ??= new SurvivalSave();
            data.survival.best ??= new List<HeroBest>();
            data.survival.best.RemoveAll(b => b == null || string.IsNullOrEmpty(b.heroId));
            data.survival.clearHeroes ??= new List<string>();
            if (string.IsNullOrEmpty(data.selectedHero)) data.selectedHero = "khan";
            if (data.accountLevel < 1) data.accountLevel = 1;
            if (data.hearts < 0) data.hearts = 0;
            if (data.recordSlots < 1) data.recordSlots = 3;
            if (data.unlockedChapter < 1) data.unlockedChapter = 1;

            data.records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id));
            foreach (RunRecord r in data.records)
            {
                r.skills ??= new List<RecordSkill>();
                r.bossesDefeated ??= new List<string>();
                r.bossWins ??= new List<BossWin>();
                r.meta ??= new MetaSnapshot();
                r.meta.equipment ??= new List<EquippedItem>();
                r.meta.training ??= new List<int>();
                r.meta.relics ??= new List<RelicStar>();
                if (r.meta.heroLevel < 1) r.meta.heroLevel = 1;
                if (r.meta.heroStars < 1) r.meta.heroStars = 1;
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
            /// <summary>정상본이 없어서 해시가 틀린 파일을 썼다</summary>
            public bool Tampered;
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> PreservedCopies = new List<string>();
        }
    }
}
