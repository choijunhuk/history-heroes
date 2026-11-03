using System;
using HistoryHeroes.Economy;
using HistoryHeroes.Meta;
using HistoryHeroes.Records;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    /// <summary>우편함 (LiveOps 4장): 한 번만 도착 · 한 번만 받기 · 기한 · 정리</summary>
    public sealed class MailBoxTests
    {
        private static readonly TimeSpan Kst = TimeSpan.FromHours(9);
        private static DateTimeOffset At(int day) => new DateTimeOffset(2026, 11, day, 12, 0, 0, Kst);

        private static MailItem Gift(string id, DateTimeOffset sent, int days = 0, int gems = 10) => new MailItem
        {
            id = id,
            title = id,
            sentAt = sent.ToUnixTimeSeconds(),
            expiresAt = days > 0 ? sent.AddDays(days).ToUnixTimeSeconds() : 0,
            rewards = { new MailReward { kind = MailBox.KindGem, amount = gems } },
        };

        [Test]
        public void Deliver_OnceById_EvenAfterPrune()
        {
            var s = new SaveData();
            Assert.IsTrue(MailBox.Deliver(s, Gift("a", At(1), days: 1)));
            Assert.IsFalse(MailBox.Deliver(s, Gift("a", At(1))));
            MailBox.Prune(s, At(10)); // 기한 지나 지워짐
            Assert.AreEqual(0, s.mail.items.Count);
            Assert.IsFalse(MailBox.Deliver(s, Gift("a", At(10))), "지워져도 같은 번호는 다시 오지 않는다");
        }

        [Test]
        public void Welcome_OnceEvenIfCalledTwice()
        {
            var s = new SaveData();
            Assert.IsTrue(MailBox.EnsureWelcome(s, At(1)));
            Assert.IsFalse(MailBox.EnsureWelcome(s, At(2)));
            Assert.AreEqual(1, s.mail.items.Count);
            Assert.AreEqual(1, MailBox.UnclaimedCount(s, At(2)));
        }

        [Test]
        public void Claim_WelcomeGivesFreeGemsAndTickets_Once()
        {
            var s = new SaveData();
            MailBox.EnsureWelcome(s, At(1));
            Assert.IsTrue(MailBox.Claim(s, MailBox.WelcomeId, At(1), out Reward r));
            Assert.AreEqual(300, r.Gems);
            Assert.AreEqual(3, r.Tickets);
            Assert.AreEqual(300, s.gemsFree);
            Assert.AreEqual(0, s.gemsPaid, "무료 보석 쪽으로");
            Assert.AreEqual(3, s.ItemCount(Wallet.Ticket));
            Assert.IsFalse(MailBox.Claim(s, MailBox.WelcomeId, At(1), out _), "두 번 받기 없음");
            Assert.AreEqual(300, s.gemsFree);
            Assert.AreEqual(3, s.ItemCount(Wallet.Ticket));
            Assert.AreEqual(0, MailBox.UnclaimedCount(s, At(1)));
        }

        [Test]
        public void ClaimAll_GrantsEveryUnclaimed_SkipsExpired()
        {
            var s = new SaveData();
            MailBox.Deliver(s, Gift("a", At(1), gems: 10));
            MailBox.Deliver(s, Gift("b", At(1), gems: 20));
            MailBox.Deliver(s, Gift("old", At(1), days: 1, gems: 999));
            MailBox.Claim(s, "a", At(5), out _);
            Reward total = MailBox.ClaimAll(s, At(5), out int count);
            Assert.AreEqual(1, count);
            Assert.AreEqual(20, total.Gems);
            Assert.AreEqual(30, s.gemsFree);
            MailBox.ClaimAll(s, At(5), out count);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Expired_CannotBeClaimed()
        {
            var s = new SaveData();
            MailBox.Deliver(s, Gift("a", At(1), days: 2));
            Assert.AreEqual(2, MailBox.DaysLeft(s.mail.items[0], At(1)));
            Assert.IsFalse(MailBox.Claim(s, "a", At(4), out _));
            Assert.AreEqual(0, s.gemsFree);
            Assert.AreEqual(0, MailBox.UnclaimedCount(s, At(4)));
        }

        [Test]
        public void Prune_DropsOldestClaimed_NeverUnclaimed()
        {
            var s = new SaveData();
            for (int i = 0; i < 105; i++) MailBox.Deliver(s, Gift("m" + i, At(1).AddMinutes(i)));
            for (int i = 0; i < 3; i++) MailBox.Claim(s, "m" + i, At(2), out _); // 가장 오래된 3통만 받음
            Assert.IsTrue(MailBox.Prune(s, At(2)));
            Assert.AreEqual(102, s.mail.items.Count, "받은 3통만 지우고 안 받은 우편은 100통 넘어도 남는다");
            Assert.IsFalse(s.mail.items.Exists(m => m.claimed));
            Assert.AreEqual(102, MailBox.UnclaimedCount(s, At(2)));
        }

        [Test]
        public void Schema13Save_MigratesToEmptyMailbox_ThenGetsWelcomeOnce()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hh-mail-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                System.IO.File.WriteAllText(store.MainPath, SaveStore.Sign("{\"schemaVersion\":13,\"gold\":55,\"gemsFree\":40,\"gemsPaid\":0,\"tutorialDone\":true}"));
                SaveData save = store.Load();
                Assert.AreEqual(SaveData.CurrentSchemaVersion, save.schemaVersion);
                Assert.AreEqual((55, 40), (save.gold, save.gemsFree), "옛 진행은 그대로");
                Assert.IsNotNull(save.mail);
                Assert.IsEmpty(save.mail.items);

                Assert.IsTrue(MailBox.EnsureWelcome(save, At(1)), "옛 계정도 환영 선물을 한 번 받는다");
                store.Save(save);
                SaveData back = new SaveStore(folder).Load();
                Assert.AreEqual(1, MailBox.UnclaimedCount(back, At(2)));
                Assert.IsFalse(MailBox.EnsureWelcome(back, At(2)), "다시 읽은 저장에도 두 번 오지 않는다");
            }
            finally
            {
                System.IO.Directory.Delete(folder, true);
            }
        }
    }
}
