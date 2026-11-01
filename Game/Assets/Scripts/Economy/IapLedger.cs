using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using HistoryHeroes.Records;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 결제 지급 장부 (저장 구조 13의 purchases). 상점 연결 없이 테스트한다 — 실제 상점 호출은 IapService.
    /// 거래 번호(구글 구매 토큰)가 장부에 있으면 다시 주지 않는다: 지급 → 저장 → 구매 확정 사이에 앱이 꺼지면
    /// 상점이 같은 거래를 다시 보내는데, 그때는 확정만 한다.
    /// 기기 상점(안드로이드)은 지급 수량을 서버가 정하고(ApplyServer · PurchaseServer), 에디터 가짜 상점만 Apply로 이 기기에서 정한다.
    /// </summary>
    public static class IapLedger
    {
        public enum Result
        {
            /// <summary>모르는 상품 · 거래 번호 없음 · 저장을 바꿀 수 없음 → 지급도 확정도 안 한다</summary>
            Invalid,
            Granted,
            /// <summary>이미 지급한 거래 (다시 보낸 것) → 확정만</summary>
            AlreadyGranted,
            /// <summary>계정당 1번 상품을 또 결제했다 → 지급 · 확정 안 함 (확정하지 않은 결제는 구글이 3일 안에 환불)</summary>
            AlreadyClaimed,
        }

        /// <summary>서버 장부에서 되살린 칸은 토큰 원문이 없어 거래 번호를 이 머리 + SHA-256(토큰)으로 적는다</summary>
        public const string HashedPrefix = "sha256:";

        /// <summary>SHA-256 소문자 16진수 (서버 PurchaseVerifier.Hash와 같다). 토큰 맞추기 · 구글 계정 표시(UGS 플레이어 ID)에 쓴다</summary>
        public static string Hash(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>같은 결제인지 볼 열쇠: 토큰 원문이면 그 해시, "sha256:"로 적힌 칸이면 그 해시</summary>
        private static string TokenKey(string transactionId) =>
            transactionId.StartsWith(HashedPrefix, StringComparison.Ordinal) ? transactionId.Substring(HashedPrefix.Length) : Hash(transactionId);

        /// <summary>이 거래가 장부에 있나 (토큰 원문 · 서버에서 되살린 해시 칸 어느 쪽이든 같은 결제면 찾는다)</summary>
        public static PurchaseEntry Find(SaveData d, string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) return null;
            string key = null;
            foreach (PurchaseEntry e in d.purchases.ledger)
            {
                if (e.transactionId == transactionId) return e;
                if (string.IsNullOrEmpty(e.transactionId)) continue;
                key ??= TokenKey(transactionId);
                if (TokenKey(e.transactionId) == key) return e;
            }

            return null;
        }

        /// <summary>이 보석 상품의 첫 구매 2배가 아직 남았나</summary>
        public static bool FirstBonusLeft(SaveData d, IapCatalog.Item item) => item.FirstDouble && !d.purchases.firstBought.Contains(item.Id);

        /// <summary>상점에 보여도 되나 (계정당 1번 상품은 받은 뒤 숨긴다)</summary>
        public static bool Available(SaveData d, IapCatalog.Item item) => !item.OncePerAccount || !d.purchases.starterClaimed;

        /// <summary>
        /// 지금 팔아도 되나: 계정 동기화가 끝났을 때만 (클라우드를 맞추기 전의 오래된 장부로는 계정당 1번 상품의 "아직 안 받음"도,
        /// 보석의 첫 구매 2배도 믿을 수 없다 — Codex 교차 리뷰 2026-11-01 3번, 재확인에서 보석 2배 경로까지 막음). 게스트는 늘 맞춰진 상태다
        /// </summary>
        public static bool Sellable(SaveData d, IapCatalog.Item item, bool syncSettled) => Available(d, item) && syncSettled;

        /// <summary>첫 구매 2배를 약속해도 되나 (배지 · 확인 창). 동기화가 안 끝났으면 약속하지 않는다 — 실제 지급이 2배면 더 받을 뿐</summary>
        public static bool FirstBonusShown(SaveData d, IapCatalog.Item item, bool syncSettled) => syncSettled && FirstBonusLeft(d, item);

        /// <summary>지금 사면 받을 것. 확인 창과 실제 지급이 같은 계산을 쓴다. promiseBonus가 false면 첫 구매 2배를 빼고 보여 준다 (약속 못 할 때)</summary>
        public static PurchaseEntry Preview(SaveData d, IapCatalog.Item item, bool promiseBonus = true)
        {
            bool first = promiseBonus && FirstBonusLeft(d, item);
            return new PurchaseEntry
            {
                productId = item.Id,
                paidGems = item.PaidGems,
                freeGems = item.FreeGems + (first ? item.PaidGems : 0),
                tickets = item.Tickets,
                gold = item.Gold,
                firstBonus = first,
            };
        }

        /// <summary>
        /// 한 거래를 지급하고 장부에 적는다 (저장은 부르는 쪽이 SaveSystem.Commit으로 — 저장이 실패하면 통째로 되돌린다).
        /// entry: Granted · AlreadyGranted면 그 거래의 지급 내역
        /// </summary>
        public static Result Apply(SaveData d, string productId, string transactionId, DateTime utcNow, out PurchaseEntry entry)
        {
            entry = Find(d, transactionId);
            if (entry != null) return Result.AlreadyGranted;
            IapCatalog.Item item = IapCatalog.Find(productId);
            if (item == null || string.IsNullOrEmpty(transactionId)) return Result.Invalid;
            if (!Available(d, item)) return Result.AlreadyClaimed;

            entry = Preview(d, item);
            entry.transactionId = transactionId;
            entry.utc = utcNow.ToUniversalTime().ToString("o");
            Wallet.AddPaidGems(d, entry.paidGems);
            Wallet.AddFreeGems(d, entry.freeGems);
            Wallet.AddGold(d, entry.gold);
            if (entry.tickets > 0) d.AddItem(Wallet.Ticket, entry.tickets);
            if (entry.firstBonus) d.purchases.firstBought.Add(item.Id);
            if (item.OncePerAccount) d.purchases.starterClaimed = true;
            d.purchases.ledger.Add(entry);
            return Result.Granted;
        }

        /// <summary>
        /// 서버가 정한 지급을 이 기기 장부에 넣는다 (기기 상점의 서버 검증 · 로그인 뒤 서버 장부 맞추기). 수량 · 첫 구매 2배는 서버 값 그대로.
        /// transactionId: 상점이 준 토큰. 없으면(맞추기) "sha256:" + 서버 해시. 같은 결제가 이미 있으면 AlreadyGranted (두 번 주지 않는다).
        /// 저장은 부르는 쪽이 SaveSystem.Commit으로
        /// </summary>
        public static Result ApplyServer(SaveData d, PurchaseServer.Purchase p, string transactionId, out PurchaseEntry entry)
        {
            entry = null;
            if (p == null || string.IsNullOrEmpty(p.tokenHash) || string.IsNullOrEmpty(p.productId)) return Result.Invalid;
            string id = string.IsNullOrEmpty(transactionId) ? HashedPrefix + p.tokenHash : transactionId;
            entry = Find(d, id);
            if (entry != null) return Result.AlreadyGranted;

            entry = new PurchaseEntry
            {
                transactionId = id,
                productId = p.productId,
                utc = p.utc,
                paidGems = p.paidGems,
                freeGems = p.freeGems,
                tickets = p.tickets,
                gold = p.gold,
                firstBonus = p.firstBonus,
            };
            Wallet.AddPaidGems(d, entry.paidGems);
            Wallet.AddFreeGems(d, entry.freeGems);
            Wallet.AddGold(d, entry.gold);
            if (entry.tickets > 0) d.AddItem(Wallet.Ticket, entry.tickets);
            if (entry.firstBonus && !d.purchases.firstBought.Contains(entry.productId)) d.purchases.firstBought.Add(entry.productId);
            if (IapCatalog.Find(entry.productId)?.OncePerAccount == true) d.purchases.starterClaimed = true;
            d.purchases.ledger.Add(entry);
            return Result.Granted;
        }

        /// <summary>
        /// 로그인 뒤 서버 장부와 맞추기: 서버에만 있는 결제를 이 기기에 다시 넣는다 (기기 저장을 잃었을 때 · 같은 계정의 다른 기기에서 산 것).
        /// 그래서 이 기기의 계정당 1번 · 첫 구매 2배 표시가 서버와 같아진다. 몇 번 불러도 두 번 주지 않는다. 돌려주는 값: 넣은 결제 수
        /// </summary>
        public static int MergeServer(SaveData d, IEnumerable<PurchaseServer.Purchase> purchases)
        {
            int added = 0;
            if (purchases == null) return 0;
            foreach (PurchaseServer.Purchase p in purchases)
                if (ApplyServer(d, p, null, out _) == Result.Granted) added++;
            return added;
        }

        /// <summary>
        /// 저장을 통째로 바꾸기 전에(클라우드 진행 고르기) 다른 쪽에만 있는 결제를 옮겨 담는다 — 진짜 돈으로 산 것은 어느 진행을 골라도 줄지 않는다 (LiveOps 3-2).
        /// target 장부에 없는 거래만 더하고 그 거래로 받은 보석 · 소환권 · 골드를 target에 다시 준다. 첫 구매 2배 · 패키지 받음은 합친다. 몇 번 불러도 두 번 주지 않는다.
        /// 돌려주는 값: 옮긴 거래 수
        /// </summary>
        public static int MergeInto(SaveData target, SaveData source)
        {
            if (target == null || source?.purchases == null || target == source) return 0;
            target.purchases ??= new PurchaseSave();
            int added = 0;
            foreach (PurchaseEntry e in source.purchases.ledger)
            {
                if (e == null || string.IsNullOrEmpty(e.transactionId) || Find(target, e.transactionId) != null) continue;
                if (!e.refunded && !e.voided)
                {
                    Wallet.AddPaidGems(target, e.paidGems);
                    Wallet.AddFreeGems(target, e.freeGems);
                    Wallet.AddGold(target, e.gold);
                    if (e.tickets > 0) target.AddItem(Wallet.Ticket, e.tickets);
                }
                target.purchases.ledger.Add(e.Copy());
                added++;
            }

            foreach (string id in source.purchases.firstBought)
                if (!string.IsNullOrEmpty(id) && !target.purchases.firstBought.Contains(id)) target.purchases.firstBought.Add(id);
            target.purchases.starterClaimed |= source.purchases.starterClaimed;
            return added;
        }

        /// <summary>"골드 +10,000 · 보석 +300 · 고급 소환권 +2" (보상 그림 포함)</summary>
        public static string Text(PurchaseEntry e) =>
            new Reward { Gold = e.gold, Gems = e.paidGems + e.freeGems, Tickets = e.tickets }.ToString();
    }
}
