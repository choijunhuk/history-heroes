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

        /// <summary>
        /// 장부에 적는 거래 번호: "sha256:" + SHA-256(토큰). 이미 그 모양이면 그대로. 구매 토큰 원문은 기기 · 클라우드 저장에 남기지 않는다 (개인정보 처리방침 1장).
        /// 서버 장부의 tokenHash와 같은 해시라 서버 기록과 바로 맞춰진다
        /// </summary>
        public static string IdOf(string transactionId) =>
            string.IsNullOrEmpty(transactionId) || transactionId.StartsWith(HashedPrefix, StringComparison.Ordinal) ? transactionId : HashedPrefix + Hash(transactionId);

        /// <summary>이 거래가 장부에 있나 (토큰 원문으로 물어도, 해시로 적힌 칸을 찾는다)</summary>
        public static PurchaseEntry Find(SaveData d, string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) return null;
            string id = IdOf(transactionId);
            foreach (PurchaseEntry e in d.purchases.ledger)
                if (e != null && IdOf(e.transactionId) == id) return e;
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
            entry.transactionId = IdOf(transactionId);
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
        /// transactionId: 상점이 준 토큰 (없으면 서버 해시로). 같은 결제가 이미 있으면 AlreadyGranted (두 번 주지 않는다).
        /// 서버가 환불됐다고 한 결제(voided)는 지급하지 않는다: 이 기기에 있으면 회수하고, 없으면 "회수됨"으로만 적어 다시는 넣지 않는다.
        /// 저장은 부르는 쪽이 SaveSystem.Commit으로
        /// </summary>
        public static Result ApplyServer(SaveData d, PurchaseServer.Purchase p, string transactionId, out PurchaseEntry entry)
        {
            entry = null;
            if (p == null || string.IsNullOrEmpty(p.tokenHash) || string.IsNullOrEmpty(p.productId)) return Result.Invalid;
            string id = string.IsNullOrEmpty(transactionId) ? HashedPrefix + p.tokenHash : IdOf(transactionId);
            entry = Find(d, id);
            if (p.voided)
            {
                if (entry != null) ClawBack(d, entry);
                else d.purchases.ledger.Add(entry = new PurchaseEntry { transactionId = id, productId = p.productId, utc = p.utc, voided = true });
                return Result.Invalid;
            }

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
        /// 환불 · 취소된 결제로 준 것을 회수한다 (LiveOps 3-4): 유료 · 무료 보석을 그만큼 빼고(모자라면 음수 — 상점 · 뽑기 잠김),
        /// 골드 · 고급 소환권은 남은 만큼만 뺀다 (이미 쓴 것 · 뽑은 장비는 그대로). 이미 회수한 칸은 다시 빼지 않는다. 돌려주는 값: 뺀 보석
        /// </summary>
        public static int ClawBack(SaveData d, PurchaseEntry e)
        {
            if (e == null || e.voided) return 0;
            e.voided = true;
            if (e.refunded) return 0; // 다른 길로 이미 돌려받은 결제
            d.gemsPaid -= e.paidGems;
            d.gemsFree -= e.freeGems;
            Wallet.Settle(d);
            d.gold = Math.Max(0, d.gold - e.gold);
            int tickets = Math.Min(e.tickets, d.ItemCount(Wallet.Ticket));
            if (tickets > 0) d.AddItem(Wallet.Ticket, -tickets);
            return e.paidGems + e.freeGems;
        }

        /// <summary>서버 장부 맞추기 결과: 새로 넣은 결제 · 회수한 결제 · 회수한 보석</summary>
        public struct ServerMerge
        {
            public int Added;
            public int Voided;
            public int GemsTaken;
        }

        /// <summary>
        /// 로그인 뒤 서버 장부와 맞추기: 서버에만 있는 결제를 이 기기에 다시 넣고, 서버가 환불됐다고 한 결제는 회수한다 (다시는 넣지 않는다).
        /// 그래서 이 기기의 계정당 1번 · 첫 구매 2배 표시가 서버와 같아진다. 몇 번 불러도 두 번 주거나 두 번 빼지 않는다.
        /// 일부러 그렇게 둔 것: 클라우드 고르기로 더 옛 진행을 받으면 그 뒤에 산 결제가 여기서 다시 들어온다 (산 것은 되살린다).
        /// 그 결제로 산 보석을 쓴 기록은 옛 진행에 없으니 보석이 다시 생긴 것처럼 보인다 — 지갑이 아직 기기 기준이라 생기는 알려진 한계 (Monetization 7-1)
        /// </summary>
        public static ServerMerge MergeServer(SaveData d, IEnumerable<PurchaseServer.Purchase> purchases)
        {
            var result = new ServerMerge();
            if (purchases == null) return result;
            foreach (PurchaseServer.Purchase p in purchases)
            {
                if (p == null) continue;
                if (p.voided)
                {
                    PurchaseEntry local = string.IsNullOrEmpty(p.tokenHash) ? null : Find(d, HashedPrefix + p.tokenHash);
                    bool wasGranted = local != null && !local.voided;
                    int taken = wasGranted ? ClawBack(d, local) : 0;
                    if (local == null) ApplyServer(d, p, null, out _); // 회수됨으로만 적는다
                    if (!wasGranted) continue;
                    result.Voided++;
                    result.GemsTaken += taken;
                    continue;
                }

                if (ApplyServer(d, p, null, out _) == Result.Granted) result.Added++;
            }

            return result;
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
                if (e == null || string.IsNullOrEmpty(e.transactionId)) continue;
                PurchaseEntry existing = Find(target, e.transactionId);
                if (existing != null)
                {
                    if (e.voided) ClawBack(target, existing); // 한쪽에서 회수한 결제는 고른 쪽에서도 회수한다 (되살리지 않는다)
                    continue;
                }
                if (!e.refunded && !e.voided)
                {
                    Wallet.AddPaidGems(target, e.paidGems);
                    Wallet.AddFreeGems(target, e.freeGems);
                    Wallet.AddGold(target, e.gold);
                    if (e.tickets > 0) target.AddItem(Wallet.Ticket, e.tickets);
                }
                PurchaseEntry copy = e.Copy();
                copy.transactionId = IdOf(copy.transactionId);
                target.purchases.ledger.Add(copy);
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
