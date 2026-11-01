using System;
using System.IO;
using HistoryHeroes.Account;
using HistoryHeroes.Progress;
using HistoryHeroes.Records;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HistoryHeroes.Tests
{
    /// <summary>계정 (LiveOps 3장) — 서버 없이 도는 판정: 고르기 요약 · 올리기 규칙 · 오류 글 · 클라우드 글 검사 · 저장 바꾸기/지우기</summary>
    public sealed class AccountTests
    {
        private string _folder;
        private SaveStore _previousStore;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "hh-account-" + Guid.NewGuid().ToString("N"));
            _previousStore = SaveSystem.Store;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Use(_previousStore);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static SaveData Progressed(int chapter, int gemsFree, int gemsPaid, int heroLevel, long savedAt)
        {
            SaveData d = SaveStore.NewGame();
            d.unlockedChapter = chapter;
            d.gemsFree = gemsFree;
            d.gemsPaid = gemsPaid;
            d.selectedHero = "khan";
            d.Hero("khan").level = heroLevel;
            d.Hero("other").level = 30; // 고른 영웅이 아닌 영웅 레벨은 요약에 안 나온다
            d.savedAtUnixSeconds = savedAt;
            return d;
        }

        [Test]
        public void Summary_ShowsChapterSelectedHeroLevelGemsAndTime_AndRecommendsMoreProgress()
        {
            long t = new DateTimeOffset(2026, 11, 2, 12, 10, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            AccountSummary device = AccountSummary.From(Progressed(3, 800, 20, 14, t));
            AccountSummary cloud = AccountSummary.From(Progressed(4, 300, 10, 17, t - 3600));

            Assert.AreEqual(3, device.chapter);
            Assert.AreEqual(14, device.heroLevel);
            Assert.AreEqual(820, device.gems, "보석은 무료분 + 유료분");
            Assert.AreEqual("2026-11-02T12:10:00.0000000Z", device.savedAtUtc);
            string text = device.Text(id => id == "khan" ? "칭기즈 칸" : null);
            StringAssert.StartsWith("3구간 · 칭기즈 칸 Lv14 · 보석 820 · ", text);
            StringAssert.Contains(DateTimeOffset.FromUnixTimeSeconds(t).ToLocalTime().ToString("yyyy-MM-dd HH:mm"), text);
            StringAssert.Contains("khan Lv17", cloud.Text(null), "이름을 모르면 ID");

            Assert.IsTrue(AccountSummary.CloudRecommended(device, cloud), "구간이 높은 쪽 (저장 시각보다 먼저)");
            Assert.IsFalse(AccountSummary.CloudRecommended(cloud, device));
            Assert.IsFalse(AccountSummary.CloudRecommended(device, device), "같으면 이 기기");
        }

        [Test]
        public void Summary_RoundTripsAsMetaJson()
        {
            AccountSummary s = AccountSummary.From(Progressed(2, 5, 0, 3, 100));
            AccountSummary back = UnityEngine.JsonUtility.FromJson<AccountSummary>(UnityEngine.JsonUtility.ToJson(s));
            Assert.AreEqual(s.Text(null), back.Text(null));
            Assert.AreEqual(SaveData.CurrentSchemaVersion, back.schemaVersion);
        }

        [Test]
        public void ShouldUpload_OnlyLinked_NotPending_Changed_AndThrottled()
        {
            const double last = 100;
            Assert.IsFalse(AccountRules.ShouldUpload(AccountState.Guest, false, true, true, 1000, last), "게스트는 이 기기에만");
            Assert.IsFalse(AccountRules.ShouldUpload(AccountState.Offline, false, true, true, 1000, last));
            Assert.IsFalse(AccountRules.ShouldUpload(AccountState.Busy, false, true, true, 1000, last));
            Assert.IsFalse(AccountRules.ShouldUpload(AccountState.Linked, true, true, true, 1000, last), "진행 고르기 전에는 클라우드를 덮지 않는다");
            Assert.IsFalse(AccountRules.ShouldUpload(AccountState.Linked, false, false, true, 1000, last), "바뀐 게 없으면 안 올린다");
            Assert.IsFalse(AccountRules.ShouldUpload(AccountState.Linked, false, true, false, last + 59.9, last), "60초 안에는 자동으로 다시 안 올린다");
            Assert.IsTrue(AccountRules.ShouldUpload(AccountState.Linked, false, true, false, last + 60, last));
            Assert.IsTrue(AccountRules.ShouldUpload(AccountState.Linked, false, true, true, last + 1, last), "뒤로 갈 때 · 연결 직후 · 지금 저장은 바로");
            Assert.IsTrue(AccountRules.ShouldUpload(AccountState.Linked, false, true, false, 0, double.NegativeInfinity), "처음 한 번은 바로");
        }

        [Test]
        public void FitsCloud_LeavesRoomForEscaping()
        {
            Assert.IsTrue(AccountRules.FitsCloud(new string('a', 100_000), "{}"));
            Assert.IsFalse(AccountRules.FitsCloud(new string('a', AccountRules.CloudLimitBytes / 2 + 1), null));
        }

        [Test]
        public void ErrorText_PlainKorean_WithShortCodeForTokenProblems()
        {
            Assert.AreEqual("인터넷 연결을 확인해 주세요", AccountRules.ErrorText(AccountRules.TransportError, "Network Error"));
            Assert.AreEqual("인터넷 연결을 확인해 주세요", AccountRules.ErrorText(AccountRules.Timeout, null));
            StringAssert.Contains("바빠요", AccountRules.ErrorText(AccountRules.TooManyRequests, ""));
            StringAssert.Contains("바빠요", AccountRules.ErrorText(AccountRules.ServiceUnavailable, ""));
            StringAssert.Contains("(코드 10008)", AccountRules.ErrorText(AccountRules.InvalidProvider, "invalid provider"));
            StringAssert.Contains("(코드 52)", AccountRules.ErrorText(AccountRules.TokenExpired, ""));
            StringAssert.Contains("문의", AccountRules.ErrorText(AccountRules.BannedUser, ""));
            StringAssert.Contains("(코드 12345)", AccountRules.ErrorText(12345, "?"));

            // UGS "Common errors"의 토큰 검사 문구 → 원인 한 단어 (대시보드 공급자 설정을 고칠 때 본다)
            Assert.AreEqual("로그인 정보를 확인하지 못했어요 (코드 51 · aud)", AccountRules.ErrorText(51, "invalid audience"));
            StringAssert.EndsWith("· iss)", AccountRules.ErrorText(0, "Invalid issuer"));
            StringAssert.EndsWith("· sig)", AccountRules.ErrorText(0, "invalid signature"));
            StringAssert.EndsWith("· exp)", AccountRules.ErrorText(0, "token is expired"));
            foreach (string text in new[] { AccountRules.ErrorText(1, "invalid audience"), AccountRules.ErrorText(3, "") })
                Assert.IsFalse(text.Contains("Exception"), "영어 예외 글을 그대로 보이지 않는다");
        }

        [Test]
        public void LoginErrorText_ShowsKakaoCode()
        {
            Assert.AreEqual("카카오 로그인에 실패했어요 (코드 KOE009)", AccountRules.LoginErrorText("카카오", "KOE009"));
            StringAssert.Contains("폰에서만", AccountRules.LoginErrorText("카카오", "NotSupported"));
            StringAssert.Contains("오래", AccountRules.LoginErrorText("카카오", "Timeout"));
            StringAssert.Contains("(코드 ?)", AccountRules.LoginErrorText("카카오", null));
        }

        [Test]
        public void StatusText_ForEachState()
        {
            StringAssert.Contains("게스트", AccountRules.StatusText(AccountState.Guest, null));
            StringAssert.Contains("카카오 연결됨", AccountRules.StatusText(AccountState.Linked, "카카오"));
            StringAssert.Contains("오프라인", AccountRules.StatusText(AccountState.Offline, null));
            StringAssert.Contains("연결 중", AccountRules.StatusText(AccountState.Connecting, null));
        }

        [Test]
        public void PrivacyVersion2_AsksPlayersWhoAgreedToVersion1Again()
        {
            Assert.GreaterOrEqual(Consent.PrivacyVersion, 2, "계정 · 클라우드 저장을 넣으며 개인정보 처리방침을 고쳤다 (결제로 3판)");
            SaveData save = SaveStore.NewGame();
            Consent.Accept(save, DateTime.UtcNow);
            save.consent.privacyVersion = 1;
            Assert.IsTrue(Consent.NeedsConsent(save));
        }

        [Test]
        public void CloudJson_ParsesLikeFile_AndCatchesTamperingAndNewerGames()
        {
            SaveData d = Progressed(3, 100, 0, 5, 1);
            d.gold = 1234;
            string json = SaveStore.Serialize(d);

            Assert.AreEqual(SaveStore.ParseResult.Ok, SaveStore.Parse(json, out SaveData back, out _));
            Assert.AreEqual(1234, back.gold);
            Assert.AreEqual(d.userId, back.userId);
            Assert.AreEqual(0, back.integrityFailures);

            string edited = json.Replace("\"gold\": 1234", "\"gold\": 999999");
            Assert.AreNotEqual(json, edited);
            Assert.AreEqual(SaveStore.ParseResult.Tampered, SaveStore.Parse(edited, out SaveData tampered, out _));
            Assert.AreEqual(999999, tampered.gold, "진행은 날리지 않고");
            Assert.AreEqual(1, tampered.integrityFailures, "표시를 남긴다");

            string newer = SaveStore.Sign(json.Replace($"\"schemaVersion\": {SaveData.CurrentSchemaVersion}", $"\"schemaVersion\": {SaveData.CurrentSchemaVersion + 1}"));
            Assert.AreEqual(SaveStore.ParseResult.NewerSchema, SaveStore.Parse(newer, out _, out _));
            Assert.AreEqual(SaveStore.ParseResult.Broken, SaveStore.Parse("not json", out _, out _));
            Assert.AreEqual(SaveStore.ParseResult.Broken, SaveStore.Parse("", out _, out _));

            // 옛 구조 글도 파일처럼 옮겨 담는다
            Assert.AreEqual(SaveStore.ParseResult.Ok, SaveStore.Parse(SaveStore.Sign("{\"schemaVersion\":11,\"integrity\":\"\",\"gold\":7}"), out SaveData old, out _));
            Assert.AreEqual(SaveData.CurrentSchemaVersion, old.schemaVersion);
            Assert.AreEqual(7, old.gold);
        }

        [Test]
        public void Replace_KeepsDeviceProgressInBackups()
        {
            var store = new SaveStore(_folder);
            SaveSystem.Use(store);
            SaveSystem.Data.gold = 11;
            Assert.IsTrue(SaveSystem.Save());

            SaveData cloud = Progressed(4, 0, 0, 9, 1);
            cloud.gold = 22;
            Assert.IsTrue(SaveSystem.Replace(cloud));

            Assert.AreEqual(22, new SaveStore(_folder).Load().gold);
            StringAssert.Contains("\"gold\": 11", File.ReadAllText(store.RunBackupPath(1)), "판 백업 1에 이 기기 진행");
            StringAssert.Contains("\"gold\": 11", File.ReadAllText(store.BackupPath));
        }

        [Test]
        public void ResetToNewGame_WipesAllCopies_AndAsksConsentAgain()
        {
            var store = new SaveStore(_folder);
            SaveSystem.Use(store);
            SaveData data = SaveSystem.Data;
            data.gold = 55;
            Consent.Accept(data, DateTime.UtcNow);
            string oldUser = data.userId;
            Assert.IsTrue(SaveSystem.Save());
            SaveSystem.RotateRunBackups();
            Assert.IsTrue(SaveSystem.Save());
            File.WriteAllText(store.MainPath + ".broken-x", "old");

            Assert.IsTrue(SaveSystem.ResetToNewGame());

            Assert.AreEqual(1, Directory.GetFiles(_folder, SaveStore.FileName + "*").Length, "새 본 파일 하나만");
            SaveData fresh = new SaveStore(_folder).Load();
            Assert.AreEqual(0, fresh.gold);
            Assert.IsTrue(Consent.NeedsConsent(fresh), "지운 계정은 동의부터 새로");
            Assert.IsFalse(string.IsNullOrEmpty(SaveSystem.Data.userId), "새 사용자 ID가 바로 생긴다 (정보 화면 · 삭제 요청용)");
            Assert.AreNotEqual(oldUser, SaveSystem.Data.userId);
        }

        [Test]
        public void CloudChangedElsewhere_OnlyWhenCloudHasSaveWithOtherUploadId()
        {
            Assert.IsFalse(AccountRules.CloudChangedElsewhere(false, null, null), "클라우드가 비었으면 올려도 된다");
            Assert.IsFalse(AccountRules.CloudChangedElsewhere(false, "a", "b"));
            Assert.IsFalse(AccountRules.CloudChangedElsewhere(true, "a", "a"), "이 기기가 마지막으로 올린 그대로");
            Assert.IsTrue(AccountRules.CloudChangedElsewhere(true, "b", "a"), "다른 기기가 그 뒤에 올렸다");
            Assert.IsTrue(AccountRules.CloudChangedElsewhere(true, "b", ""), "이 기기는 맞춘 적이 없다 (저장을 못 읽어 새로 시작 등)");
            Assert.IsTrue(AccountRules.CloudChangedElsewhere(true, null, null), "번호 없는 옛 meta는 덮지 않는다");
        }

        private static string Jwt(string payloadJson)
        {
            string b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return "eyJhbGciOiJSUzI1NiJ9." + b64 + ".sig";
        }

        [Test]
        public void JwtSubject_ReadsSubFromUnpaddedBase64Url()
        {
            Assert.AreEqual("4321098765", AccountRules.JwtSubject(Jwt("{\"iss\":\"https://kauth.kakao.com\",\"sub\":\"4321098765\",\"aud\":\"x\"}")));
            Assert.AreEqual("12", AccountRules.JwtSubject(Jwt("{\"sub\":\"12\"}")));
            Assert.IsNull(AccountRules.JwtSubject("not-a-jwt"));
            Assert.IsNull(AccountRules.JwtSubject(null));
        }

        [Test]
        public void SameExternalUser_NeverUnlinksWhenUnsure()
        {
            Assert.IsTrue(AccountRules.SameExternalUser("4321", "4321"));
            Assert.IsTrue(AccountRules.SameExternalUser("4321", "oidc-kakao|4321"), "접두어가 붙은 외부 ID도 같은 계정");
            Assert.IsFalse(AccountRules.SameExternalUser("4321", "1234"));
            Assert.IsFalse(AccountRules.SameExternalUser("21", "4321"), "끝자리만 같은 다른 계정");
            Assert.IsFalse(AccountRules.SameExternalUser(null, "4321"));
            Assert.IsFalse(AccountRules.SameExternalUser("4321", null));
            StringAssert.Contains("다른 카카오 계정", AccountRules.LoginErrorText("카카오", "OtherAccount"));
        }

        [Test]
        public void LostSaveOnLoad_OnlyWhenFilesExistedButNoneReadable()
        {
            SaveSystem.Use(new SaveStore(_folder));
            Assert.IsFalse(SaveSystem.LostSaveOnLoad, "처음 설치는 잃은 게 아니다");

            Directory.CreateDirectory(_folder);
            var store = new SaveStore(_folder);
            File.WriteAllText(store.MainPath, "{ 깨진 글");
            SaveSystem.Use(store);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("저장 파일 문제"));
            Assert.IsTrue(SaveSystem.LostSaveOnLoad, "파일이 있었는데 못 읽어 새로 시작 → 계정은 빈 저장을 올리지 않는다");
        }

        [Test]
        public void Replace_SavesPendingChangesIntoTheBackupFirst()
        {
            var store = new SaveStore(_folder);
            SaveSystem.Use(store);
            SaveSystem.Data.gold = 1;
            Assert.IsTrue(SaveSystem.Save());
            SaveSystem.Data.gold = 33; // 로비에서 바뀌고 아직 안 쓴 것
            SaveSystem.MarkDirty();

            Assert.IsTrue(SaveSystem.Replace(Progressed(2, 0, 0, 1, 1)));
            StringAssert.Contains("\"gold\": 33", File.ReadAllText(store.RunBackupPath(1)));
        }

        [Test]
        public void Summary_CarriesIdsForDeletionRequests()
        {
            SaveData d = SaveStore.NewGame();
            Assert.AreEqual(d.userId, AccountSummary.From(d).userId);
        }

        [Test]
        public void OlderBackupRestore_NeedsCloudRecheck_ButFinishedTempPromotionDoesNot()
        {
            var store = new SaveStore(_folder);
            SaveSystem.Use(store);
            SaveSystem.Data.gold = 1;
            Assert.IsTrue(SaveSystem.Save());
            SaveSystem.Data.gold = 2;
            Assert.IsTrue(SaveSystem.Save()); // .bak = gold 1 (더 오래됨)
            File.WriteAllText(store.MainPath, "{ 깨진 글");

            SaveSystem.Use(new SaveStore(_folder));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("저장 파일 문제"));
            Assert.AreEqual(1, SaveSystem.Data.gold, "오래된 백업에서 읽었다");
            Assert.IsTrue(SaveSystem.LostSaveOnLoad);
            Assert.IsTrue(SaveSystem.CloudRecheckNeeded, "이 오래된 진행으로 클라우드를 덮지 않게");
            SaveSystem.CloudRechecked();
            Assert.IsFalse(SaveSystem.CloudRecheckNeeded);

            // 교체 도중 꺼져 남은 임시 파일 = 같은 저장의 완성본 → 잃은 게 아니다
            string other = _folder + "-tmp";
            try
            {
                var tmpStore = new SaveStore(other);
                Directory.CreateDirectory(other);
                SaveData d = SaveStore.NewGame();
                d.gold = 9;
                File.WriteAllText(tmpStore.TempPath, SaveStore.Serialize(d));
                SaveSystem.Use(tmpStore);
                Assert.AreEqual(9, SaveSystem.Data.gold);
                Assert.IsFalse(SaveSystem.LostSaveOnLoad);
                Assert.IsFalse(SaveSystem.CloudRecheckNeeded);
            }
            finally
            {
                if (Directory.Exists(other)) Directory.Delete(other, true);
            }
        }

        [Test]
        public void ResetToNewGame_WhenFilesCantBeWritten_DropsOldProgressAndBlocksSavingUntilRetried()
        {
            var store = new SaveStore(_folder);
            SaveSystem.Use(store);
            SaveSystem.Data.gold = 77;
            Consent.Accept(SaveSystem.Data, DateTime.UtcNow);
            Assert.IsTrue(SaveSystem.Save());
            string blocker = store.TempPath; // 임시 파일 자리에 폴더 → 새 게임을 쓸 수 없다
            Directory.CreateDirectory(blocker);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("지우지 못함"));
            Assert.IsFalse(SaveSystem.ResetToNewGame());
            Assert.IsTrue(SaveSystem.WipePending);
            Assert.AreEqual(0, SaveSystem.Data.gold, "지운 계정의 진행은 메모리에도 남지 않는다");
            Assert.IsTrue(Consent.NeedsConsent(SaveSystem.Data));
            SaveSystem.Data.gold = 5;
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("지우지 못함"));
            Assert.IsFalse(SaveSystem.Save(), "지우기가 끝나기 전에는 아무것도 쓰지 않는다");
            Assert.AreEqual(SaveSystem.WipeFailedText, SaveSystem.LastError);

            Directory.Delete(blocker);
            Assert.IsTrue(SaveSystem.Save(), "다시 시도하면 지우고 새 게임을 쓴다");
            Assert.IsFalse(SaveSystem.WipePending);
            Assert.AreEqual(5, new SaveStore(_folder).Load().gold);
            Assert.AreEqual(1, Directory.GetFiles(_folder, SaveStore.FileName + "*").Length);
        }

        [Test]
        public void SyncSettled_GuestAlways_LinkedOnlyAfterCleanCheck()
        {
            Assert.IsTrue(AccountRules.SyncSettled(AccountState.Guest, false, false, false, false), "게스트는 이 기기 장부가 전부");
            Assert.IsTrue(AccountRules.SyncSettled(AccountState.Linked, true, false, false, false));
            Assert.IsFalse(AccountRules.SyncSettled(AccountState.Linked, false, false, false, false), "이번 실행에서 아직 클라우드를 안 봤다");
            Assert.IsFalse(AccountRules.SyncSettled(AccountState.Linked, true, true, false, false), "충돌 · 고르기 미룸");
            Assert.IsFalse(AccountRules.SyncSettled(AccountState.Linked, true, false, true, false), "계정 전환 고르기 중");
            Assert.IsFalse(AccountRules.SyncSettled(AccountState.Linked, true, false, false, true), "저장을 잃어 클라우드 복구 확인 전");
            Assert.IsFalse(AccountRules.SyncSettled(AccountState.Offline, true, false, false, false), "연결됐는지 모른다");
            Assert.IsFalse(AccountRules.SyncSettled(AccountState.Connecting, true, false, false, false));
            Assert.IsFalse(AccountRules.SyncSettled(AccountState.Busy, true, false, false, false));
        }

        [Test]
        public void UnsettledSync_BlocksStarterAndDoesNotPromiseFirstDouble_GemPacksStillSell()
        {
            SaveData d = SaveStore.NewGame();
            Economy.IapCatalog.Item starter = Economy.IapCatalog.Find(Economy.IapCatalog.StarterPack);
            Economy.IapCatalog.Item gems = Economy.IapCatalog.Find("gems_t1");

            Assert.IsTrue(Economy.IapLedger.Sellable(d, starter, true));
            Assert.IsFalse(Economy.IapLedger.Sellable(d, starter, false), "동기화 전에는 계정당 1번 상품을 안 판다");
            Assert.IsFalse(Economy.IapLedger.Sellable(d, gems, false), "동기화 전에는 보석도 안 판다 (첫 구매 2배를 오래된 장부로 판정하지 않게)");
            Assert.IsTrue(Economy.IapLedger.Sellable(d, gems, true));

            Assert.IsTrue(Economy.IapLedger.FirstBonusShown(d, gems, true));
            Assert.IsFalse(Economy.IapLedger.FirstBonusShown(d, gems, false), "2배 배지는 동기화 뒤에만");
            Records.PurchaseEntry promised = Economy.IapLedger.Preview(d, gems, false);
            Assert.IsFalse(promised.firstBonus);
            Assert.AreEqual(gems.FreeGems, promised.freeGems, "확인 창도 2배를 약속하지 않는다");
            Assert.IsTrue(Economy.IapLedger.Preview(d, gems).firstBonus, "실제 지급 계산은 그대로 (더 받을 수는 있어도 덜 받지는 않는다)");
        }

        [Test]
        public void SwitchInterrupted_EachStage_ResumesIntoTargetOrFinishesGuestDeletion_NeverNewGuestWithTargetData()
        {
            const string guest = "guest-1", target = "kakao-b";
            // ① 게스트 정리: 닫기 전 · 닫은 뒤 · 보관 뒤 어디서 꺼져도 같은 게스트 세션이면 닫기 · 보관 · 지우기를 마저 한다 (다시 열지 않는다)
            Assert.AreEqual(AccountRules.SwitchStep.Nothing, AccountRules.DecideSwitchPrep(null, guest, true));
            Assert.AreEqual(AccountRules.SwitchStep.FinishGuestCleanup, AccountRules.DecideSwitchPrep(guest, guest, true));
            Assert.AreEqual(AccountRules.SwitchStep.Clear, AccountRules.DecideSwitchPrep(guest, "guest-2", null), "지운 뒤 꺼짐: 세션도 지워져 새 게스트 — 지운 게스트는 아무것도 없다");
            Assert.AreEqual(AccountRules.SwitchStep.Hold, AccountRules.DecideSwitchPrep(guest, null, null), "로그인 전 (오프라인)");

            // ② 그 계정 진행 고르기
            Assert.AreEqual(AccountRules.SwitchStep.Nothing, AccountRules.DecideSwitchTarget(null, guest));
            Assert.AreEqual(AccountRules.SwitchStep.Clear, AccountRules.DecideSwitchTarget(string.Empty, null), "그 계정 로그인 전에 꺼짐: 받은 것이 없다 → 평소대로");
            Assert.AreEqual(AccountRules.SwitchStep.ResumeTarget, AccountRules.DecideSwitchTarget(string.Empty, target), "로그인 직후 꺼짐: 남은 세션은 그 계정뿐");
            Assert.AreEqual(AccountRules.SwitchStep.ResumeTarget, AccountRules.DecideSwitchTarget(target, target), "고르기 전 · 그 계정 진행을 받은 뒤 표시를 지우기 전: 그 계정 세션 그대로");
            Assert.AreEqual(AccountRules.SwitchStep.Hold, AccountRules.DecideSwitchTarget(target, null), "그 계정에 들어간 뒤 세션이 없음: 새 게스트를 만들지 않는다");
            Assert.AreEqual(AccountRules.SwitchStep.Hold, AccountRules.DecideSwitchTarget(target, "someone-else"), "다른 플레이어 세션으로 이어 가지 않는다 (7차 3)");

            // 어느 단계에서도 "그 계정에 들어간 뒤(②, 대상 기록됨)"에 새 게스트 · 다른 플레이어로 가는 답은 없다
            foreach (string current in new[] { null, target, "someone-else" })
                Assert.AreNotEqual(AccountRules.SwitchStep.Clear, AccountRules.DecideSwitchTarget(target, current));
        }

        [TestCase("temp")]
        [TestCase("backup")]
        [TestCase("promote")]
        public void Replace_IoFailureAtEachStep_RollsBackMemoryAndDisk_NoOtherAccountTempLeft(string step)
        {
            // 7차 3: 계정 바꾸기에서 B 진행으로 바꾸다 어느 단계에서 입출력 오류가 나도 → 메모리 · 디스크 모두 A 그대로,
            // B 글의 임시 파일이 남지 않는다 (남으면 다음 실행이 "교체 도중 꺼져 남은 완성본"으로 읽어 다른 플레이어의 진행이 된다)
            var store = new SaveStore(_folder);
            SaveSystem.Use(store);
            SaveData a = Progressed(3, 10, 0, 5, 100);
            Assert.IsTrue(SaveSystem.Replace(a));
            string aOnDisk = File.ReadAllText(store.MainPath);
            SaveData b = Progressed(9, 999, 50, 40, 200);

            store.FaultBeforeStep = s => { if (s == step) throw new IOException("흉내: " + s); };
            LogAssert.Expect(LogType.Error, new Regex("저장"));
            Assert.IsFalse(SaveSystem.Replace(b));
            store.FaultBeforeStep = null;

            Assert.AreEqual(3, SaveSystem.Data.unlockedChapter, "메모리는 A");
            Assert.IsTrue(File.Exists(store.MainPath), "본 파일이 있다");
            Assert.AreEqual(aOnDisk, File.ReadAllText(store.MainPath), "디스크도 A");
            Assert.IsFalse(File.Exists(store.TempPath), "B 임시 파일이 남지 않는다");
            SaveData next = new SaveStore(_folder).Load();
            Assert.AreEqual((3, 10), (next.unlockedChapter, next.gemsFree), "다음 실행도 A");
        }

        [Test]
        public void Save_PromoteFails_ThenRestoreAlsoFails_TempStillRemoved_LoadsOlderCopyNotB()
        {
            // 되돌리기까지 실패해 본 파일이 없더라도 B 임시 파일은 지운다 → 다음 실행은 백업(A)을 읽는다 (B를 완성본으로 올리지 않는다)
            var store = new SaveStore(_folder);
            store.Save(Progressed(3, 10, 0, 5, 100));
            store.FaultBeforeStep = s =>
            {
                if (s != "promote") return;
                File.Move(store.BackupPath, store.BackupPath + ".away"); // 되돌릴 백업이 사라진 흉내
                throw new IOException("흉내: promote");
            };
            Assert.Throws<IOException>(() => store.Save(Progressed(9, 999, 50, 40, 200)));
            Assert.IsFalse(File.Exists(store.TempPath));
            File.Move(store.BackupPath + ".away", store.BackupPath);
            SaveData next = new SaveStore(_folder).Load();
            Assert.AreEqual(3, next.unlockedChapter);
        }

        [Test]
        public void DeletionInterrupted_AtAnyStep_NextLaunchWipesBeforeUse_NewGuestNeverGetsOldData()
        {
            // 8차 🔴: 계정 B 삭제 — 삭제 표시는 서버 단계(카카오 연결 끊기 · 결제 보관 · 클라우드 지우기 · 계정 지우기 · 세션 지우기) 전에 남는다.
            // 어느 단계 뒤에 꺼져도 다음 실행은 저장을 읽기 전에 이 기기 진행 · 백업을 지우고 새 게임으로 (동의부터 다시)
            var store = new SaveStore(_folder);
            SaveData b = Progressed(9, 999, 50, 40, 200);
            b.purchases.ledger.Add(new PurchaseEntry { transactionId = "sha256:b", productId = "gems_t4", paidGems = 2750 });
            store.Save(b);
            store.RotateRunBackups();
            store.Save(b); // 본 파일 + .bak + 판 백업

            SaveSystem.Use(store);
            Assert.AreEqual(9, SaveSystem.Data.unlockedChapter);
            SaveSystem.WipeOnNextLoad(true); // 삭제 시작 (서버 단계 전)
            Assert.AreEqual(9, SaveSystem.Data.unlockedChapter, "지금 실행의 메모리는 그대로");
            Assert.IsFalse(SaveSystem.WipePending, "지금 실행은 막히지 않는다");

            // 다음 실행: 표시가 남은 채 저장을 처음 읽는다 (실제로는 PlayerPrefs)
            SaveSystem.Use(new SaveStore(_folder));
            SaveSystem.WipeOnNextLoad(true);
            SaveData next = SaveSystem.Data;
            SaveData fresh = SaveStore.NewGame();
            Assert.AreEqual((fresh.unlockedChapter, 0, 0), (next.unlockedChapter, next.gemsPaid, next.purchases.ledger.Count), "B 진행 · 유료 보석 · 결제 장부 없음");
            Assert.IsTrue(Consent.NeedsConsent(next), "동의부터 다시");
            Assert.IsFalse(SaveSystem.WipePending, "지우기를 끝냈다");
            foreach (string file in Directory.GetFiles(_folder))
                StringAssert.DoesNotContain("sha256:b", File.ReadAllText(file), "백업에도 B가 남지 않는다: " + Path.GetFileName(file));
            Assert.AreEqual(fresh.unlockedChapter, new SaveStore(_folder).Load().unlockedChapter);
        }

        [Test]
        public void DeletionCancelledOrFailed_ProgressKept()
        {
            var store = new SaveStore(_folder);
            store.Save(Progressed(9, 999, 50, 40, 200));
            SaveSystem.Use(store);
            _ = SaveSystem.Data;
            SaveSystem.WipeOnNextLoad(true);
            SaveSystem.WipeOnNextLoad(false); // 연결 끊기 그만둠 · 서버 단계 실패 → 계정이 남았다
            SaveSystem.Use(new SaveStore(_folder));
            Assert.AreEqual(9, SaveSystem.Data.unlockedChapter);
        }

        [Test]
        public void DeletionResume_EachStep()
        {
            const string b = "player-b";
            Assert.AreEqual(AccountRules.DeletionStep.Nothing, AccountRules.DecideDeletionResume(null, true, b, false));
            // 연결 끊기 · 결제 보관 · 클라우드 지우기 뒤 꺼짐: 그 계정이 아직 있다 → 서버 단계를 마저
            Assert.AreEqual(AccountRules.DeletionStep.FinishRemote, AccountRules.DecideDeletionResume(b, true, b, false));
            // 계정 지우기(서버)는 됐는데 세션을 지우기 전 꺼짐: 서버가 세션을 거절 → 끝남
            Assert.AreEqual(AccountRules.DeletionStep.Done, AccountRules.DecideDeletionResume(b, true, null, true));
            // 세션까지 지운 뒤 꺼짐
            Assert.AreEqual(AccountRules.DeletionStep.Done, AccountRules.DecideDeletionResume(b, false, null, false));
            Assert.AreEqual(AccountRules.DeletionStep.Done, AccountRules.DecideDeletionResume(b, true, "someone-else", false));
        }

        [Test]
        public void SwitchPrep_NeverDeletesALinkedOrUnverifiedAccount()
        {
            // 7차 2: 정리 표시가 남은 채 그 게스트에 새 카카오 계정을 연결했다 (같은 플레이어 ID) → 다음 정리가 그 계정을 지우면 안 된다
            const string guest = "guest-1";
            Assert.AreEqual(AccountRules.SwitchStep.Clear, AccountRules.DecideSwitchPrep(guest, guest, false), "연결된 계정: 표시만 지우고 아무것도 지우지 않는다");
            Assert.AreEqual(AccountRules.SwitchStep.Hold, AccountRules.DecideSwitchPrep(guest, guest, null), "플레이어 정보를 못 받음: 지우지 않고 기다린다");
            Assert.AreEqual(AccountRules.SwitchStep.FinishGuestCleanup, AccountRules.DecideSwitchPrep(guest, guest, true));

            Assert.IsTrue(AccountRules.IsAnonymous(0, null));
            Assert.IsTrue(AccountRules.IsAnonymous(0, string.Empty));
            Assert.IsFalse(AccountRules.IsAnonymous(1, null), "외부 계정(카카오 등)이 붙음");
            Assert.IsFalse(AccountRules.IsAnonymous(0, "player"), "사용자 이름이 있음");
        }

        [Test]
        public void SwitchTarget_WithAnyPurchase_OnlyCloudOption()
        {
            SaveData empty = SaveStore.NewGame();
            var none = new Economy.PurchaseServer.Ledger { status = "ok" };
            Assert.IsFalse(AccountRules.TargetHasPurchases(empty, none), "결제 없는 계정: 두 쪽 다 고를 수 있다");

            SaveData paidCloud = SaveStore.NewGame();
            paidCloud.purchases.ledger.Add(new PurchaseEntry { transactionId = "sha256:x", productId = "gems_t1", voided = true });
            Assert.IsTrue(AccountRules.TargetHasPurchases(paidCloud, none), "클라우드 장부에 (회수된 것이라도) 결제");

            var paidServer = new Economy.PurchaseServer.Ledger { status = "ok" };
            paidServer.purchases.Add(new Economy.PurchaseServer.Purchase { tokenHash = "x", productId = "gems_t1" });
            Assert.IsTrue(AccountRules.TargetHasPurchases(empty, paidServer), "서버 장부에 결제");
            Assert.IsTrue(AccountRules.TargetHasPurchases(empty, null), "서버 장부를 못 받음 → 있는 것으로");
            Assert.IsTrue(AccountRules.TargetHasPurchases(empty, new Economy.PurchaseServer.Ledger { status = "retry" }));
        }

        [Test]
        public void FreshStart_HasUserIdRightAway()
        {
            Assert.IsFalse(string.IsNullOrEmpty(new SaveStore(_folder).Load().userId));
        }
    }
}
