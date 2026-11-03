using System;
using System.Collections.Generic;
using System.Linq;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;

namespace HistoryHeroes.Meta
{
    /// <summary>
    /// 우편함 (LiveOps 4장). 지금은 **기기 저장 안의 우편**만 다룬다 — 보석 잔액이 아직 기기 기준이라 서버가 받기를 처리해도 더 안전해지지 않는다.
    /// 서버 전체 우편(운영 보상 · 점검 보상)과 쿠폰은 나중에 Cloud Code로 (LiveOps 4장).
    /// </summary>
    public static class MailBox
    {
        public const int MaxMails = 100;
        public const string WelcomeId = "welcome_v1";

        public const string KindGold = "gold";
        public const string KindGem = "gem";
        public const string KindTicket = "ticket";
        public const string KindGearShard = "gear_shard";

        /// <summary>환영 선물: 무료 보석 300 + 고급 소환권 3, 기한 없음</summary>
        public static MailItem Welcome(DateTimeOffset now) => new MailItem
        {
            id = WelcomeId,
            title = "환영 선물",
            body = "역사 영웅의 세계에 온 걸 환영해요! 작은 선물이에요.",
            sentAt = now.ToUnixTimeSeconds(),
            expiresAt = 0,
            rewards = new List<MailReward>
            {
                new MailReward { kind = KindGem, amount = 300 },
                new MailReward { kind = KindTicket, amount = 3 },
            },
        };

        public static bool IsExpired(MailItem m, DateTimeOffset now) => m.expiresAt > 0 && now.ToUnixTimeSeconds() >= m.expiresAt;

        /// <summary>남은 날 (올림). 기한이 없으면 -1</summary>
        public static int DaysLeft(MailItem m, DateTimeOffset now)
        {
            if (m.expiresAt <= 0) return -1;
            long left = m.expiresAt - now.ToUnixTimeSeconds();
            return left <= 0 ? 0 : (int)((left + 86399) / 86400);
        }

        /// <summary>우편 보상 줄을 Reward 묶음으로</summary>
        public static Reward ToReward(MailItem m)
        {
            var r = new Reward();
            foreach (MailReward x in m.rewards)
            {
                int amount = Math.Max(0, x.amount);
                switch (x.kind)
                {
                    case KindGold: r.Gold += amount; break;
                    case KindGem: r.Gems += amount; break;
                    case KindTicket: r.Tickets += amount; break;
                    case KindGearShard: r.GearShards += amount; break;
                }
            }

            return r;
        }

        /// <summary>우편을 넣는다. 같은 번호가 한 번이라도 왔으면 아무것도 안 한다 (정리돼도 다시 오지 않음). 넣었으면 true</summary>
        public static bool Deliver(SaveData s, MailItem mail)
        {
            if (mail == null || string.IsNullOrEmpty(mail.id) || s.mail.deliveredIds.Contains(mail.id)) return false;
            s.mail.deliveredIds.Add(mail.id);
            s.mail.items.Add(mail);
            return true;
        }

        /// <summary>환영 선물을 계정당 한 번 보낸다 (옛 저장도 해당). 바뀌었으면 true</summary>
        public static bool EnsureWelcome(SaveData s, DateTimeOffset now) => Deliver(s, Welcome(now));

        /// <summary>한 통 받기. 이미 받았거나 기한이 지났거나 없는 번호면 false (아무것도 안 줌)</summary>
        public static bool Claim(SaveData s, string id, DateTimeOffset now, out Reward granted)
        {
            granted = default;
            MailItem m = s.mail.items.Find(x => x.id == id);
            if (m == null || m.claimed || IsExpired(m, now)) return false;
            m.claimed = true;
            granted = ToReward(m);
            Wallet.Grant(s, granted);
            return true;
        }

        /// <summary>받을 수 있는 우편을 모두 받는다. 받은 보상 합을 돌려준다</summary>
        public static Reward ClaimAll(SaveData s, DateTimeOffset now, out int count)
        {
            Reward total = default;
            count = 0;
            foreach (MailItem m in s.mail.items.ToList())
            {
                if (!Claim(s, m.id, now, out Reward r)) continue;
                total += r;
                count++;
            }

            return total;
        }

        /// <summary>안 받은 우편 수 (기한 지난 것 제외). 로비 빨간 점</summary>
        public static int UnclaimedCount(SaveData s, DateTimeOffset now) => s.mail.items.Count(m => !m.claimed && !IsExpired(m, now));

        /// <summary>기한 지난 우편을 지우고, 100통이 넘으면 가장 오래된 **받은** 우편부터 지운다 (안 받은 것은 안 지움). 바뀌었으면 true</summary>
        public static bool Prune(SaveData s, DateTimeOffset now)
        {
            int before = s.mail.items.Count;
            s.mail.items.RemoveAll(m => IsExpired(m, now));
            while (s.mail.items.Count > MaxMails)
            {
                MailItem oldest = null;
                foreach (MailItem m in s.mail.items)
                    if (m.claimed && (oldest == null || m.sentAt < oldest.sentAt)) oldest = m;
                if (oldest == null) break; // 남은 건 전부 안 받은 우편
                s.mail.items.Remove(oldest);
            }

            return s.mail.items.Count != before;
        }

        /// <summary>
        /// 로비가 열릴 때 부른다: 환영 선물 보내기 + 정리. 바뀌었으면 true (그러면 저장)
        /// </summary>
        public static bool Refresh(SaveData s, DateTimeOffset now)
        {
            bool changed = EnsureWelcome(s, now);
            return Prune(s, now) || changed;
        }

        /// <summary>보여 줄 순서: 안 받은 것 먼저(새것 위), 받은 것 뒤(새것 위). 기한 지난 것은 뺀다</summary>
        public static List<MailItem> Sorted(SaveData s, DateTimeOffset now) => s.mail.items
            .Where(m => !IsExpired(m, now))
            .OrderBy(m => m.claimed ? 1 : 0)
            .ThenByDescending(m => m.sentAt)
            .ToList();
    }
}
